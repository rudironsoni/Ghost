# ============================================================================
# GENERAL VARIABLES
# ============================================================================

variable "environment" {
  description = "Environment name (development, staging, production)"
  type        = string
  default     = "development"
}

variable "project_name" {
  description = "Project name used for resource naming and tagging"
  type        = string
  default     = "ghost"
}

variable "aws_region" {
  description = "AWS region for resource deployment"
  type        = string
  default     = "us-east-1"
}

variable "aws_profile" {
  description = "AWS CLI profile to use for authentication"
  type        = string
  default     = "default"
}

# GCP alternatives (optional)
# variable "gcp_project_id" {
#   description = "GCP project ID"
#   type        = string
#   default     = "ghost-platform"
# }

# variable "gcp_region" {
#   description = "GCP region for resource deployment"
#   type        = string
#   default     = "us-east1"
# }

# ============================================================================
# NETWORKING VARIABLES
# ============================================================================

variable "vpc_cidr" {
  description = "CIDR block for VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "public_subnet_cidrs" {
  description = "CIDR blocks for public subnets (one per AZ)"
  type        = list(string)
  default     = ["10.0.1.0/24", "10.0.2.0/24"]
}

variable "availability_zones" {
  description = "List of availability zones to use"
  type        = list(string)
  default     = ["us-east-1a", "us-east-1b"]
}

variable "enable_nat_gateway" {
  description = "Enable NAT gateway for private subnets (disabled for cost savings in dev)"
  type        = bool
  default     = false
}

# ============================================================================
# COMPUTE VARIABLES
# ============================================================================

variable "instance_type" {
  description = "EC2 instance type for k3s node"
  type        = string
  default     = "t3.small" # 2 vCPU, 2GB RAM - cost optimized
}

variable "use_spot_instances" {
  description = "Use spot instances for cost savings"
  type        = bool
  default     = true
}

variable "spot_max_price" {
  description = "Maximum price for spot instances (leave empty for on-demand price)"
  type        = string
  default     = "" # Auto-pricing
}

variable "disk_size_gb" {
  description = "Root disk size in GB"
  type        = number
  default     = 30
}

variable "enable_auto_shutdown" {
  description = "Enable automated shutdown/startup schedule"
  type        = bool
  default     = true
}

variable "auto_shutdown_schedule" {
  description = "Cron expression for shutdown (default: 8 PM EST weekdays)"
  type        = string
  default     = "0 0 * * MON-FRI" # Midnight UTC (8 PM EST)
}

variable "auto_startup_schedule" {
  description = "Cron expression for startup (default: 8 AM EST weekdays)"
  type        = string
  default     = "0 12 * * MON-FRI" # Noon UTC (8 AM EST)
}

# GCP alternative
# variable "gcp_machine_type" {
#   description = "GCP machine type"
#   type        = string
#   default     = "e2-small"
# }

# Azure alternative
# variable "azure_vm_size" {
#   description = "Azure VM size"
#   type        = string
#   default     = "Standard_B2s"
# }

# ============================================================================
# DATABASE VARIABLES
# ============================================================================

variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t3.micro" # 2 vCPU, 1GB RAM - smallest available
}

variable "db_allocated_storage" {
  description = "Allocated storage for RDS in GB"
  type        = number
  default     = 20
}

variable "db_max_allocated_storage" {
  description = "Maximum allocated storage for RDS autoscaling"
  type        = number
  default     = 50
}

variable "db_engine_version" {
  description = "PostgreSQL engine version"
  type        = string
  default     = "15.4"
}

variable "db_name" {
  description = "Database name"
  type        = string
  default     = "ghost"
}

variable "db_username" {
  description = "Database master username"
  type        = string
  default     = "ghostadmin"
  sensitive   = true
}

variable "db_backup_retention_days" {
  description = "Number of days to retain backups (7 minimum for dev)"
  type        = number
  default     = 7
}

variable "db_skip_final_snapshot" {
  description = "Skip final snapshot on deletion (true for dev)"
  type        = bool
  default     = true
}

# GCP alternative
# variable "gcp_db_tier" {
#   description = "GCP Cloud SQL tier"
#   type        = string
#   default     = "db-f1-micro"
# }

# ============================================================================
# CACHE VARIABLES (Redis)
# ============================================================================

variable "redis_node_type" {
  description = "ElastiCache node type"
  type        = string
  default     = "cache.t3.micro" # 2 vCPU, 0.5GB RAM
}

variable "redis_num_cache_nodes" {
  description = "Number of cache nodes"
  type        = number
  default     = 1
}

variable "redis_engine_version" {
  description = "Redis engine version"
  type        = string
  default     = "7.0"
}

variable "redis_parameter_group_family" {
  description = "Redis parameter group family"
  type        = string
  default     = "redis7"
}

# ============================================================================
# MESSAGING VARIABLES (RabbitMQ)
# ============================================================================

variable "mq_deployment_mode" {
  description = "Deployment mode for Amazon MQ (SINGLE_INSTANCE or ACTIVE_STANDBY_MULTI_AZ)"
  type        = string
  default     = "SINGLE_INSTANCE"
}

variable "mq_instance_type" {
  description = "Amazon MQ instance type"
  type        = string
  default     = "mq.t3.micro" # Smallest available
}

variable "mq_engine_version" {
  description = "RabbitMQ engine version"
  type        = string
  default     = "3.11.20"
}

variable "mq_username" {
  description = "RabbitMQ admin username"
  type        = string
  default     = "ghostadmin"
  sensitive   = true
}

variable "use_self_hosted_rabbitmq" {
  description = "Use self-hosted RabbitMQ on k3s instead of Amazon MQ (saves ~$10/month)"
  type        = bool
  default     = true
}

# ============================================================================
# MONITORING & OBSERVABILITY
# ============================================================================

variable "enable_cloudwatch_logs" {
  description = "Enable CloudWatch Logs"
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "CloudWatch Logs retention in days"
  type        = number
  default     = 7
}

variable "enable_detailed_monitoring" {
  description = "Enable detailed CloudWatch monitoring (costs extra)"
  type        = bool
  default     = false
}

# ============================================================================
# SECURITY VARIABLES
# ============================================================================

variable "allowed_ssh_cidr_blocks" {
  description = "CIDR blocks allowed to SSH into instances"
  type        = list(string)
  default     = ["0.0.0.0/0"] # Restrict this in production!
}

variable "allowed_http_cidr_blocks" {
  description = "CIDR blocks allowed to access HTTP/HTTPS"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "enable_encryption_at_rest" {
  description = "Enable encryption at rest for databases and volumes"
  type        = bool
  default     = true
}

variable "enable_backup_encryption" {
  description = "Enable encryption for backups"
  type        = bool
  default     = true
}

variable "ssh_key_name" {
  description = "Name of existing AWS SSH key pair (leave empty to create new)"
  type        = string
  default     = ""
}

# ============================================================================
# COST ALLOCATION & TAGGING
# ============================================================================

variable "cost_center" {
  description = "Cost center for billing allocation"
  type        = string
  default     = "engineering"
}

variable "team" {
  description = "Team responsible for resources"
  type        = string
  default     = "platform"
}

variable "owner" {
  description = "Resource owner email"
  type        = string
  default     = "platform-team@example.com"
}

variable "additional_tags" {
  description = "Additional tags to apply to all resources"
  type        = map(string)
  default     = {}
}

# ============================================================================
# FEATURE FLAGS
# ============================================================================

variable "enable_bastion_host" {
  description = "Deploy a bastion host for SSH access"
  type        = bool
  default     = false # Not needed for dev with public access
}

variable "enable_load_balancer" {
  description = "Deploy an Application Load Balancer"
  type        = bool
  default     = false # Single node doesn't need LB
}

variable "enable_autoscaling" {
  description = "Enable auto-scaling for compute instances"
  type        = bool
  default     = false # Single node environment
}

variable "enable_multi_az" {
  description = "Enable multi-AZ deployment for high availability"
  type        = bool
  default     = false # Single-AZ for cost savings in dev
}
