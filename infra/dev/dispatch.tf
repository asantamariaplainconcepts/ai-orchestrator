# The dispatch substrate: a queue, an identity for the jobs that drain it, and the KEDA-scaled
# job itself.

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

resource "azurerm_storage_queue" "dispatch" {
  # Must match DispatchQueue.Name in the code and the KEDA scale rule below. Three places, one
  # value — the scale rule silently never fires if they drift.
  name               = "run-dispatch"
  storage_account_id = azurerm_storage_account.dispatch.id
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

# Data-plane access to the queue. "Storage Account Contributor" would also work and is what many
# samples reach for; it is management-plane and far too much — this identity needs to read and
# delete messages, not administer the account.
# The portal enqueues (matching, #17). Message Sender alone will not do: IRunDispatcher's
# contract includes ensuring the queue exists (CreateIfNotExists), and that PUT needs
# queues/write — a sender-only identity gets 403 on the probe even when the queue is there.
resource "azurerm_role_assignment" "portal_queue_data" {
  scope                = azurerm_storage_account.dispatch.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

resource "azurerm_role_assignment" "dispatch_queue_data" {
  scope                = azurerm_storage_account.dispatch.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_user_assigned_identity.dispatch.principal_id
}

# KEDA's scaler authenticates separately from the workload: it polls queue length from outside
# the container, so it needs its own read on the account.
resource "azurerm_role_assignment" "dispatch_queue_scaler" {
  scope                = azurerm_storage_account.dispatch.id
  role_definition_name = "Storage Queue Data Reader"
  principal_id         = azurerm_user_assigned_identity.dispatch.principal_id
}

# The human running Terraform also needs data-plane access, for the same reason they need
# Secrets Officer on the vault: exercising the substrate by hand is how it gets verified at all.
# Owning the subscription grants neither — data-plane RBAC is separate from management-plane, and
# the first attempt to enqueue a test message failed on precisely this.
resource "azurerm_role_assignment" "operator_queue_data" {
  scope                = azurerm_storage_account.dispatch.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ---- The job ----------------------------------------------------------------------------------

resource "azurerm_container_app_job" "dispatch" {
  name                         = "caj-${local.prefix}-dispatch"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  container_app_environment_id = azurerm_container_app_environment.main.id
  tags                         = local.tags

  # THREE SITES, ONE CONTRACT (#144, DEC-054). This must be at least the phase-timeout ceiling plus
  # a drain margin. The other two are:
  #   * CreateAutomation.MaximumTimeoutMinutes (60) — the ceiling an Admin cannot exceed
  #   * BR-005 in docs/product/mvp/05-business-rules.md — the rule both serve
  # No test can span a Terraform value, a C# constant and a written rule, so each names the others.
  #
  # 60 min ceiling + 5 min drain = 3900s. The drain margin is not padding: a worker needs time after
  # a phase ends to write the outcome, flush the log and acknowledge the message, and a budget equal
  # to the phase timeout kills it mid-write — which is the failure this whole change is about.
  #
  # Ten minutes was the previous value, and it silently overrode BR-005's 30-minute default: every
  # implement Run over ten minutes was killed by the platform rather than by its own budget.
  replica_timeout_in_seconds = 3900

  # Zero retries, deliberately. A retried replica would re-read the queue and could pick up a
  # *different* Run — and BR-004 forbids re-running the one it dropped anyway.
  replica_retry_limit = 0

  event_trigger_config {
    parallelism              = 1
    replica_completion_count = 1

    scale {
      min_executions              = 0
      max_executions              = 3
      polling_interval_in_seconds = 30

      rules {
        name             = "queue-length"
        custom_rule_type = "azure-queue"

        metadata = {
          queueName   = azurerm_storage_queue.dispatch.name
          queueLength = "1"
          accountName = azurerm_storage_account.dispatch.name
        }

        # A connection string, not the identity — and not by choice.
        #
        # The azure-queue scaler on Container Apps *jobs* does not support managed identity: KEDA
        # answers "error parsing azure queue metadata: no connection setting given" and the rule
        # silently never fires. Microsoft's own event-driven jobs tutorial uses
        # `--scale-rule-auth "connection=<secret>"` for exactly this reason. Two earlier shapes
        # were tried and both were accepted by ARM, reported healthy, and did nothing.
        #
        # The key is confined to the scaler. It is stored in Key Vault, referenced by the job as a
        # secret (never a literal), and the *workload* still reaches the queue with its managed
        # identity — so no credential is in configuration and only the scaler holds one at all.
        authentication {
          trigger_parameter = "connection"
          secret_name       = "queue-connection"
        }
      }
    }
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.dispatch.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.dispatch.id
  }

  secret {
    name                = "queue-connection"
    identity            = azurerm_user_assigned_identity.dispatch.id
    key_vault_secret_id = azurerm_key_vault_secret.dispatch_queue_connection.id
  }

  template {
    container {
      name   = "dispatch"
      image  = var.dispatch_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ConnectionStrings__queues"
        value = "https://${azurerm_storage_account.dispatch.name}.queue.core.windows.net/"
      }
      # The worker needs the database as much as the portal does: claiming a Run reads the queue,
      # but executing it is entirely database work. Without this the job claimed messages, failed
      # to build its DbContext, and exited zero — Runs orphaned in Queued with the job reporting
      # success (#90).
      env {
        name  = "ConnectionStrings__aiorchestratordbSecretName"
        value = azurerm_key_vault_secret.database_connection_string.name
      }
      env {
        name  = "Secrets__KeyVaultUri"
        value = azurerm_key_vault.main.vault_uri
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.dispatch.client_id
      }
    }
  }

  depends_on = [
    azurerm_role_assignment.dispatch_acr_pull,
    azurerm_role_assignment.dispatch_vault_read,
    azurerm_role_assignment.dispatch_queue_data,
    azurerm_role_assignment.dispatch_queue_scaler,
  ]

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}

# The scaler's credential, in the vault rather than in the job definition. The workload does not
# use it — it authenticates with the identity — so this exists solely because KEDA's azure-queue
# scaler cannot.
resource "azurerm_key_vault_secret" "dispatch_queue_connection" {
  name         = "dispatch-queue-connection"
  value        = azurerm_storage_account.dispatch.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_secrets]
}
