# One user-assigned identity for both workloads, created and granted **before** either app.
#
# Not a stylistic choice — a system-assigned identity cannot work here, and the first apply
# proved it by hanging. Container Apps validates the registry credential while it creates the
# app, but a system-assigned principal only exists *after* the app is created, so the AcrPull
# grant cannot precede the thing that needs it. The app waits forever on a permission Terraform
# is waiting for the app to enable. A user-assigned identity has no such cycle: it exists first,
# gets its roles, and the apps reference something already entitled.
#
# Sharing one identity between the portal and the migration job is deliberate: they need exactly
# the same two permissions, and two identities would be two things to keep in step.

resource "azurerm_user_assigned_identity" "workload" {
  name                = "id-${local.prefix}-workload"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

resource "azurerm_role_assignment" "workload_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

resource "azurerm_role_assignment" "workload_vault_read" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Reading and writing are two assignments, mirroring the two seams the application depends on
# (#124): ISecretResolver reads, ISecretStore writes, and nothing holds both by accident. Only
# the Server gets this one — the dispatch identity resolves credentials and never stores one,
# which is a property of its role assignments and not only of its code.
resource "azurerm_role_assignment" "workload_vault_write" {
  scope              = azurerm_key_vault.main.id
  role_definition_id = azurerm_role_definition.secret_writer.role_definition_resource_id
  principal_id       = azurerm_user_assigned_identity.workload.principal_id
}
