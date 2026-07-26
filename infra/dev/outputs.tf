# Outputs are the deploy script's inputs. Every value here is deliberately non-sensitive:
# names, URIs and hostnames. Credentials stay in the vault and are never surfaced by
# `terraform output` — task 2.4 verifies that by reading the output set back.

output "resource_group_name" {
  description = "Resource group holding the environment."
  value       = azurerm_resource_group.main.name
}

output "registry_login_server" {
  description = "ACR login server — the image prefix for docker push."
  value       = azurerm_container_registry.main.login_server
}

output "registry_name" {
  description = "ACR name, for `az acr login`."
  value       = azurerm_container_registry.main.name
}

output "portal_app_name" {
  description = "Container app to update with a new image revision."
  value       = azurerm_container_app.portal.name
}

output "portal_url" {
  description = "Public URL of the portal."
  value       = "https://${azurerm_container_app.portal.ingress[0].fqdn}"
}

output "migration_job_name" {
  description = "Container app job the deploy script starts and waits on."
  value       = azurerm_container_app_job.migrations.name
}

output "key_vault_uri" {
  description = "Vault URI handed to the app; secret *names* resolve through it at use."
  value       = azurerm_key_vault.main.vault_uri
}

output "postgres_fqdn" {
  description = "Database host. The credential lives in the vault, not here."
  value       = azurerm_postgresql_flexible_server.main.fqdn
}

output "dispatch_job_name" {
  description = "KEDA-scaled job that drains the dispatch queue."
  value       = azurerm_container_app_job.dispatch.name
}

output "dispatch_queue_account" {
  description = "Storage account holding the dispatch queue."
  value       = azurerm_storage_account.dispatch.name
}

output "dispatch_queue_name" {
  description = "Queue the worker drains and the scaler watches."
  value       = azurerm_storage_queue.dispatch.name
}
