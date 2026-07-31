# The conversation session host (#166, design D2).
#
# Azure Container Apps **dynamic sessions**: a pool declared once, sessions addressed by identifier,
# each its own container, and the platform reclaiming them after inactivity. The conversation's id is
# the identifier, so one conversation is one container is one project's PAT — the isolation boundary
# coincides with the credential boundary (DEC-030) — and the portal creates nothing in ARM: it calls
# the pool's data plane with its own managed identity.
#
# **Why azapi and not azurerm.** The azurerm provider models no session pool. That was checked
# against the provider's own schema rather than its documentation, and it is the whole reason this
# one resource is written in raw ARM while everything else here is not. When azurerm grows the
# resource, this becomes an ordinary block and the escape hatch goes.
#
# **What this revises.** ADR-0008 said nothing idles. A container warm for the length of a
# conversation does, between one message and the next — recorded as DEC-061 rather than left as a
# contradiction, and bounded by the cooldown below rather than by hope.

# ---- The session's own identity, separate again -----------------------------------------------
# A session clones repositories with project PATs, exactly as a dispatch job does, so it gets the
# dispatch identity's entitlements rather than the portal's. The portal must not gain the ability to
# read a project credential just because it can start a conversation.

resource "azurerm_role_assignment" "session_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.dispatch.principal_id
}

# ---- The pool ----------------------------------------------------------------------------------

resource "azapi_resource" "conversation_sessions" {
  type      = "Microsoft.App/sessionPools@2025-01-01"
  name      = "sp-${local.prefix}-conversations"
  location  = azurerm_resource_group.main.location
  parent_id = azurerm_resource_group.main.id
  tags      = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.dispatch.id]
  }

  body = {
    properties = {
      environmentId      = azurerm_container_app_environment.main.id
      poolManagementType = "Dynamic"
      containerType      = "CustomContainer"

      scaleConfiguration = {
        # Ready sessions cost money while nobody is talking, so there are none: the first message of
        # a conversation pays the start (~10s, and it is the only non-instant reply), and every later
        # one is warm. That is the trade ADR-0008's revision is about.
        maxConcurrentSessions = 20
        readySessionInstances = 0
      }

      dynamicPoolConfiguration = {
        # Reclaimed on inactivity, not on a fixed clock: a conversation somebody is still using keeps
        # its container, and one nobody has touched for ten minutes stops costing anything. This is
        # what bounds the idling DEC-061 accepts.
        lifecycleConfiguration = {
          lifecycleType           = "Timed"
          cooldownPeriodInSeconds = 600
        }
      }

      customContainerTemplate = {
        registryCredentials = {
          server   = azurerm_container_registry.main.login_server
          identity = azurerm_user_assigned_identity.dispatch.id
        }

        containers = [
          {
            name = "conversation-session"
            # Placeholder until the first deploy pushes a real tag, exactly as the jobs do: the image
            # is rolled by deploy.sh, and pinning a tag here would make Terraform and the deploy
            # script disagree about which one is current.
            image = var.session_image
            resources = {
              cpu    = 1.0
              memory = "2Gi"
            }
            env = [
              {
                name  = "AZURE_CLIENT_ID"
                value = azurerm_user_assigned_identity.dispatch.client_id
              },
              {
                name  = "Secrets__KeyVaultUri"
                value = azurerm_key_vault.main.vault_uri
              },
            ]
          }
        ]

        ingress = {
          targetPort = 8080
        }
      }

      sessionNetworkConfiguration = {
        # The agent clones a repository and calls a model, both of which are the internet.
        status = "EgressEnabled"
      }
    }
  }

  # The endpoint the portal calls. azapi returns nothing by default, so what is needed is asked for
  # by name — and the portal's configuration reads it from here rather than from a hand-built URL.
  response_export_values = ["properties.poolManagementEndpoint"]

  depends_on = [
    azurerm_role_assignment.session_acr_pull,
    azurerm_role_assignment.dispatch_vault_read,
  ]
}

# ---- The portal's permission to start one ------------------------------------------------------
# Executor, not Contributor: the portal creates sessions in an existing pool and manages no
# infrastructure. It never gains the ability to read a project's credential — that belongs to the
# session's own identity, on the other side of the boundary.

resource "azurerm_role_assignment" "portal_session_executor" {
  scope                = azapi_resource.conversation_sessions.id
  role_definition_name = "Azure ContainerApps Session Executor"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}
