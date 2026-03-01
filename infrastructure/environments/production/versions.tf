# Terraform and Provider Version Constraints

terraform {
  required_version = ">= 1.6.0"

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

    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }

    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.11"
    }

    kubectl = {
      source  = "gavinbunney/kubectl"
      version = "~> 1.14"
    }

    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }

    time = {
      source  = "hashicorp/time"
      version = "~> 0.9"
    }
  }
}

# AWS Provider Configuration
provider "aws" {
  region = var.aws_region

  # Default tags applied to all resources
  default_tags {
    tags = {
      Environment        = "production"
      Project            = var.project_name
      ManagedBy          = "Terraform"
      TerraformWorkspace = terraform.workspace
    }
  }

  # Assume role configuration (if using cross-account access)
  # Uncomment and configure if needed
  # assume_role {
  #   role_arn     = "arn:aws:iam::ACCOUNT_ID:role/TerraformExecutionRole"
  #   session_name = "TerraformProduction"
  # }
}

# Additional AWS provider for DR region
provider "aws" {
  alias  = "dr"
  region = var.dr_region

  default_tags {
    tags = {
      Environment        = "production-dr"
      Project            = var.project_name
      ManagedBy          = "Terraform"
      TerraformWorkspace = terraform.workspace
    }
  }
}

# Kubernetes provider configuration (configured after EKS cluster creation)
provider "kubernetes" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)

  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args = [
      "eks",
      "get-token",
      "--cluster-name",
      module.eks.cluster_id,
      "--region",
      var.aws_region
    ]
  }
}

# Helm provider configuration
provider "helm" {
  kubernetes {
    host                   = module.eks.cluster_endpoint
    cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)

    exec {
      api_version = "client.authentication.k8s.io/v1beta1"
      command     = "aws"
      args = [
        "eks",
        "get-token",
        "--cluster-name",
        module.eks.cluster_id,
        "--region",
        var.aws_region
      ]
    }
  }
}

# Kubectl provider configuration
provider "kubectl" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(module.eks.cluster_certificate_authority_data)
  load_config_file       = false

  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args = [
      "eks",
      "get-token",
      "--cluster-name",
      module.eks.cluster_id,
      "--region",
      var.aws_region
    ]
  }
}

# AWS Region variable for provider
variable "aws_region" {
  description = "AWS region for resources"
  type        = string
  default     = "us-east-1"
}
