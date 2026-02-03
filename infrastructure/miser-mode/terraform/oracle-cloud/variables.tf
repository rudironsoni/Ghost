variable "tenancy_ocid" {
  description = "Oracle Cloud Tenancy OCID"
  type        = string
}

variable "user_ocid" {
  description = "Oracle Cloud User OCID"
  type        = string
}

variable "fingerprint" {
  description = "Fingerprint of the API key"
  type        = string
}

variable "private_key_path" {
  description = "Path to the private API key"
  type        = string
}

variable "region" {
  description = "Oracle Cloud region"
  type        = string
  default     = "us-ashburn-1"
}

variable "compartment_ocid" {
  description = "Compartment OCID (defaults to tenancy root if not specified)"
  type        = string
  default     = ""
}

variable "instance_name" {
  description = "Name of the compute instance"
  type        = string
  default     = "ghost-platform-miser"
}

variable "instance_shape" {
  description = "Instance shape (VM.Standard.A1.Flex for ARM-based Ampere)"
  type        = string
  default     = "VM.Standard.A1.Flex"
}

variable "instance_ocpus" {
  description = "Number of OCPUs (max 4 for free tier)"
  type        = number
  default     = 4
}

variable "instance_memory_in_gbs" {
  description = "Memory in GB (max 24 for free tier)"
  type        = number
  default     = 24
}

variable "boot_volume_size_in_gbs" {
  description = "Boot volume size in GB (max 200 for free tier)"
  type        = number
  default     = 200
}

variable "availability_domain" {
  description = "Availability domain (leave empty to use first available)"
  type        = string
  default     = ""
}

variable "ssh_public_key" {
  description = "SSH public key for instance access"
  type        = string
  default     = ""
}

variable "ssh_public_key_path" {
  description = "Path to SSH public key file"
  type        = string
  default     = "~/.ssh/id_rsa.pub"
}
