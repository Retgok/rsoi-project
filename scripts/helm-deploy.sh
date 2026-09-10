#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
[[ -f "$ROOT/scripts/deploy.env" ]] && set -a && source "$ROOT/scripts/deploy.env" && set +a

NAMESPACE="${KUBE_NAMESPACE:-flight-booking}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
HOST="${HOST:-}"
FOLDER_ID="${YC_FOLDER_ID:?set YC_FOLDER_ID}"
CLUSTER_ID="${YC_CLUSTER_ID:?set YC_CLUSTER_ID}"

export PATH="${HOME}/yandex-cloud/bin:${HOME}/.local/bin:${PATH}"

command -v kubectl >/dev/null
command -v helm >/dev/null
command -v yc >/dev/null

yc config set folder-id "$FOLDER_ID" >/dev/null
yc managed-kubernetes cluster get-credentials "$CLUSTER_ID" --external --force --folder-id "$FOLDER_ID" >/dev/null

REGISTRY="${YC_REGISTRY:-}"
if [[ -z "$REGISTRY" ]]; then
  REGISTRY_NAME="${YC_REGISTRY_NAME:-flight-booking-registry}"
  REGISTRY_ID=$(yc container registry get "$REGISTRY_NAME" --format json \
    | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
  REGISTRY="cr.yandex/${REGISTRY_ID}"
fi

if [[ -z "$HOST" ]]; then
  EXTERNAL_IP=$(kubectl -n ingress-nginx get svc ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
  [[ -n "$EXTERNAL_IP" ]] || {
    echo "ingress NLB IP not found. >&2
    exit 1
  }
  HOST="${EXTERNAL_IP}.nip.io"
fi

echo "==> deploy apps ns=${NAMESPACE} host=${HOST} tag=${IMAGE_TAG}"

kubectl -n "$NAMESPACE" get deploy postgres kafka >/dev/null \
  || { echo "postgres/kafka missing in ${NAMESPACE}" >&2; exit 1; }

deploy_app() {
  local release="$1" values="$2" repo="$3"
  shift 3
  helm upgrade --install "$release" "$ROOT/charts/microservice" -n "$NAMESPACE" \
    -f "$ROOT/charts/microservice/${values}" \
    --set "image.repository=${REGISTRY}/${repo}" \
    --set "image.tag=${IMAGE_TAG}" \
    --set "image.pullPolicy=Always" \
    --set "ingress.host=${HOST}" \
    --set ingress.className=nginx \
    --set "env.Auth__Issuer=http://${HOST}" \
    "$@" \
    --wait --timeout 8m
}

deploy_app identity-provider values-identity-provider.yaml identity \
  --set "env.Auth__UiLoginUrl=http://${HOST}/login" \
  --set "env.Auth__UiRedirectUrl=http://${HOST}/callback"

deploy_app statistics values-statistics.yaml statistics
deploy_app bonus values-bonus.yaml bonus
deploy_app flights values-flight.yaml flight
deploy_app tickets values-ticket.yaml ticket

deploy_app gateway values-gateway.yaml gateway \
  --set "env.Auth__PublicIdentityProviderUrl=http://${HOST}" \
  --set "env.Auth__CallbackUrl=http://${HOST}/api/v1/callback" \
  --set "env.Auth__UiRedirectUrl=http://${HOST}/callback"

deploy_app ui values-ui.yaml ui

kubectl -n "$NAMESPACE" get pods,ingress
echo "Open: http://${HOST}"
