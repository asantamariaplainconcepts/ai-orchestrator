#!/usr/bin/env bash
# Creates the Entra ID app registration the portal signs users in with, and its service principal.
#
# A script rather than Terraform, for the reason ci-identity.sh is one (DEC-046): this creates a
# **directory** object, not a subscription resource. Terraform-managing it would mean granting the
# CI deploy identity Graph permissions with admin consent — widening what a pipeline can do inside
# the tenant, which is a larger blast radius than the resource group it manages today. Directory
# objects are bootstrapped by a human; subscription resources are Terraform's.
#
# Idempotent: every step checks before it creates, so running it twice changes nothing.
#
# Deliberately minimal. It answers OPN-002's first half — *can an app registration be created in
# this tenant* — and stops there. How the SPA and the API actually authenticate (scopes, audience,
# token validation) is the auth slice's decision (#12, UC-001), not this script's.
set -euo pipefail

APP_NAME="${APP_NAME:-ai-orchestrator-dev}"
# Where the portal runs. Both, because a SPA that only works deployed cannot be developed.
REDIRECT_LOCAL="${REDIRECT_LOCAL:-http://localhost:5173}"
REDIRECT_DEPLOYED="${REDIRECT_DEPLOYED:-}"
REPO="${REPO:-asantamariaplainconcepts/ai-orchestrator}"

need() { command -v "$1" >/dev/null || { echo "Missing required tool: $1" >&2; exit 1; }; }
need az

az account show --output none 2>/dev/null || { echo "Not logged in: run 'az login'." >&2; exit 1; }

tenant_id="$(az account show --query tenantId -o tsv)"
tenant_domain="$(az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/organization?\$select=verifiedDomains" \
  --query "value[0].verifiedDomains[?isDefault].name | [0]" -o tsv 2>/dev/null || echo "unknown")"

# No subscription hash guard here, unlike ci-identity.sh: an app registration is tenant-scoped, and
# the subscription is irrelevant to it. The tenant is what must be right, so the tenant is what gets
# confirmed — by eye, because there is no expected value to compare against yet.
cat <<EOF
Tenant     : ${tenant_domain}
App        : ${APP_NAME}
Redirects  : ${REDIRECT_LOCAL}${REDIRECT_DEPLOYED:+ , ${REDIRECT_DEPLOYED}}
Audience   : this tenant only (AzureADMyOrg)

This creates a directory object in the tenant above. It grants nobody anything yet:
an app registration with no API permissions and no client secret can sign a user in
and nothing else. Implicit flow stays off — auth code with PKCE needs no id_token
issuance, and leaving it off is one fewer flow to have to reason about later.
EOF

read -r -p "Create/verify the app registration? [y/N] " reply
[[ "${reply}" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }

app_id="$(az ad app list --display-name "${APP_NAME}" --query "[0].appId" -o tsv)"
if [ -n "${app_id}" ]; then
  echo "✓ app registration ${APP_NAME} already exists"
else
  app_id="$(az ad app create \
    --display-name "${APP_NAME}" \
    --sign-in-audience AzureADMyOrg \
    --query appId -o tsv)"
  echo "✓ created app registration ${APP_NAME}"
fi

object_id="$(az ad app show --id "${app_id}" --query id -o tsv)"

# `az ad app create` has --web-redirect-uris and --public-client-redirect-uris, and no SPA flag —
# checked against az 2.82, not assumed. A SPA's redirect URIs live under `spa.redirectUris`, which
# only Graph will set, so this is a PATCH rather than a CLI argument.
uris="\"${REDIRECT_LOCAL}\""
[ -n "${REDIRECT_DEPLOYED}" ] && uris="${uris},\"${REDIRECT_DEPLOYED}\""

current="$(az ad app show --id "${app_id}" --query "spa.redirectUris" -o json)"
if [ "$(printf '%s' "${current}" | tr -d ' \n')" = "[${uris}]" ]; then
  echo "✓ SPA redirect URIs already set"
else
  az rest --method PATCH \
    --url "https://graph.microsoft.com/v1.0/applications/${object_id}" \
    --headers "Content-Type=application/json" \
    --body "{\"spa\":{\"redirectUris\":[${uris}]}}"
  echo "✓ set SPA redirect URIs"
fi

if az ad sp show --id "${app_id}" --output none 2>/dev/null; then
  echo "✓ service principal already exists"
else
  az ad sp create --id "${app_id}" --output none
  echo "✓ created service principal"
fi

# Printed, not piped into a secret: a SPA's client id and tenant id are delivered to every browser
# that loads the app. Treating them as secrets would be theatre, and would make them harder to
# configure than they deserve. The subscription id — which this script never touches — is the one
# that stays out of a public repository.
cat <<EOF

Done. Configure the portal with:

  AZURE_CLIENT_ID = ${app_id}
  AZURE_TENANT_ID = ${tenant_id}

Both are public by nature — they ship inside the browser bundle of any SPA that uses them.

OPN-002's first half is now answered for real: the registration exists. Its second half — a
local-dev and functional-test strategy, since Entra cannot be containerized — is still open,
and #11 is where the outcome of both gets recorded.
EOF
