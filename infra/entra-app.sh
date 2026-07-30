#!/usr/bin/env bash
# Creates the Entra ID app registration the portal signs users in with, as a CONFIDENTIAL web
# client, and puts its secret in Key Vault without printing it.
#
# A script rather than Terraform, for the reason ci-identity.sh is one (DEC-046): this creates a
# **directory** object, not a subscription resource. Terraform-managing it would mean granting the
# CI deploy identity Graph permissions with admin consent — widening what a pipeline can do inside
# the tenant, a larger blast radius than the resource group it manages today.
#
# **Why a web client and not a SPA.** The portal is a same-origin single web app: the Vite build is
# served from the Server's wwwroot with an index.html fallback, API calls are relative, and there is
# no CORS configuration anywhere (frontend-architecture spec). That shape is already a
# backend-for-frontend, so the session belongs in an HttpOnly cookie on the server and no access
# token ever needs to reach the browser. A public-client SPA flow would put tokens in JavaScript to
# solve a cross-origin problem this product does not have.
#
# Idempotent: every step checks before it creates.
#
# Answers OPN-002's first half — can an app registration be created in this tenant — and stops
# there. Token validation, scopes and role mapping are the auth slice's decisions (#12, #13).
set -euo pipefail

APP_NAME="${APP_NAME:-ai-orchestrator-dev}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-aio-dev}"
# Both origins, because auth that only works deployed cannot be developed against.
#
# The local one is the Server's own dev profile (launchSettings: http://localhost:5080), read from
# there rather than guessed — a redirect URI that does not match to the character fails sign-in with
# no useful message. Plain http is allowed here because Entra exempts localhost specifically; it
# would be rejected for any other host.
LOCAL_ORIGIN="${LOCAL_ORIGIN:-http://localhost:5080}"
DEPLOYED_ORIGIN="${DEPLOYED_ORIGIN:-}"
SECRET_NAME="${SECRET_NAME:-entra-client-secret}"

need() { command -v "$1" >/dev/null || { echo "Missing required tool: $1" >&2; exit 1; }; }
need az

az account show --output none 2>/dev/null || { echo "Not logged in: run 'az login'." >&2; exit 1; }

tenant_id="$(az account show --query tenantId -o tsv)"
tenant_domain="$(az rest --method GET \
  --url "https://graph.microsoft.com/v1.0/organization?\$select=verifiedDomains" \
  --query "value[0].verifiedDomains[?isDefault].name | [0]" -o tsv 2>/dev/null || echo "unknown")"

# /signin-oidc receives the code; /signed-out is where sign-out lands, and it is in this list
# because Entra validates post-logout redirect URIs against the registered redirect URIs.
redirects="${LOCAL_ORIGIN}/signin-oidc ${LOCAL_ORIGIN}/signed-out"
[ -n "${DEPLOYED_ORIGIN}" ] && redirects="${redirects} ${DEPLOYED_ORIGIN}/signin-oidc ${DEPLOYED_ORIGIN}/signed-out"

# No subscription guard, unlike ci-identity.sh: an app registration is tenant-scoped and the
# subscription is irrelevant to it. The tenant is what must be right, so the tenant is what gets
# confirmed — by eye, because there is no expected value to compare against yet.
cat <<EOF
Tenant     : ${tenant_domain}
App        : ${APP_NAME} (confidential web client)
Redirects  : ${redirects}
Audience   : this tenant only (AzureADMyOrg)
Secret     : created and written to Key Vault as '${SECRET_NAME}', never printed

The portal is served same-origin by its own server, so this is a backend-for-frontend:
the browser gets an HttpOnly session cookie and never sees a token. That is why this is
a web client with a secret rather than a public SPA client without one.
EOF

read -r -p "Create/verify the app registration? [y/N] " reply
[[ "${reply}" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }

app_id="$(az ad app list --display-name "${APP_NAME}" --query "[0].appId" -o tsv)"
if [ -n "${app_id}" ]; then
  echo "✓ app registration ${APP_NAME} already exists"
else
  # Implicit flow stays off: the code flow redeems on the server, so no id_token issuance is
  # needed and leaving it off is one fewer flow to reason about.
  # shellcheck disable=SC2086
  app_id="$(az ad app create \
    --display-name "${APP_NAME}" \
    --sign-in-audience AzureADMyOrg \
    --enable-id-token-issuance true \
    --web-redirect-uris ${redirects} \
    --query appId -o tsv)"
  echo "✓ created app registration ${APP_NAME}"
fi

object_id="$(az ad app show --id "${app_id}" --query id -o tsv)"

# Declarative, every run: the create above only fires the first time, and a bootstrap whose
# re-run cannot add the deployed origin is one that silently strands the first environment it
# was run without. The desired list simply overwrites — running twice converges, which is what
# the first real run proved this script previously did not do (#12: the deployed redirect was
# missing because the owner ran it before DEPLOYED_ORIGIN existed).
# Word-splitting is the point here: $redirects is a space-separated list built above, and each
# element becomes one JSON string.
# shellcheck disable=SC2086
uris_json="$(printf '"%s",' ${redirects} | sed 's/,$//')"
az rest --method PATCH \
  --url "https://graph.microsoft.com/v1.0/applications/${object_id}" \
  --headers "Content-Type=application/json" \
  --body "{\"web\":{\"redirectUris\":[${uris_json}],\"implicitGrantSettings\":{\"enableIdTokenIssuance\":true,\"enableAccessTokenIssuance\":false}}}"
echo "✓ redirect URIs and id-token issuance set (declaratively, ${redirects})"

# Front-channel logout, so signing out of Entra also drops the local cookie session.
front_channel="${DEPLOYED_ORIGIN:-${LOCAL_ORIGIN}}/signout-oidc"
current_logout="$(az ad app show --id "${app_id}" --query "web.logoutUrl" -o tsv)"
if [ "${current_logout}" = "${front_channel}" ]; then
  echo "✓ logout URL already set"
else
  az rest --method PATCH \
    --url "https://graph.microsoft.com/v1.0/applications/${object_id}" \
    --headers "Content-Type=application/json" \
    --body "{\"web\":{\"logoutUrl\":\"${front_channel}\"}}"
  echo "✓ set front-channel logout URL"
fi

if az ad sp show --id "${app_id}" --output none 2>/dev/null; then
  echo "✓ service principal already exists"
else
  az ad sp create --id "${app_id}" --output none
  echo "✓ created service principal"
fi

# --- the client secret ------------------------------------------------------------------------
#
# A confidential client needs a credential to redeem the authorization code. The value goes from
# `az ad app credential reset` straight into the vault through a pipe and is never printed, never
# written to a file, and never held in a shell variable that gets echoed — the same discipline
# ci-identity.sh applies to the subscription id, for the same reason: this repository is public and
# so are its Actions logs.
#
# BR-010 is satisfied by construction: what reaches configuration is the secret's NAME.
vault="${VAULT_NAME:-$(az keyvault list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || true)}"
if [ -z "${vault}" ]; then
  echo "No Key Vault found in ${RESOURCE_GROUP}. Set VAULT_NAME, or run bootstrap.sh first." >&2
  exit 1
fi

if az keyvault secret show --vault-name "${vault}" --name "${SECRET_NAME}" --output none 2>/dev/null; then
  echo "✓ ${SECRET_NAME} already in ${vault} — not rotated (rotation is a deliberate act)"
else
  az ad app credential reset --id "${app_id}" --append \
    --display-name "portal-bff" --years 1 --query password -o tsv \
    | az keyvault secret set --vault-name "${vault}" --name "${SECRET_NAME}" \
        --file /dev/stdin --output none
  echo "✓ created a client secret and stored it as ${SECRET_NAME} in ${vault}"
fi

cat <<EOF

Done. Configure the Server with:

  AzureAd__TenantId              = ${tenant_id}
  AzureAd__ClientId              = ${app_id}
  AzureAd__ClientCredentials__0  = the vault reference to '${SECRET_NAME}'

The tenant and client ids are not secrets — they identify the app, they do not authenticate it.
The secret is in the vault and was never printed here.

Two cookie traps, both silent. A cookie marked Secure is not sent over plain http, so the local
profile above needs that relaxed in development or the session simply never arrives. And:
SameSite=Strict is correct for the
application session, because every request that carries it is same-origin. The OIDC handshake
cookies (correlation, nonce) are a different matter — the response arrives from
login.microsoftonline.com, which is cross-site, so Strict would drop them and sign-in would fail
in a way that looks like nothing happened. Leave those at the library's default.

OPN-002's first half is now answered for real. Its second half — a local-dev and functional-test
strategy, since Entra cannot be containerized — the BFF shape answers cheaply: the server owns the
session, so tests keep injecting ICurrentPrincipal and Entra is composed only in the real host.
Record both outcomes on #11.
EOF
