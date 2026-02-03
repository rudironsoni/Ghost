terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
    cloudinit = {
      source  = "hashicorp/cloudinit"
      version = "~> 2.3"
    }
  }
}

# Alternative providers for multi-cloud support
# Uncomment as needed:

# provider "google" {
#   version = "~> 5.0"
#   project = var.gcp_project_id
#   region  = var.gcp_region
# }

# provider "azurerm" {
#   version = "~> 3.0"
#   features {}
# }
