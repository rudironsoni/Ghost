variable "do_token" {
  description = "DigitalOcean API Token"
  type        = string
  sensitive   = true
}

variable "droplet_name" {
  description = "Name of the droplet"
  type        = string
  default     = "ghost-platform-miser"
}

variable "droplet_size" {
  description = "DigitalOcean droplet size"
  type        = string
  default     = "s-2vcpu-4gb"
}

variable "region" {
  description = "DigitalOcean region"
  type        = string
  default     = "nyc3"
}

variable "image" {
  description = "OS image to use"
  type        = string
  default     = "ubuntu-22-04-x64"
}

variable "ssh_keys" {
  description = "List of SSH key fingerprints to add to the droplet"
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags to apply to the droplet"
  type        = list(string)
  default     = ["miser-mode", "ghost-platform"]
}

variable "monitoring" {
  description = "Enable DigitalOcean monitoring"
  type        = bool
  default     = true
}

variable "backups" {
  description = "Enable automated backups"
  type        = bool
  default     = false
}
