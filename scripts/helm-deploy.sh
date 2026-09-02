#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
[[ -f "$ROOT/scripts/deploy.env" ]] && set -a && source "$ROOT/scripts/deploy.env" && set +a

DEPLOY_TARGET="${DEPLOY_TARGET:-cloud}"
NAMESPACE="${KUBE_NAMESPACE:-flight-booking}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
HOST="${HOST:-}"
STORAGE_CLASS="${YC_POSTGRES_STORAGE_CLASS:-}"
SKIP_BUILD="${SKIP_BUILD:-0}"

export PATH="${HOME}/yandex-cloud/bin:${HOME}/.local/bin:${PATH}"

ensure_kubectl() {
  if [[ "${K8S_TUNNEL:-0}" == "1" ]]; then
    if ! curl -sk --connect-timeout 3 -o /dev/null https://127.0.0.1:8443/; then
      ssh -o IdentitiesOnly=yes -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 \
        ${JUMP_SSH_KEY:+-i "$JUMP_SSH_KEY"} -f -N \
        -L "127.0.0.1:8443:${K8S_MASTER_IP:-46.243.210.98}:443" \
        "${JUMP_HOST:?set JUMP_HOST for tunnel}"
      sleep 2
    fi
    yc managed-kubernetes cluster get-credentials "${YC_CLUSTER_ID}" --external --force >/dev/null
    kubectl config set-cluster "yc-managed-k8s-${YC_CLUSTER_ID}" \
      --server=https://127.0.0.1:8443 --insecure-skip-tls-verify=true >/dev/null 2>&1 || true
  fi
}

resolve_cloud_config() {
  REGISTRY="${YC_REGISTRY:-}"
  if [[ -z "$REGISTRY" ]]; then
    REGISTRY_NAME="${YC_REGISTRY_NAME:-flight-booking-registry}"
    REGISTRY_ID=$(yc container registry get "$REGISTRY_NAME" --format json | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
    REGISTRY="cr.yandex/${REGISTRY_ID}"
  fi
  IMAGE_PULL_POLICY="Always"
  IMAGE_PREFIX="${REGISTRY}"

  if [[ -z "$HOST" ]]; then
    EXTERNAL_IP=$(kubectl -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
    [[ -n "$EXTERNAL_IP" ]] || { echo "ingress NLB IP not found. Run ./scripts/setup-ingress.sh first." >&2; exit 1; }
    HOST="${EXTERNAL_IP}.nip.io"
  fi

  [[ -n "$STORAGE_CLASS" ]] || STORAGE_CLASS="yc-network-hdd"
}

resolve_minikube_config() {
  command -v minikube >/dev/null
  if ! minikube status >/dev/null 2>&1; then
    minikube start --driver=docker --cpus="${MINIKUBE_CPUS:-4}" --memory="${MINIKUBE_MEMORY:-8192}"
  fi
  if ! minikube addons list | grep -q 'ingress.*enabled'; then
    minikube addons enable ingress
  fi
  if [[ "$SKIP_BUILD" != "1" ]]; then
    eval "$(minikube docker-env)"
    build_local() {
      docker build -t "flight-booking/$1:${IMAGE_TAG}" -f "$2" "$3" "${@:4}"
    }
    build_local identity "$ROOT/src/IdentityProvider/Dockerfile" "$ROOT/src"
    build_local statistics "$ROOT/src/StatisticsService/Dockerfile" "$ROOT/src"
    build_local bonus "$ROOT/src/BonusService/Dockerfile" "$ROOT/src"
    build_local flight "$ROOT/src/FlightService/Dockerfile" "$ROOT/src"
    build_local ticket "$ROOT/src/TicketService/Dockerfile" "$ROOT/src"
    build_local gateway "$ROOT/src/ApiGateway/Dockerfile" "$ROOT/src"
    build_local ui "$ROOT/ui/Dockerfile" "$ROOT/ui" --build-arg NGINX_CONF=nginx.k8s.conf
  fi
  IMAGE_PULL_POLICY="Never"
  IMAGE_PREFIX="flight-booking"
  [[ -n "$HOST" ]] || HOST="flight.local"
  STORAGE_CLASS=""
}

command -v kubectl >/dev/null
command -v helm >/dev/null

if [[ "$DEPLOY_TARGET" == "minikube" ]]; then
  resolve_minikube_config
else
  command -v yc >/dev/null
  ensure_kubectl
  resolve_cloud_config
fi

echo "==> helm deploy target=${DEPLOY_TARGET} namespace=${NAMESPACE} host=${HOST}"

kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$NAMESPACE" create configmap postgres-init-main \
  --from-file=10-create-user.sql="$ROOT/postgres/10-create-user.sql" \
  --from-file=20-create-databases.sh="$ROOT/postgres/20-create-databases.sh" \
  --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$NAMESPACE" create configmap postgres-init-scripts \
  --from-file="$ROOT/postgres/scripts/" \
  --dry-run=client -o yaml | kubectl apply -f -

POSTGRES_SET=(--wait --timeout 8m)
[[ -n "$STORAGE_CLASS" ]] && POSTGRES_SET+=(--set "persistence.storageClassName=${STORAGE_CLASS}")

helm upgrade --install postgres "$ROOT/charts/postgres" -n "$NAMESPACE" "${POSTGRES_SET[@]}"
helm upgrade --install kafka "$ROOT/charts/kafka" -n "$NAMESPACE" \
  --set resources.requests.memory=256Mi \
  --set resources.limits.memory=768Mi \
  --wait --timeout 8m

deploy_app() {
  local release="$1" values="$2" repo="$3"
  helm upgrade --install "$release" "$ROOT/charts/microservice" -n "$NAMESPACE" \
    -f "$ROOT/charts/microservice/${values}" \
    --set "image.repository=${IMAGE_PREFIX}/${repo}" \
    --set "image.tag=${IMAGE_TAG}" \
    --set "image.pullPolicy=${IMAGE_PULL_POLICY}" \
    --set "ingress.host=${HOST}" \
    --set ingress.className=nginx \
    --wait --timeout 8m
}

deploy_app identity-provider values-identity-provider.yaml identity
deploy_app statistics values-statistics.yaml statistics
deploy_app bonus values-bonus.yaml bonus
deploy_app flights values-flight.yaml flight
deploy_app tickets values-ticket.yaml ticket

helm upgrade --install gateway "$ROOT/charts/microservice" -n "$NAMESPACE" \
  -f "$ROOT/charts/microservice/values-gateway.yaml" \
  --set "image.repository=${IMAGE_PREFIX}/gateway" \
  --set "image.tag=${IMAGE_TAG}" \
  --set "image.pullPolicy=${IMAGE_PULL_POLICY}" \
  --set "ingress.host=${HOST}" \
  --set ingress.className=nginx \
  --set "env.Auth__PublicIdentityProviderUrl=http://${HOST}" \
  --set "env.Auth__Issuer=http://${HOST}" \
  --set "env.Auth__CallbackUrl=http://${HOST}/api/v1/callback" \
  --set "env.Auth__UiRedirectUrl=http://${HOST}/callback" \
  --wait --timeout 8m

helm upgrade --install identity-provider "$ROOT/charts/microservice" -n "$NAMESPACE" \
  -f "$ROOT/charts/microservice/values-identity-provider.yaml" \
  --set "image.repository=${IMAGE_PREFIX}/identity" \
  --set "image.tag=${IMAGE_TAG}" \
  --set "image.pullPolicy=${IMAGE_PULL_POLICY}" \
  --set "ingress.host=${HOST}" \
  --set ingress.className=nginx \
  --set "env.Auth__Issuer=http://${HOST}" \
  --set "env.Auth__UiLoginUrl=http://${HOST}/login" \
  --set "env.Auth__UiRedirectUrl=http://${HOST}/callback" \
  --wait --timeout 8m

deploy_app ui values-ui.yaml ui

kubectl -n "$NAMESPACE" get pods,ingress

if [[ "$DEPLOY_TARGET" == "minikube" ]]; then
  MINIKUBE_IP="$(minikube ip)"
  grep -qE "[[:space:]]${HOST}$" /etc/hosts 2>/dev/null || \
    echo "${MINIKUBE_IP} ${HOST}" | sudo tee -a /etc/hosts >/dev/null || true
  echo "Open: http://${HOST} (minikube ${MINIKUBE_IP})"
else
  echo "Open: http://${HOST}"
fi
