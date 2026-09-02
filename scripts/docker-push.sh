#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
[[ -f "$ROOT/scripts/deploy.env" ]] && set -a && source "$ROOT/scripts/deploy.env" && set +a

DEPLOY_TARGET="${DEPLOY_TARGET:-cloud}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

if [[ "$DEPLOY_TARGET" == "minikube" ]]; then
  echo "For minikube use: DEPLOY_TARGET=minikube ./scripts/helm-deploy.sh" >&2
  exit 1
fi

export PATH="${HOME}/yandex-cloud/bin:${HOME}/.local/bin:${PATH}"
command -v yc >/dev/null
command -v docker >/dev/null

REGISTRY="${YC_REGISTRY:-}"
if [[ -z "$REGISTRY" ]]; then
  REGISTRY_NAME="${YC_REGISTRY_NAME:-flight-booking-registry}"
  REGISTRY_ID=$(yc container registry get "$REGISTRY_NAME" --format json | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
  REGISTRY="cr.yandex/${REGISTRY_ID}"
fi

yc container registry configure-docker

if [[ -n "${YC_NODE_SA_ID:-}" ]]; then
  REGISTRY_ID="${REGISTRY#cr.yandex/}"
  yc container registry add-access-binding "$REGISTRY_ID" \
    --role container-registry.images.puller \
    --subject "serviceAccount:${YC_NODE_SA_ID}" 2>/dev/null || true
fi

build_push() {
  local name="$1" dockerfile="$2" context="$3"
  shift 3 || true
  echo "==> push ${REGISTRY}/${name}:${IMAGE_TAG}"
  docker build -t "${REGISTRY}/${name}:${IMAGE_TAG}" -f "$dockerfile" "$context" "$@"
  docker tag "${REGISTRY}/${name}:${IMAGE_TAG}" "${REGISTRY}/${name}:latest"
  docker push "${REGISTRY}/${name}:${IMAGE_TAG}"
  docker push "${REGISTRY}/${name}:latest"
}

build_push identity "$ROOT/src/IdentityProvider/Dockerfile" "$ROOT/src"
build_push statistics "$ROOT/src/StatisticsService/Dockerfile" "$ROOT/src"
build_push bonus "$ROOT/src/BonusService/Dockerfile" "$ROOT/src"
build_push flight "$ROOT/src/FlightService/Dockerfile" "$ROOT/src"
build_push ticket "$ROOT/src/TicketService/Dockerfile" "$ROOT/src"
build_push gateway "$ROOT/src/ApiGateway/Dockerfile" "$ROOT/src"
build_push ui "$ROOT/ui/Dockerfile" "$ROOT/ui" --build-arg NGINX_CONF=nginx.k8s.conf

echo "==> pushed to ${REGISTRY} tag ${IMAGE_TAG}"
