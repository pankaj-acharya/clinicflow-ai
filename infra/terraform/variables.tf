variable "environment" {
  type    = string
  default = "dev"
}

variable "region" {
  type    = string
  default = "uksouth"
}

variable "container_registry_name" {
  type    = string
  default = "clinicflowaidev"
}

variable "deploy_container_apps" {
  type    = bool
  default = true
}

variable "api_image" {
  type    = string
  default = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
}

variable "gateway_image" {
  type    = string
  default = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
}
