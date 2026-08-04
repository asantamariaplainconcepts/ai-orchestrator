#!/usr/bin/env bash
# Deploy the portal to the dev environment.
#
# The ordering is the point (design D6): push images, run the migration job, wait for it to
# succeed, and only then move the app revision. A failed migration leaves the previous revision
# serving — the schema never changes underneath a running app, and a broken migration never
# takes the site down with it.
#
# Called two ways: by a human with their own az login, and by deploy.yml after a reviewer
# approves the run (DEC-046). Both get the same ordering because both call this script.
set -euo pipefail

TAG="${TAG:-$(git rev-parse --short HEAD)}"
TF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/dev" && pwd)"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

need() { command -v "$1" >/dev/null || { echo "Missing required tool: $1" >&2; exit 1; }; }
need az
need docker
need terraform
# The product images are SDK-published and the SPA is built here first (#260).
need dotnet
need pnpm

# Every name comes from Terraform state — nothing here is hardcoded or guessed.
tf() { terraform -chdir="${TF_DIR}" output -raw "$1"; }

RESOURCE_GROUP="$(tf resource_group_name)"
REGISTRY="$(tf registry_login_server)"
REGISTRY_NAME="$(tf registry_name)"
PORTAL_APP="$(tf portal_app_name)"
MIGRATION_JOB="$(tf migration_job_name)"
DISPATCH_JOB="$(tf dispatch_job_name)"
SESSION_POOL="$(tf conversation_session_pool_name)"
PORTAL_URL="$(tf portal_url)"

PORTAL_IMAGE="${REGISTRY}/portal:${TAG}"
MIGRATION_IMAGE="${REGISTRY}/migrations:${TAG}"
DISPATCH_IMAGE="${REGISTRY}/dispatch:${TAG}"
# The conversation session (#166). Built, pushed and rolled like the others (#193). It has no
# revision, so there is nothing to roll back to and nothing to wait for — but leaving it out was
# how the pool stayed on the bootstrap placeholder while every other workload moved.
SESSION_IMAGE="${REGISTRY}/conversation-session:${TAG}"

echo "Tag        : ${TAG}"
echo "Registry   : ${REGISTRY}"
echo "Portal     : ${PORTAL_URL}"
echo

echo "→ signing in to the registry"
az acr login --name "${REGISTRY_NAME}" --output none

# The SPA lands in the Server's wwwroot before the portal is published — the same order the
# publish-images workflow and the E2E suite require: a portal with a stale or absent wwwroot is
# the failure CI already caught once.
echo "→ building the SPA into wwwroot"
pnpm -C "${REPO_ROOT}/src/frontend" install --frozen-lockfile
pnpm -C "${REPO_ROOT}/src/frontend" build

# The three product images are SDK-published straight to the registry (#257: no Dockerfile backs
# them, #260: this script learned that one deploy too late). linux-x64 explicitly: Container
# Apps runs amd64, and an Apple-silicon default would publish an arm64 image that pushes fine
# and then fails to start with an exec-format error.
echo "→ publishing images (SDK container publish, linux-x64)"
publish_image() {
  dotnet publish "${REPO_ROOT}/src/root/$1/$1.csproj" \
    -c Release /t:PublishContainer \
    -p:ContainerRuntimeIdentifier=linux-x64 \
    -p:ContainerRegistry="${REGISTRY}" \
    -p:ContainerRepository="$2" \
    -p:ContainerImageTags="${TAG}"
}
publish_image AiOrchestrator.Server portal
publish_image AiOrchestrator.MigrationService migrations
publish_image AiOrchestrator.DispatchWorker dispatch

# The conversation session keeps the one Dockerfile left (#257): it bakes agent CLIs through
# RUN steps the SDK cannot express.
echo "→ building the conversation session image (linux/amd64)"
docker build --platform linux/amd64 \
  -f "${REPO_ROOT}/src/root/AiOrchestrator.ConversationSession/Dockerfile" \
  -t "${SESSION_IMAGE}" "${REPO_ROOT}"

echo "→ pushing"
docker push "${SESSION_IMAGE}"

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

# The worker moves before the app that feeds it. Both run the new code against the just-migrated
# schema, and updating the job first means no window where a new portal enqueues work for a
# worker built against an older schema. This step is the one #92 was missing entirely: the job
# ran whatever image the first terraform apply set, for as long as nobody looked.
echo "→ pointing the dispatch worker at ${TAG}"
az containerapp job update \
  --name "${DISPATCH_JOB}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${DISPATCH_IMAGE}" \
  --output none

# Before the portal, for the same reason the worker moves first: the portal is what starts sessions,
# and a new portal talking to a pool still running the previous image is the window this ordering
# exists to close. A pool has no revision, so this replaces the image for sessions started from now
# on; sessions already running finish on the old one and are reclaimed on cooldown.
echo "→ pointing the session pool at ${TAG}"
az containerapp sessionpool update \
  --name "${SESSION_POOL}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${SESSION_IMAGE}" \
  --output none

echo "→ updating the portal revision"
az containerapp update \
  --name "${PORTAL_APP}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${PORTAL_IMAGE}" \
  --output none

# Assert what is RUNNING, not what was commanded. #92 shipped for days with a stale worker
# because every command returned zero and nobody compared the result to the intent.
echo "→ confirming the running images carry ${TAG}"
running_portal="$(az containerapp show --name "${PORTAL_APP}" --resource-group "${RESOURCE_GROUP}" --query "properties.template.containers[0].image" -o tsv)"
running_dispatch="$(az containerapp job show --name "${DISPATCH_JOB}" --resource-group "${RESOURCE_GROUP}" --query "properties.template.containers[0].image" -o tsv)"
running_session="$(az containerapp sessionpool show --name "${SESSION_POOL}" --resource-group "${RESOURCE_GROUP}" --query "properties.customContainerTemplate.containers[0].image" -o tsv)"

for pair in "portal:${running_portal}" "dispatch:${running_dispatch}" "session:${running_session}"; do
  name="${pair%%:*}"
  image="${pair#*:}"
  case "${image}" in
    *":${TAG}") echo "  ✓ ${name} → ${image}" ;;
    *)
      echo "  ✗ ${name} is running ${image}, not tag ${TAG}" >&2
      exit 1
      ;;
  esac
done

echo
echo "Deployed. Verify with the artifact, not the exit code (ADR-0004):"
printf "  curl -s -o /dev/null -w '%%{http_code}\\n' %s/api/health\n" "${PORTAL_URL}"
echo "  curl -s -X POST ${PORTAL_URL}/api/projects -H 'Content-Type: application/json' -d '{\"name\":\"deploy check\"}'"
