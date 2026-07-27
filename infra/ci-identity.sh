#!/usr/bin/env bash
# Creates the federated identity that deploy.yml uses, and fills in GitHub's side of it.
#
# Idempotent: every step checks before it creates, so running this twice changes nothing. Run
# once per subscription, by a human with their own az login and gh auth — this is the step that
# grants CI the ability to change infrastructure, so it is deliberately not something CI can do
# for itself (DEC-046).
#
# The subscription id is never printed. It goes from `az` straight into `gh secret set` through a
# pipe, because this repository is public and its Actions logs are public with it.
set -euo pipefail

REPO="${REPO:-asantamariaplainconcepts/ai-orchestrator}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
APP_NAME="${APP_NAME:-github-ai-orchestrator-${ENVIRONMENT}}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-aio-dev}"
STATE_RG="${STATE_RG:-rg-aio-tfstate}"
STATE_CONTAINER="${STATE_CONTAINER:-tfstate}"
STATE_KEY="${STATE_KEY:-${ENVIRONMENT}.tfstate}"

# Matches the guard in infra/dev/variables.tf. Both compare a hash rather than the id, so the
# expected subscription can live in a public repository.
EXPECTED_HASH="8540b85423bbab52e34ab92af6031d6b36ec56f3d9f6aa9d47b4004c3fe8a4f7"

need() { command -v "$1" >/dev/null || { echo "Missing required tool: $1" >&2; exit 1; }; }
need az
need gh

az account show --output none 2>/dev/null || { echo "Not logged in: run 'az login'." >&2; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "Not logged in: run 'gh auth login'." >&2; exit 1; }

subscription_id="$(az account show --query id -o tsv)"
subscription_name="$(az account show --query name -o tsv)"
tenant_id="$(az account show --query tenantId -o tsv)"
actual_hash="$(printf '%s' "${subscription_id}" | shasum -a 256 | cut -d' ' -f1)"

# The same refusal Terraform makes at plan time, made here instead — before anything is created
# rather than after. Granting a deploy identity rights over the wrong subscription is the kind of
# mistake that is quiet until it is expensive.
if [ "${actual_hash}" != "${EXPECTED_HASH}" ]; then
  echo "Wrong subscription: '${subscription_name}' is not the expected one." >&2
  echo "Switch with 'az account set --subscription <name>' and run again." >&2
  exit 1
fi

# Derived, not hardcoded — the same derivation bootstrap.sh uses, so the two cannot drift.
suffix="$(printf '%s' "${subscription_id}" | shasum -a 256 | cut -c1-8)"
STATE_STORAGE="${STATE_STORAGE:-staiotfstate${suffix}}"

cat <<EOF
Subscription : ${subscription_name} (guard matched)
Repository   : ${REPO}
Environment  : ${ENVIRONMENT}
App          : ${APP_NAME}
Grants       : Contributor + User Access Administrator on ${RESOURCE_GROUP}
               Storage Blob Data Contributor on ${STATE_STORAGE}
               Key Vault Secrets Officer on the vault in ${RESOURCE_GROUP}

This lets a GitHub Actions run change that resource group and read its secrets,
including the database password — Terraform manages those secrets, so it reads
them on every refresh. Whether a run must be approved first is decided by the
required reviewers on the ${ENVIRONMENT} environment.
EOF

read -r -p "Create/verify the deploy identity? [y/N] " reply
[[ "${reply}" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }

# --- Azure -------------------------------------------------------------------------------

app_id="$(az ad app list --display-name "${APP_NAME}" --query "[0].appId" -o tsv)"
if [ -n "${app_id}" ]; then
  echo "✓ app registration ${APP_NAME} already exists"
else
  app_id="$(az ad app create --display-name "${APP_NAME}" --query appId -o tsv)"
  echo "✓ created app registration ${APP_NAME}"
fi

if az ad sp show --id "${app_id}" --output none 2>/dev/null; then
  echo "✓ service principal already exists"
else
  az ad sp create --id "${app_id}" --output none
  echo "✓ created service principal"
fi
sp_object_id="$(az ad sp show --id "${app_id}" --query id -o tsv)"

# Ask GitHub what it will actually present rather than assembling the subject by hand. The
# default format now embeds the immutable owner and repository IDs — `repo:owner@123/repo@456`
# — which survives a rename, and which no amount of reading the docs would have told us as
# reliably as the API does.
sub_prefix="$(gh api "repos/${REPO}/actions/oidc/customization/sub" --jq .sub_claim_prefix 2>/dev/null || true)"
[ -n "${sub_prefix}" ] || sub_prefix="repo:${REPO}"

# Scoped to the environment rather than to a branch. That is what makes the approval gate
# load-bearing: GitHub only mints a token for a run a reviewer has already released, so the
# credential cannot be used by an unapproved run at all.
credential_subject="${sub_prefix}:environment:${ENVIRONMENT}"
credential_name="github-${ENVIRONMENT}-environment"

credential_parameters="$(printf '{"name":"%s","issuer":"https://token.actions.githubusercontent.com","subject":"%s","audiences":["api://AzureADTokenExchange"]}' \
  "${credential_name}" "${credential_subject}")"

# Compare the subject, not merely the name. A credential that exists with the wrong subject is
# the failure this script is meant to prevent, and "it is already there" would hide it on every
# subsequent run (ADR-0004).
existing_subject="$(az ad app federated-credential show \
  --id "${app_id}" \
  --federated-credential-id "${credential_name}" \
  --query subject -o tsv 2>/dev/null || true)"

if [ "${existing_subject}" = "${credential_subject}" ]; then
  echo "✓ federated credential matches ${credential_subject}"
elif [ -n "${existing_subject}" ]; then
  az ad app federated-credential update \
    --id "${app_id}" \
    --federated-credential-id "${credential_name}" \
    --parameters "${credential_parameters}" \
    --output none
  echo "✓ corrected federated credential subject"
  echo "    was: ${existing_subject}"
  echo "    now: ${credential_subject}"
else
  az ad app federated-credential create \
    --id "${app_id}" \
    --parameters "${credential_parameters}" \
    --output none
  echo "✓ created federated credential for ${credential_subject}"
fi

# Scoped to the resource group, never the subscription. A deploy identity that can reach
# everything is one compromised workflow away from being able to change everything.
rg_scope="$(az group show --name "${RESOURCE_GROUP}" --query id -o tsv)"
state_scope="$(az storage account show \
  --name "${STATE_STORAGE}" \
  --resource-group "${STATE_RG}" \
  --query id -o tsv)"
vault_scope="$(az keyvault list \
  --resource-group "${RESOURCE_GROUP}" \
  --query "[0].id" -o tsv)"

grant() {
  local role="$1" scope="$2"
  if [ -n "$(az role assignment list \
    --assignee "${sp_object_id}" \
    --role "${role}" \
    --scope "${scope}" \
    --query "[0].id" -o tsv 2>/dev/null)" ]; then
    echo "✓ ${role} already granted"
  else
    az role assignment create \
      --assignee-object-id "${sp_object_id}" \
      --assignee-principal-type ServicePrincipal \
      --role "${role}" \
      --scope "${scope}" \
      --output none
    echo "✓ granted ${role}"
  fi
}

# User Access Administrator is not optional: Terraform creates role assignments of its own
# (AcrPull, Key Vault Secrets User, the queue grant), and handing out a role is itself a
# permission the caller must hold.
grant "Contributor" "${rg_scope}"
grant "User Access Administrator" "${rg_scope}"
grant "Storage Blob Data Contributor" "${state_scope}"

# Key Vault RBAC is a separate plane from Contributor, and Terraform *manages* the secrets in
# this vault — so it reads their values on every refresh, not just when writing them. Without
# this, plan fails 403 before it can propose anything. It also means the deploy identity can
# read the database password: unavoidable while Terraform owns those secrets, and the reason
# the environment gate is worth keeping wherever the data is not disposable.
if [ -n "${vault_scope}" ]; then
  grant "Key Vault Secrets Officer" "${vault_scope}"
else
  echo "! no key vault found in ${RESOURCE_GROUP} — skipping the secrets grant"
  echo "  (expected before the first terraform apply; re-run this script afterwards)"
fi

# --- GitHub ------------------------------------------------------------------------------

# Without a required reviewer the environment is a label, not a gate, and the credential above
# becomes usable unattended — which is the whole thing this design refuses. Existing reviewers
# are left alone: deploy.yml deliberately keeps them out of version control so they can be
# tightened without a commit, and a script that overwrote them would undo that on every run.
existing_reviewers="$(gh api "repos/${REPO}/environments/${ENVIRONMENT}" \
  --jq '[.protection_rules[]?|select(.type=="required_reviewers")|.reviewers[].reviewer.login]|join(", ")' \
  2>/dev/null || true)"

if [ -n "${existing_reviewers}" ]; then
  echo "✓ environment ${ENVIRONMENT} already requires approval from ${existing_reviewers}"
else
  gh api --method PUT "repos/${REPO}/environments/${ENVIRONMENT}" \
    --input - >/dev/null <<EOF
{"reviewers":[{"type":"User","id":$(gh api user --jq .id)}]}
EOF
  echo "✓ environment ${ENVIRONMENT} now requires approval from $(gh api user --jq .login)"
fi

# Repository-level. The workflow is a single job inside the environment, so environment-scoped
# secrets would also work — repository level keeps one place to look, and nothing outside an
# approved run can read them either way, because nothing outside one runs.
gh secret set AZURE_CLIENT_ID --repo "${REPO}" --body "${app_id}"
gh secret set AZURE_TENANT_ID --repo "${REPO}" --body "${tenant_id}"
printf '%s' "${subscription_id}" | gh secret set ARM_SUBSCRIPTION_ID --repo "${REPO}"
gh secret set TF_STATE_STORAGE_ACCOUNT --repo "${REPO}" --body "${STATE_STORAGE}"
echo "✓ secrets set (the subscription id was piped, never printed)"

gh variable set TF_STATE_RESOURCE_GROUP --repo "${REPO}" --body "${STATE_RG}"
gh variable set TF_STATE_CONTAINER --repo "${REPO}" --body "${STATE_CONTAINER}"
gh variable set TF_STATE_KEY --repo "${REPO}" --body "${STATE_KEY}"
echo "✓ variables set"

cat <<EOF

Deploy identity ready. Start a run with:

  gh workflow run "Deploy (dev)" --repo ${REPO} --ref main

The plan job finishes unattended; the deploy job waits for your approval with the plan in the
run summary. Approve it in Actions → the run → Review deployments.

Nothing here has been exercised yet (ADR-0005) — the first run is the test. If it fails in
Initialise on blob authorization, add ARM_USE_OIDC to the Terraform steps' env; if it fails at
az acr login, grant AcrPush on the registry.
EOF
