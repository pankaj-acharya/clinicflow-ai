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

variable "deploy_postgres" {
  description = "Whether to provision Azure PostgreSQL Flexible Server."
  type        = bool
  default     = true
}

variable "postgres_admin_user" {
  description = "PostgreSQL administrator login."
  type        = string
  default     = "clinicadmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password. Store in CI secrets, never in tfvars."
  type        = string
  sensitive   = true
  default     = ""
}
