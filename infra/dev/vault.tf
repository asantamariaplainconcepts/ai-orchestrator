# Key Vault, and the generated credentials that live in it.
#
# The one-way rule (design D4): credentials are written *into* the vault by Terraform, and the
# application receives only the vault URI. Nothing sensitive travels through container app
# configuration, and no human ever copies a password between systems.

resource "azurerm_key_vault" "main" {
  # 24-character cap: "kv-aio-dev-" (11) plus the 8-char suffix leaves room to spare.
  name                = "kv-${local.prefix}-${local.unique}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # RBAC rather than access policies: role assignments are visible in the same place as every
  # other permission, and they are what the portal's managed identity will use.
  rbac_authorization_enabled = true

  # Dev vaults are disposable; the provider's purge_soft_delete_on_destroy pairs with this so a
  # destroy does not leave the name reserved for 7 days.
  soft_delete_retention_days = 7
  purge_protection_enabled   = false

  tags = local.tags
}

# The human running terraform must be able to write the secrets below. Without this the apply
# fails at the first secret with a 403 — RBAC on the vault does not follow from owning it.
resource "azurerm_role_assignment" "terraform_operator_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Role assignments are eventually consistent; writing a secret immediately after granting the
# role races the propagation and fails intermittently. An explicit dependency plus the
# provider's own retries make the ordering deterministic rather than lucky.
resource "azurerm_key_vault_secret" "postgres_password" {
  name         = "postgres-admin-password"
  value        = random_password.postgres.result
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_secrets]
}

# The full connection string, composed once here so the application never assembles credentials
# itself — it resolves this name and connects. This is the secret BR-010 is really about.
resource "azurerm_key_vault_secret" "database_connection_string" {
  name = "aiorchestratordb-connection-string"
  value = join(";", [
    "Host=${azurerm_postgresql_flexible_server.main.fqdn}",
    "Port=5432",
    "Database=${azurerm_postgresql_flexible_server_database.main.name}",
    "Username=${var.postgres_admin_username}",
    "Password=${random_password.postgres.result}",
    "SSL Mode=Require",
    "Trust Server Certificate=true",
  ])
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_secrets]
}
