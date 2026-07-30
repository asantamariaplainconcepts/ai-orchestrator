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

  # The sign-in secret rides as a vault REFERENCE (#12): the workload identity reads it at
  # startup, so no value passes through Terraform state or this file. Created by entra-app.sh,
  # deliberately outside Terraform — a directory bootstrap, like the deploy identity (DEC-046).
  dynamic "secret" {
    for_each = var.entra_client_id == "" ? [] : [1]
    content {
      name                = "entra-client-secret"
      identity            = azurerm_user_assigned_identity.workload.id
      key_vault_secret_id = "${azurerm_key_vault.main.vault_uri}secrets/entra-client-secret"
    }
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
      # The portal is the dispatch producer (#17): matching enqueues Run ids. URI form — the
      # workload identity supplies the credential, so no key exists to leak (BR-010).
      env {
        name  = "ConnectionStrings__queues"
        value = "https://${azurerm_storage_account.dispatch.name}.queue.core.windows.net/"
      }
      # DefaultAzureCredential must be told which user-assigned identity to use; with more than
      # one available it will not guess, and the app would fail to reach the vault at runtime.
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.workload.client_id
      }
      # Sign-in (#12): present only when the registration is named, because presence of
      # AzureAd__ClientId is exactly what turns the provider mode on in the Server. Ids are
      # plain values — they identify the app, they do not authenticate it (DEC-058).
      dynamic "env" {
        for_each = var.entra_client_id == "" ? {} : {
          AzureAd__TenantId = var.entra_tenant_id
          AzureAd__ClientId = var.entra_client_id
        }
        content {
          name  = env.key
          value = env.value
        }
      }
      dynamic "env" {
        for_each = var.entra_client_id == "" ? [] : [1]
        content {
          name        = "AzureAd__ClientSecret"
          secret_name = "entra-client-secret"
        }
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
