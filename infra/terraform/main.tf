terraform {
  required_version = ">= 1.5.0"

  backend "azurerm" {}
}

locals {
  project_name                 = "clinicflow-ai"
  resource_group_name          = "${local.project_name}-${var.environment}-rg"
  log_analytics_workspace_name = "${local.project_name}-${var.environment}-law"
  application_insights_name    = "${local.project_name}-${var.environment}-appi"
  managed_identity_name        = "${local.project_name}-${var.environment}-uai"
  container_apps_environment   = "${local.project_name}-${var.environment}-cae"
  api_container_app_name       = "${local.project_name}-${var.environment}-api"
  gateway_container_app_name   = "${local.project_name}-${var.environment}-gateway"
  web_container_app_name       = "${local.project_name}-${var.environment}-web"
  key_vault_name               = substr(replace(lower("${local.project_name}${var.environment}kv"), "-", ""), 0, 24)
  postgres_server_name         = "${local.project_name}-${var.environment}-psql"
  postgres_db_name             = "clinicflow"
}

resource "azurerm_resource_group" "this" {
  name     = local.resource_group_name
  location = var.region
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = local.log_analytics_workspace_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_application_insights" "this" {
  name                = local.application_insights_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
}

resource "azurerm_user_assigned_identity" "this" {
  name                = local.managed_identity_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name

}

resource "azurerm_container_registry" "this" {
  name                = var.container_registry_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "Standard"
  admin_enabled       = false
}

resource "azurerm_role_assignment" "acr_push" {
  scope                = azurerm_container_registry.this.id
  role_definition_name = "AcrPush"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "acr_user_access_administrator" {
  scope                = azurerm_container_registry.this.id
  role_definition_name = "User Access Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                            = azurerm_container_registry.this.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_user_assigned_identity.this.principal_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

resource "azurerm_key_vault" "this" {
  name                        = local.key_vault_name
  location                    = azurerm_resource_group.this.location
  resource_group_name         = azurerm_resource_group.this.name
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"
  purge_protection_enabled    = false
  soft_delete_retention_days  = 7
  enabled_for_disk_encryption = true

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

    secret_permissions = [
      "Get",
      "List",
      "Set",
      "Delete",
      "Recover",
      "Backup",
      "Restore",
      "Purge"
    ]
  }

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = azurerm_user_assigned_identity.this.principal_id

    secret_permissions = [
      "Get",
      "List"
    ]
  }
}

resource "azurerm_container_app_environment" "this" {
  name                       = local.container_apps_environment
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
}

resource "azurerm_container_app" "api" {
  count                        = var.deploy_container_apps ? 1 : 0
  name                         = local.api_container_app_name
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  revision_mode                = "Single"
  workload_profile_name        = null

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.this.id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.this.id
  }

  dynamic "secret" {
    for_each = var.deploy_postgres ? [1] : []
    content {
      name                = "clinicflow-postgres-connection-string"
      key_vault_secret_id = azurerm_key_vault_secret.postgres_connection_string[0].versionless_id
      identity            = azurerm_user_assigned_identity.this.id
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Development"
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.this.connection_string
      }

      dynamic "env" {
        for_each = var.deploy_postgres ? [1] : []
        content {
          name        = "ConnectionStrings__ClinicFlowDb"
          secret_name = "clinicflow-postgres-connection-string"
        }
      }
    }
  }
}

resource "azurerm_key_vault_secret" "clinicflow_api_base_url" {
  count        = var.deploy_container_apps ? 1 : 0
  name         = "clinicflow-api-base-url"
  value        = "https://${azurerm_container_app.api[0].ingress[0].fqdn}"
  key_vault_id = azurerm_key_vault.this.id
}


# ---------------------------------------------------------------------------
# Import existing PostgreSQL server into Terraform state if not tracked yet.
# Safe to keep permanently — Terraform skips if already in state.
# ---------------------------------------------------------------------------
import {
  to = azurerm_postgresql_flexible_server.this[0]
  id = "/subscriptions/3e430fb8-73f7-4930-a2ec-645fd80f5661/resourceGroups/clinicflow-ai-dev-rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/clinicflow-ai-dev-psql"
}

# Import the pre-existing firewall rule that was created manually.
# Safe to keep permanently — Terraform skips if already in state.
import {
  to = azurerm_postgresql_flexible_server_firewall_rule.allow_azure_services[0]
  id = "/subscriptions/3e430fb8-73f7-4930-a2ec-645fd80f5661/resourceGroups/clinicflow-ai-dev-rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/clinicflow-ai-dev-psql/firewallRules/allow-azure-services"
}
# ---------------------------------------------------------------------------
# PostgreSQL Flexible Server
# ---------------------------------------------------------------------------

resource "azurerm_postgresql_flexible_server" "this" {
  count               = var.deploy_postgres ? 1 : 0
  name                = local.postgres_server_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  version             = "16"
  administrator_login    = var.postgres_admin_user
  administrator_password = var.postgres_admin_password
  storage_mb          = 32768
  sku_name            = "B_Standard_B1ms"
  zone                = "1"
  backup_retention_days = 7
}

resource "azurerm_postgresql_flexible_server_database" "this" {
  count     = var.deploy_postgres ? 1 : 0
  name      = local.postgres_db_name
  server_id = azurerm_postgresql_flexible_server.this[0].id
  charset   = "utf8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure_services" {
  count            = var.deploy_postgres ? 1 : 0
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.this[0].id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}


resource "azurerm_key_vault_secret" "postgres_connection_string" {
  count        = (var.deploy_postgres && var.deploy_container_apps) ? 1 : 0
  name         = "clinicflow-postgres-connection-string"
  value        = "Host=${azurerm_postgresql_flexible_server.this[0].fqdn};Database=${local.postgres_db_name};Username=${var.postgres_admin_user};Password=${var.postgres_admin_password};SslMode=Require"
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_container_app" "gateway" {
  count                        = var.deploy_container_apps ? 1 : 0
  name                         = local.gateway_container_app_name
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.this.id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.this.id
  }

  secret {
    name                = "clinicflow-api-base-url"
    key_vault_secret_id = azurerm_key_vault_secret.clinicflow_api_base_url[0].versionless_id
    identity            = azurerm_user_assigned_identity.this.id
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "gateway"
      image  = var.gateway_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Development"
      }

      env {
        name        = "ClinicFlowApi__BaseUrl"
        secret_name = "clinicflow-api-base-url"
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.this.connection_string
      }
    }
  }
}

resource "azurerm_container_app" "web" {
  count                        = var.deploy_container_apps ? 1 : 0
  name                         = local.web_container_app_name
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.this.id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.this.id
  }

  secret {
    name                = "clinicflow-api-base-url"
    key_vault_secret_id = azurerm_key_vault_secret.clinicflow_api_base_url[0].versionless_id
    identity            = azurerm_user_assigned_identity.this.id
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Development"
      }

      env {
        name        = "ClinicFlowApi__BaseUrl"
        secret_name = "clinicflow-api-base-url"
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.this.connection_string
      }
    }
  }
}
