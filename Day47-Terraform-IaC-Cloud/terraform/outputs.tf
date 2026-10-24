output "postgres_fqdn" {
  description = "Fully qualified domain name of PostgreSQL flexible server"
  value       = azurerm_postgresql_flexible_server.postgres.fqdn
}

output "container_app_fqdn" {
  description = "Public ingress FQDN for the deployed container app"
  value       = azurerm_container_app.api.latest_revision_fqdn
}

output "database_connection_string" {
  description = "Constructed connection string for PostgreSQL"
  value       = "Host=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=orders;Username=${var.db_admin_user};Password=${var.db_admin_password};SSL Mode=Require;"
  sensitive   = true
}
