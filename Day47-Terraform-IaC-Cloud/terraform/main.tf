provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-microservices-${var.environment}"
  location = var.location
  tags = {
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

resource "azurerm_virtual_network" "vnet" {
  name                = "vnet-microservices-${var.environment}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "subnet_db" {
  name                 = "snet-postgres"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
  delegation {
    name = "fs"
    service_delegation {
      name = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_postgresql_flexible_server" "postgres" {
  name                   = "psql-microservices-${var.environment}"
  resource_group_name    = azurerm_resource_group.rg.name
  location               = azurerm_resource_group.rg.location
  version                = "16"
  delegated_subnet_id    = azurerm_subnet.subnet_db.id
  administrator_login    = var.db_admin_user
  administrator_password = var.db_admin_password
  storage_mb             = 32768
  sku_name               = "GP_Standard_D2s_v3"
  zone                   = "1"

  depends_on = [azurerm_subnet.subnet_db]
}

resource "azurerm_log_analytics_workspace" "logs" {
  name                = "log-microservices-${var.environment}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_container_app_environment" "env" {
  name                       = "cae-microservices-${var.environment}"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
}

resource "azurerm_container_app" "api" {
  name                         = "ca-orders-api-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"

  template {
    min_replicas = var.replica_count
    max_replicas = 10

    container {
      name   = "orders-api"
      image  = "ghcr.io/shatru123/orders-api:v1.0.0"
      cpu    = 0.5
      memory = "1.0Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-conn"
      }
    }
  }

  secret {
    name  = "postgres-conn"
    value = "Host=${azurerm_postgresql_flexible_server.postgres.fqdn};Database=orders;Username=${var.db_admin_user};Password=${var.db_admin_password};SSL Mode=Require;"
  }

  depends_on = [
    azurerm_postgresql_flexible_server.postgres,
    azurerm_container_app_environment.env
  ]
}
