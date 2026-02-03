output "user_data" {
  description = "Cloud-init configuration for Docker installation"
  value       = file("${path.module}/cloud-init.yaml")
}
