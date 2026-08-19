locals {
  foundry_resource_group_name   = "${local.project_name}-${var.environment}-foundry-rg"
  foundry_account_name          = lower(replace("${local.project_name}${var.environment}foundry", "-", ""))
  foundry_project_name          = "${local.project_name}-${var.environment}-foundry"
  foundry_model_deployment_name = "${local.project_name}-${var.environment}-model"
  foundry_model_name            = "gpt-4.1-mini"
  foundry_model_version         = "2025-04-14"
}

resource "azurerm_resource_group" "foundry" {
  name     = local.foundry_resource_group_name
  location = var.region
}

resource "azapi_resource" "foundry_account" {
  type      = "Microsoft.CognitiveServices/accounts@2025-06-01"
  name      = local.foundry_account_name
  parent_id = azurerm_resource_group.foundry.id
  location  = var.region

  identity {
    type = "SystemAssigned"
  }

  body = {
    kind = "AIServices"
    properties = {
      allowProjectManagement        = true
      customSubDomainName           = local.foundry_account_name
      disableLocalAuth              = false
      dynamicThrottlingEnabled      = false
      publicNetworkAccess           = "Enabled"
      restrictOutboundNetworkAccess = false
    }
    sku = {
      name = "S0"
    }
  }

  schema_validation_enabled = false
  response_export_values    = ["*"]
}

resource "azapi_resource" "foundry_project" {
  type      = "Microsoft.CognitiveServices/accounts/projects@2025-06-01"
  name      = local.foundry_project_name
  parent_id = azapi_resource.foundry_account.id
  location  = var.region

  identity {
    type = "SystemAssigned"
  }

  body = {
    sku = {
      name = "S0"
    }
    properties = {
      displayName = "ClinicFlow AI ${var.environment} Foundry"
      description = "ClinicFlow AI ${var.environment} Foundry project"
    }
  }

  schema_validation_enabled = false
  response_export_values    = ["*"]
}

resource "azapi_resource" "foundry_model_deployment" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2025-06-01"
  name      = local.foundry_model_deployment_name
  parent_id = azapi_resource.foundry_account.id
  location  = var.region

  depends_on = [
    azapi_resource.foundry_project,
  ]

  body = {
    sku = {
      name     = "Standard"
      capacity = 1
    }
    properties = {
      model = {
        format  = "OpenAI"
        name    = local.foundry_model_name
        version = local.foundry_model_version
      }
      versionUpgradeOption = "OnceNewDefaultVersionAvailable"
      raiPolicyName        = "Microsoft.DefaultV2"
    }
  }

  schema_validation_enabled = false
  response_export_values    = ["*"]
}

resource "azurerm_role_assignment" "foundry_project_manager" {
  scope                = azapi_resource.foundry_project.id
  role_definition_name = "Foundry Project Manager"
  principal_id         = data.azurerm_client_config.current.object_id
}
