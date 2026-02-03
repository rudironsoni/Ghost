module "cloud_init" {
  source = "../modules/cloud-init"
}

# Firewall for Ghost Platform ports
resource "digitalocean_firewall" "ghost_platform" {
  name = "${var.droplet_name}-firewall"

  droplet_ids = [digitalocean_droplet.ghost_platform.id]

  inbound_rule {
    protocol         = "tcp"
    port_range       = "22"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  inbound_rule {
    protocol         = "tcp"
    port_range       = "80"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  inbound_rule {
    protocol         = "tcp"
    port_range       = "443"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  inbound_rule {
    protocol         = "tcp"
    port_range       = "8080"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  inbound_rule {
    protocol         = "tcp"
    port_range       = "9090"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  inbound_rule {
    protocol         = "tcp"
    port_range       = "3000"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  outbound_rule {
    protocol              = "tcp"
    port_range            = "1-65535"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }

  outbound_rule {
    protocol              = "udp"
    port_range            = "1-65535"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }

  outbound_rule {
    protocol              = "icmp"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }
}

# Ghost Platform Droplet
resource "digitalocean_droplet" "ghost_platform" {
  name       = var.droplet_name
  size       = var.droplet_size
  region     = var.region
  image      = var.image
  ssh_keys   = var.ssh_keys
  tags       = var.tags
  monitoring = var.monitoring
  backups    = var.backups
  user_data  = module.cloud_init.user_data

  lifecycle {
    create_before_destroy = true
  }
}
