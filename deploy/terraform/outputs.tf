output "resource_group" {
  value = azurerm_resource_group.this.name
}

output "aks_name" {
  value = azurerm_kubernetes_cluster.this.name
}

output "get_credentials" {
  description = "Run this to point kubectl at the cluster"
  value       = "az aks get-credentials -g ${azurerm_resource_group.this.name} -n ${azurerm_kubernetes_cluster.this.name}"
}

output "acr_login_server" {
  value = azurerm_container_registry.this.login_server
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.this.fqdn
}

output "postgres_connection_string" {
  description = "Goes into the Kubernetes secret safar-secrets as ConnectionStrings__Postgres"
  value       = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Database=${azurerm_postgresql_flexible_server_database.safar.name};Username=${azurerm_postgresql_flexible_server.this.administrator_login};Password=${random_password.postgres.result};SSL Mode=Require"
  sensitive   = true
}
