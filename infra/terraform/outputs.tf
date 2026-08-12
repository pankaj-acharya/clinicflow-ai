output "environment" {
  value = var.environment
}

output "region" {
  value = var.region
}

output "resource_group_name" {
  value = azurerm_resource_group.this.name
}

output "container_registry_login_server" {
  value = azurerm_container_registry.this.login_server
}

output "container_registry_id" {
  value = azurerm_container_registry.this.id
}

output "application_insights_connection_string" {
  value     = azurerm_application_insights.this.connection_string
  sensitive = true
}

output "key_vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "api_url" {
  value = try("https://${azurerm_container_app.api[0].ingress[0].fqdn}", null)
}

output "gateway_url" {
  value = try("https://${azurerm_container_app.gateway[0].ingress[0].fqdn}", null)
}

output "managed_identity_client_id" {
  value = azurerm_user_assigned_identity.this.client_id
}

output "managed_identity_principal_id" {
  value = azurerm_user_assigned_identity.this.principal_id
}

output "deployment_principal_object_id" {
  value = data.azurerm_client_config.current.object_id
}
