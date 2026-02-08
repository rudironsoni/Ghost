# Compute Module
# Manages compute resources: VMs, Kubernetes clusters, node groups

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
  }
}

# Local values
locals {
  common_tags = merge(
    var.tags,
    {
      ManagedBy = "Terraform"
      Module    = "compute"
    }
  )
}

#------------------------------------------------------------------------------
# EKS Cluster
#------------------------------------------------------------------------------

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 19.0"

  cluster_name    = var.cluster_name
  cluster_version = var.kubernetes_version

  vpc_id     = var.vpc_id
  subnet_ids = var.private_subnet_ids

  # Control plane logging
  cluster_enabled_log_types = ["api", "audit", "authenticator", "controllerManager", "scheduler"]

  # Public endpoint for development, private for production
  cluster_endpoint_public_access  = var.environment == "production" ? false : true
  cluster_endpoint_private_access = true

  # Managed node groups
  eks_managed_node_groups = {
    main = {
      name = "${var.cluster_name}-main"

      instance_types = var.instance_types
      capacity_type  = var.capacity_type

      min_size     = var.min_size
      max_size     = var.max_size
      desired_size = var.desired_size

      disk_size = var.disk_size

      labels = {
        Environment = var.environment
        NodeGroup   = "main"
      }

      tags = local.common_tags

      # Taints for spot instances
      taints = var.capacity_type == "SPOT" ? [
        {
          key    = "spot"
          value  = "true"
          effect = "NO_SCHEDULE"
        }
      ] : []
    }
  }

  # IRSA (IAM Roles for Service Accounts)
  enable_irsa = true

  # Cluster addons
  cluster_addons = {
    coredns = {
      most_recent = true
    }
    kube-proxy = {
      most_recent = true
    }
    vpc-cni = {
      most_recent = true
    }
    aws-ebs-csi-driver = {
      most_recent = true
    }
  }

  tags = local.common_tags
}

#------------------------------------------------------------------------------
# Karpenter (Auto-scaling) - Optional
#------------------------------------------------------------------------------

module "karpenter" {
  source  = "terraform-aws-modules/eks/aws//modules/karpenter"
  version = "~> 19.0"

  cluster_name = module.eks.cluster_name

  irsa_oidc_provider_arn          = module.eks.oidc_provider_arn
  irsa_namespace_service_accounts = ["karpenter:karpenter"]

  tags = local.common_tags

  count = var.enable_karpenter ? 1 : 0
}

#------------------------------------------------------------------------------
# Bastion Host (Jump box) - Optional
#------------------------------------------------------------------------------

resource "aws_instance" "bastion" {
  count = var.create_bastion ? 1 : 0

  ami           = data.aws_ami.amazon_linux_2.id
  instance_type = "t3.micro"
  key_name      = var.key_name

  subnet_id              = var.public_subnet_ids[0]
  vpc_security_group_ids = [aws_security_group.bastion[0].id]

  user_data = <<-EOF
              #!/bin/bash
              yum update -y
              yum install -y kubectl
              echo "Bastion host ready"
              EOF

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-bastion"
  })
}

#------------------------------------------------------------------------------
# Security Groups
#------------------------------------------------------------------------------

resource "aws_security_group" "bastion" {
  count = var.create_bastion ? 1 : 0

  name_prefix = "${var.cluster_name}-bastion-"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.bastion_allowed_cidr
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.common_tags
}

#------------------------------------------------------------------------------
# Data Sources
#------------------------------------------------------------------------------

data "aws_ami" "amazon_linux_2" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["amzn2-ami-hvm-*-x86_64-gp2"]
  }
}