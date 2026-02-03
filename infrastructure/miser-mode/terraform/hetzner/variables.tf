variable "hcloud_token" {
  description = "Hetzner Cloud API Token"
  type        = string
  sensitive   = true
}

variable "server_name" {
  description = "Name of the server"
  type        = string
  default     = "ghost-platform-miser"
}

variable "server_type" {
  description = "Hetzner server type"
  type        = string
  default     = "cpx21"
}

variable "location" {
  description = "Hetzner datacenter location"
  type        = string
  default     = "nbg1"
}

variable "image" {
  description = "OS image to use"
  type        = string
  default     = "ubuntu-22.04"
}

variable "ssh_keys" {
  description = "List of SSH key IDs to add to the server"
  type        = list(string)
  default     = []
}

variable "labels" {
  description = "Labels to apply to the server"
  type        = map(string)
  default = {
    environment = "miser-mode"
    platform    = "ghost"
  }
}
