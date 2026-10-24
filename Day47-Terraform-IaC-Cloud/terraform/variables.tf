variable "environment" {
  type        = string
  description = "Deployment environment name (e.g. dev, staging, prod)"
  default     = "prod"
}

variable "location" {
  type        = string
  description = "Azure region for resource deployment"
  default     = "eastus"
}

variable "db_admin_user" {
  type        = string
  description = "PostgreSQL flexible server administrator username"
  default     = "pgadmin"
}

variable "db_admin_password" {
  type        = string
  description = "PostgreSQL flexible server administrator password"
  sensitive   = true
}

variable "replica_count" {
  type        = number
  description = "Container app minimum replica count"
  default     = 2
}
