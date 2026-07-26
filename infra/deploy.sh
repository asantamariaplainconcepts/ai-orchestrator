#!/usr/bin/env bash
# Deploy the portal to the dev environment.
#
# The ordering is the point (design D6): push images, run the migration job, wait for it to
# succeed, and only then move the app revision. A failed migration leaves the previous revision
# serving — the schema never changes underneath a running app, and a broken migration never
# takes the site down with it.
#
# Run by a human with their own az login. CI has no credentials by design (design D7).
set -euo pipefail

TAG="${TAG:-$(git rev-parse --short HEAD)}"
TF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/dev" && pwd)"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

need() { command -v "$1" >/dev/null || { echo "Missing required tool: $1" >&2; exit 1; }; }
need az
need docker
need terraform

# Every name comes from Terraform state — nothing here is hardcoded or guessed.
tf() { terraform -chdir="${TF_DIR}" output -raw "$1"; }

RESOURCE_GROUP="$(tf resource_group_name)"
REGISTRY="$(tf registry_login_server)"
REGISTRY_NAME="$(tf registry_name)"
PORTAL_APP="$(tf portal_app_name)"
MIGRATION_JOB="$(tf migration_job_name)"
PORTAL_URL="$(tf portal_url)"

PORTAL_IMAGE="${REGISTRY}/portal:${TAG}"
MIGRATION_IMAGE="${REGISTRY}/migrations:${TAG}"

echo "Tag        : ${TAG}"
echo "Registry   : ${REGISTRY}"
echo "Portal     : ${PORTAL_URL}"
echo

echo "→ signing in to the registry"
az acr login --name "${REGISTRY_NAME}" --output none

# linux/amd64 explicitly: Container Apps runs amd64, and an Apple-silicon default would build an
# arm64 image that pushes fine and then fails to start with an exec-format error.
echo "→ building images (linux/amd64)"
docker build --platform linux/amd64 \
  -f "${REPO_ROOT}/src/root/AiOrchestrator.Server/Dockerfile" \
  -t "${PORTAL_IMAGE}" "${REPO_ROOT}"
docker build --platform linux/amd64 \
  -f "${REPO_ROOT}/src/root/AiOrchestrator.MigrationService/Dockerfile" \
  -t "${MIGRATION_IMAGE}" "${REPO_ROOT}"

echo "→ pushing"
docker push "${PORTAL_IMAGE}"
docker push "${MIGRATION_IMAGE}"

echo "→ pointing the migration job at ${TAG}"
az containerapp job update \
  --name "${MIGRATION_JOB}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${MIGRATION_IMAGE}" \
  --output none

echo "→ running migrations"
execution="$(az containerapp job start \
  --name "${MIGRATION_JOB}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query name -o tsv)"
echo "  execution: ${execution}"

# Poll to a terminal state. Succeeded is the only outcome that continues; anything else stops
# the deploy with the previous revision still serving.
for _ in $(seq 1 60); do
  status="$(az containerapp job execution show \
    --name "${MIGRATION_JOB}" \
    --resource-group "${RESOURCE_GROUP}" \
    --job-execution-name "${execution}" \
    --query properties.status -o tsv 2>/dev/null || echo Unknown)"

  case "${status}" in
    Succeeded)
      echo "  ✓ migrations succeeded"
      break
      ;;
    Failed|Cancelled)
      echo "  ✗ migrations ${status} — the portal was NOT updated and keeps serving the previous revision." >&2
      echo "    Logs: az containerapp job logs show -n ${MIGRATION_JOB} -g ${RESOURCE_GROUP} --container migrations" >&2
      exit 1
      ;;
    *)
      sleep 10
      ;;
  esac
done

if [ "${status:-}" != "Succeeded" ]; then
  echo "  ✗ migrations did not reach a terminal state in 10 minutes — portal NOT updated." >&2
  exit 1
fi

echo "→ updating the portal revision"
az containerapp update \
  --name "${PORTAL_APP}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${PORTAL_IMAGE}" \
  --output none

echo
echo "Deployed. Verify with the artifact, not the exit code (ADR-0004):"
printf "  curl -s -o /dev/null -w '%%{http_code}\\n' %s/api/health\n" "${PORTAL_URL}"
echo "  curl -s -X POST ${PORTAL_URL}/api/projects -H 'Content-Type: application/json' -d '{\"name\":\"deploy check\"}'"
