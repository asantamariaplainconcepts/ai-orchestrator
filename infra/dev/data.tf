# Observability, registry, and the Container Apps environment the portal and the migration job
# both run in.

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  # Dev logs age out quickly; 30 is the minimum the service allows.
  retention_in_days = 30
  tags              = local.tags
}

resource "azurerm_container_registry" "main" {
  # Registry names allow no hyphens, so the prefix is flattened rather than renamed.
  name                = replace("cr${local.prefix}${local.unique}", "-", "")
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  # The portal pulls with its managed identity (AcrPull); no admin credential exists to leak.
  admin_enabled = false
  tags          = local.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.prefix}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags

  # Workload profiles, not Consumption-only — because dynamic sessions require it (#200):
  # `EnvironmentTypeInvalid: Session does not support 'Consumption only' environments`. Azure does
  # not convert an environment's type in place, so **adding this block replaces the environment**,
  # and with it the portal and both jobs, which take their environment id from here. The data
  # survives — Postgres, Key Vault, the registry, the storage account and the Data Protection key
  # ring are all separate resources — but the portal's hostname changes, because it is derived from
  # the environment's default domain.
  #
  # The profile is Consumption, so per-app billing is unchanged: what changes is the environment's
  # *type*, which is what the session pool checks. A dedicated profile would be a second standing
  # cost on top of DEC-063's warm session, and nothing has asked for one.
  #
  # After a replacement, re-run `entra-app.sh` with the new origin. Entra matches redirect URIs to
  # the character, so sign-in fails until it is registered against the new hostname — which is the
  # kind of breakage that looks like a bug in the portal rather than a consequence of a deploy.
  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

# ---- PostgreSQL -----------------------------------------------------------------------------

resource "random_password" "postgres" {
  length = 32
  # Npgsql connection strings and shell interpolation both mishandle some punctuation; this set
  # is safe in each and still yields ample entropy at 32 characters.
  override_special = "!#%*-_=+"
  special          = true
  min_lower        = 1
  min_upper        = 1
  min_numeric      = 1
  min_special      = 1
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                = "psql-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  version                = "16"
  sku_name               = var.postgres_sku
  storage_mb             = var.postgres_storage_mb
  administrator_login    = var.postgres_admin_username
  administrator_password = random_password.postgres.result

  # Public endpoint with the Azure-services rule below, not VNet integration: a private
  # endpoint needs a VNet and a private DNS zone, which is real complexity to carry for a dev
  # environment. Recorded as a deliberate dev-only stance — production revisits it.
  public_network_access_enabled = true
  zone                          = "1"

  backup_retention_days = 7
  tags                  = local.tags

  lifecycle {
    # The generated password lives in Key Vault; rotating it is a deliberate act, not a
    # side effect of an unrelated apply.
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = "aiorchestratordb"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# Container Apps egresses from shared Azure address space; 0.0.0.0 is the documented rule for
# "Azure services", not the public internet. Narrowing this to the environment's static outbound
# IPs is the obvious hardening step when the environment stops being disposable.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
