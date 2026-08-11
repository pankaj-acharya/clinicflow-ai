terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
}

data "azurerm_client_config" "current" {}

provider "azurerm" {
  # resource_provider_registrations = "none" prevents Terraform from registering
  # or unregistering Azure providers (e.g. Microsoft.App). Provider registration
  # is subscription-level and should not be managed per-deployment in a shared
  # dev environment. Without this, destroy triggers an InvalidUnregistration 409
  # when other resources in the subscription still depend on the provider.
  resource_provider_registrations = "none"
  features {}
}
