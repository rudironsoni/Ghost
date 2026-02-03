output "server_id" {
  description = "The ID of the Hetzner server"
  value       = hcloud_server.ghost_platform.id
}

output "server_ip" {
  description = "The public IPv4 address of the server"
  value       = hcloud_server.ghost_platform.ipv4_address
}

output "server_ipv6" {
  description = "The public IPv6 address of the server"
  value       = hcloud_server.ghost_platform.ipv6_address
}

output "ssh_connection_string" {
  description = "SSH connection string for the server"
  value       = "ssh root@${hcloud_server.ghost_platform.ipv4_address}"
}

output "created_at" {
  description = "Timestamp when the server was created"
  value       = hcloud_server.ghost_platform.created_at
}

output "server_name" {
  description = "Name of the server"
  value       = hcloud_server.ghost_platform.name
}

output "server_type" {
  description = "Type of the server"
  value       = hcloud_server.ghost_platform.server_type
}

output "location" {
  description = "Location of the server"
  value       = hcloud_server.ghost_platform.location
}
