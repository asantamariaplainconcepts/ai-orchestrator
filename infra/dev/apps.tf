# The portal container app and the migration job.
#
# Both carry a system-assigned identity and receive the vault URI — never a credential. The
# job exists as its own resource because migrations are a deploy step, never a side effect of
# the Server starting (design D6); deploy.sh runs it and waits for exit 0 before the app
# revision moves.

resource "azurerm_container_app" "portal" {
  name                         = "ca-${local.prefix}-portal"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type = "SystemAssigned"
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = "System"
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    # Scale to zero: a dev portal nobody is using should cost nothing.
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "portal"
      image  = var.portal_image
      cpu    = 0.25
      memory = "0.5Gi"

      # Only non-secrets. Secrets:KeyVaultUri is what makes the host compose the Key Vault
      # resolver; the connection string is resolved by name at use, not injected here.
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "ASPNETCORE_HTTP_PORTS"
        value = "8080"
      }
      env {
        name  = "Secrets__KeyVaultUri"
        value = azurerm_key_vault.main.vault_uri
      }
      env {
        name  = "ConnectionStrings__aiorchestratordbSecretName"
        value = azurerm_key_vault_secret.database_connection_string.name
      }
    }
  }

  lifecycle {
    # deploy.sh owns the image after the first apply; Terraform reverting it to the bootstrap
    # placeholder on an unrelated apply would silently roll the portal back.
    ignore_changes = [template[0].container[0].image]
  }
}

resource "azurerm_container_app_job" "migrations" {
  name                         = "caj-${local.prefix}-migrations"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  container_app_environment_id = azurerm_container_app_environment.main.id
  tags                         = local.tags

  # Manual trigger: the deploy script starts it and waits. Nothing schedules migrations.
  replica_timeout_in_seconds = 600
  replica_retry_limit        = 0

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type = "SystemAssigned"
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = "System"
  }

  template {
    container {
      name   = "migrations"
      image  = var.migration_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "Secrets__KeyVaultUri"
        value = azurerm_key_vault.main.vault_uri
      }
      env {
        name  = "ConnectionStrings__aiorchestratordbSecretName"
        value = azurerm_key_vault_secret.database_connection_string.name
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}

# ---- Identity grants ------------------------------------------------------------------------
# Read-only on the vault, pull-only on the registry. Neither app can write a secret or push an
# image, which is the least privilege each actually needs.

resource "azurerm_role_assignment" "portal_vault_read" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app.portal.identity[0].principal_id
}

resource "azurerm_role_assignment" "portal_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_container_app.portal.identity[0].principal_id
}

resource "azurerm_role_assignment" "migrations_vault_read" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app_job.migrations.identity[0].principal_id
}

resource "azurerm_role_assignment" "migrations_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_container_app_job.migrations.identity[0].principal_id
}
