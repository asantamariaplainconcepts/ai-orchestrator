# The portal container app and the migration job.
#
# Both run as the shared user-assigned identity (see identity.tf for why it cannot be
# system-assigned) and receive the vault URI — never a credential. The job exists as its own
# resource because migrations are a deploy step, never a side effect of the Server starting
# (design D6); deploy.sh runs it and waits for exit 0 before the app revision moves.

resource "azurerm_container_app" "portal" {
  name                         = "ca-${local.prefix}-portal"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.workload.id]
  }

  registry {
    server = azurerm_container_registry.main.login_server
    # The identity must already hold AcrPull when the app is created — the explicit dependency
    # below is what guarantees it, and its absence is what hung the first apply.
    identity = azurerm_user_assigned_identity.workload.id
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
      # DefaultAzureCredential must be told which user-assigned identity to use; with more than
      # one available it will not guess, and the app would fail to reach the vault at runtime.
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.workload.client_id
      }
    }
  }

  depends_on = [
    azurerm_role_assignment.workload_acr_pull,
    azurerm_role_assignment.workload_vault_read,
  ]

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
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.workload.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.workload.id
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
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.workload.client_id
      }
    }
  }

  depends_on = [
    azurerm_role_assignment.workload_acr_pull,
    azurerm_role_assignment.workload_vault_read,
  ]

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}
