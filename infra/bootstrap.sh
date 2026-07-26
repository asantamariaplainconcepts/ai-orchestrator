#!/usr/bin/env bash
# Creates the Terraform remote-state backend — the one thing Terraform cannot create for itself.
#
# Idempotent: every step checks before it creates, so running this twice changes nothing. Prints
# the backend configuration to pass to `terraform init` when it finishes.
#
# Run once per subscription, by a human with their own az login (design D2/D7).
set -euo pipefail

LOCATION="${LOCATION:-northeurope}"
STATE_RG="${STATE_RG:-rg-aio-tfstate}"
CONTAINER="${CONTAINER:-tfstate}"

# The storage account name must be globally unique, 3–24 lowercase alphanumerics. Deriving it
# from the subscription id keeps it stable across runs without anyone inventing a name — and
# without the id itself appearing anywhere, since only a hash prefix is used.
subscription_id="$(az account show --query id -o tsv)"
subscription_name="$(az account show --query name -o tsv)"
suffix="$(printf '%s' "$subscription_id" | shasum -a 256 | cut -c1-8)"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-staiotfstate${suffix}}"

echo "Subscription : ${subscription_name}"
echo "Location     : ${LOCATION}"
echo "State RG     : ${STATE_RG}"
echo "Storage      : ${STORAGE_ACCOUNT}"
echo

read -r -p "Create/verify the state backend in this subscription? [y/N] " reply
[[ "${reply}" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }

if [ "$(az group exists --name "${STATE_RG}")" = "true" ]; then
  echo "✓ resource group ${STATE_RG} already exists"
else
  az group create --name "${STATE_RG}" --location "${LOCATION}" --output none
  echo "✓ created resource group ${STATE_RG}"
fi

if az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${STATE_RG}" --output none 2>/dev/null; then
  echo "✓ storage account ${STORAGE_ACCOUNT} already exists"
else
  # Standard_LRS is sufficient for state; versioning is what actually protects it, and
  # blob-level public access is disabled because state carries generated credentials.
  az storage account create \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${STATE_RG}" \
    --location "${LOCATION}" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --min-tls-version TLS1_2 \
    --allow-blob-public-access false \
    --output none
  echo "✓ created storage account ${STORAGE_ACCOUNT}"
fi

az storage account blob-service-properties update \
  --account-name "${STORAGE_ACCOUNT}" \
  --resource-group "${STATE_RG}" \
  --enable-versioning true \
  --output none
echo "✓ blob versioning enabled (state history is recoverable)"

# --auth-mode login uses the caller's identity; the account keys are never fetched or printed.
if az storage container show \
  --name "${CONTAINER}" \
  --account-name "${STORAGE_ACCOUNT}" \
  --auth-mode login \
  --output none 2>/dev/null; then
  echo "✓ container ${CONTAINER} already exists"
else
  az storage container create \
    --name "${CONTAINER}" \
    --account-name "${STORAGE_ACCOUNT}" \
    --auth-mode login \
    --output none
  echo "✓ created container ${CONTAINER}"
fi

cat <<EOF

Backend ready. Initialise Terraform with:

  cd infra/dev
  terraform init \\
    -backend-config="resource_group_name=${STATE_RG}" \\
    -backend-config="storage_account_name=${STORAGE_ACCOUNT}" \\
    -backend-config="container_name=${CONTAINER}" \\
    -backend-config="key=dev.tfstate"

And create infra/dev/terraform.tfvars (gitignored) with:

  subscription_id = "<your subscription id>"
EOF
