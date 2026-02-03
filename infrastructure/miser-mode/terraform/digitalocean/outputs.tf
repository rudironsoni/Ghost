output "server_id" {
  description = "The ID of the DigitalOcean droplet"
  value       = digitalocean_droplet.ghost_platform.id
}

output "server_ip" {
  description = "The public IPv4 address of the droplet"
  value       = digitalocean_droplet.ghost_platform.ipv4_address
}

output "server_ipv6" {
  description = "The public IPv6 address of the droplet"
  value       = digitalocean_droplet.ghost_platform.ipv6_address
}

output "ssh_connection_string" {
  description = "SSH connection string for the droplet"
  value       = "ssh root@${digitalocean_droplet.ghost_platform.ipv4_address}"
}

output "created_at" {
  description = "Timestamp when the droplet was created"
  value       = digitalocean_droplet.ghost_platform.created_at
}

output "server_name" {
  description = "Name of the droplet"
  value       = digitalocean_droplet.ghost_platform.name
}

output "droplet_size" {
  description = "Size of the droplet"
  value       = digitalocean_droplet.ghost_platform.size
}

output "region" {
  description = "Region of the droplet"
  value       = digitalocean_droplet.ghost_platform.region
}

output "droplet_urn" {
  description = "URN of the droplet"
  value       = digitalocean_droplet.ghost_platform.urn
}
