# What remains of the dispatch substrate after #296. The queue and its KEDA-scaled job retired
# with DEC-013's supersession: Runs dispatch through the Postgres outbox and execute from the
# Server, whose launcher creates a per-Run sandbox. Two residents stay, each for its own reason:
#
# - the STORAGE ACCOUNT, because it also hosts the portal's Data Protection key ring (#180),
#   which has nothing to do with dispatch and everything to do with OIDC surviving scale-to-zero;
# - the IDENTITY, because conversation sessions deliberately run as it (conversations.tf): a
#   session clones repositories with project PATs, and the portal must not gain the ability to
#   read a project credential just because it can start a conversation.

resource "azurerm_storage_account" "dispatch" {
  # Globally unique, same deterministic suffix as the vault and registry (design D8 — the first
  # apply of #8 failed on a name a stranger already held).
  name                = "staio${var.environment}${local.unique}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  # Nothing in a dispatch queue should ever be readable anonymously.
  allow_nested_items_to_be_public = false

  tags = local.tags
}


# ---- Job identity, separate from the portal's (design D3) -------------------------------------
# Agent jobs will clone repositories with project PATs — a far wider blast radius than a web
# host. Sharing the portal's identity would mean a compromise of either reaches both, and would
# grow the portal's entitlements every time an Agent needs something new.

resource "azurerm_user_assigned_identity" "dispatch" {
  name                = "id-${local.prefix}-dispatch"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

resource "azurerm_role_assignment" "dispatch_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.dispatch.principal_id
}

resource "azurerm_role_assignment" "dispatch_vault_read" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.dispatch.principal_id
}





# The Data Protection key ring (#180). Not "just storage": this ring encrypts the OIDC state and
# signs session cookies, and an in-memory ring cannot survive scale-to-zero or a new revision — the
# two things that sit between a sign-in challenge and its callback. Sign-in failed on every real
# attempt with "Unable to unprotect the message.State" until this existed.
resource "azurerm_storage_container" "keyring" {
  name                  = "dataprotection"
  storage_account_id    = azurerm_storage_account.dispatch.id
  container_access_type = "private"
}

# NOT wrapped with a Key Vault key yet, and that is a stated gap rather than an oversight (#183).
# Wrapping would need a key, creating a key needs Crypto Officer, and neither the deploy identity nor
# the operator running the bootstrap holds it — granting CI that role would also let a pipeline delete
# the key that wraps every session. So the ring rides Azure Storage's own encryption at rest, in a
# private container reachable only by the workload identity.
#
# The residual risk, plainly: a principal who can read this container can forge session cookies. Today
# that is the workload identity alone. The code already reads a wrapping key when configured, so
# closing #183 is configuration plus one role grant, not a rewrite.

resource "azurerm_role_assignment" "portal_keyring_blob" {
  scope                = azurerm_storage_account.dispatch.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Crypto User, not Secrets User: wrapping a key is a key operation, and the secret roles the
# identity already holds do not grant it.
resource "azurerm_role_assignment" "portal_keyring_crypto" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}
