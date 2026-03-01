# Production Environment Variables

################################################################################
# General
################################################################################

variable "project_name" {
  description = "Name of the project"
  type        = string
  default     = "ghost-blog"
}

variable "owner" {
  description = "Owner of the resources"
  type        = string
  default     = "platform-team"
}

variable "cost_center" {
  description = "Cost center for billing"
  type        = string
  default     = "engineering"
}

variable "common_tags" {
  description = "Common tags to apply to all resources"
  type        = map(string)
  default     = {}
}

################################################################################
# Networking
################################################################################

variable "vpc_cidr" {
  description = "CIDR block for VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "private_subnet_cidrs" {
  description = "CIDR blocks for private subnets"
  type        = list(string)
  default     = ["10.0.1.0/24", "10.0.2.0/24", "10.0.3.0/24"]
}

variable "public_subnet_cidrs" {
  description = "CIDR blocks for public subnets"
  type        = list(string)
  default     = ["10.0.101.0/24", "10.0.102.0/24", "10.0.103.0/24"]
}

variable "database_subnet_cidrs" {
  description = "CIDR blocks for database subnets"
  type        = list(string)
  default     = ["10.0.21.0/24", "10.0.22.0/24", "10.0.23.0/24"]
}

variable "elasticache_subnet_cidrs" {
  description = "CIDR blocks for ElastiCache subnets"
  type        = list(string)
  default     = ["10.0.31.0/24", "10.0.32.0/24", "10.0.33.0/24"]
}

variable "flow_log_retention_days" {
  description = "Number of days to retain VPC flow logs"
  type        = number
  default     = 90
}

variable "alb_ingress_cidrs" {
  description = "CIDR blocks allowed to access the ALB"
  type        = list(string)
  default     = ["0.0.0.0/0"] # Restrict this in production
}

################################################################################
# EKS
################################################################################

variable "eks_cluster_version" {
  description = "Kubernetes version for EKS cluster"
  type        = string
  default     = "1.28"
}

variable "eks_node_instance_types" {
  description = "Instance types for EKS primary node group"
  type        = list(string)
  default     = ["r6i.xlarge", "r6i.2xlarge"] # For production workloads
}

variable "eks_node_min_size" {
  description = "Minimum number of nodes in primary node group"
  type        = number
  default     = 3
}

variable "eks_node_max_size" {
  description = "Maximum number of nodes in primary node group"
  type        = number
  default     = 10
}

variable "eks_node_desired_size" {
  description = "Desired number of nodes in primary node group"
  type        = number
  default     = 5
}

variable "eks_highmem_instance_types" {
  description = "Instance types for high-memory workloads"
  type        = list(string)
  default     = ["r6i.2xlarge", "r6i.4xlarge"]
}

################################################################################
# RDS PostgreSQL
################################################################################

variable "rds_engine_version" {
  description = "PostgreSQL engine version"
  type        = string
  default     = "15.4"
}

variable "rds_parameter_group_family" {
  description = "RDS parameter group family"
  type        = string
  default     = "postgres15"
}

variable "rds_major_engine_version" {
  description = "RDS major engine version"
  type        = string
  default     = "15"
}

variable "rds_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.r6i.large" # Production-grade instance
}

variable "rds_replica_instance_class" {
  description = "RDS read replica instance class"
  type        = string
  default     = "db.r6i.large"
}

variable "rds_allocated_storage" {
  description = "Initial allocated storage in GB"
  type        = number
  default     = 500
}

variable "rds_max_allocated_storage" {
  description = "Maximum allocated storage for autoscaling in GB"
  type        = number
  default     = 2000
}

variable "rds_iops" {
  description = "IOPS for gp3 storage"
  type        = number
  default     = 12000
}

variable "rds_database_name" {
  description = "Name of the database"
  type        = string
  default     = "ghost"
}

variable "rds_master_username" {
  description = "Master username for RDS"
  type        = string
  default     = "ghost_admin"
  sensitive   = true
}

variable "rds_maintenance_window" {
  description = "Maintenance window for RDS"
  type        = string
  default     = "sun:03:00-sun:04:00"
}

variable "rds_backup_window" {
  description = "Backup window for RDS"
  type        = string
  default     = "02:00-02:30"
}

variable "rds_backup_retention_period" {
  description = "Number of days to retain backups"
  type        = number
  default     = 30 # 30 days for production
}

variable "rds_deletion_protection" {
  description = "Enable deletion protection for RDS"
  type        = bool
  default     = true
}

variable "rds_max_connections" {
  description = "Maximum number of database connections"
  type        = string
  default     = "500"
}

################################################################################
# ElastiCache Redis
################################################################################

variable "redis_engine_version" {
  description = "Redis engine version"
  type        = string
  default     = "7.0"
}

variable "redis_node_type" {
  description = "ElastiCache node type"
  type        = string
  default     = "cache.r7g.large"
}

variable "redis_num_cache_nodes" {
  description = "Number of cache nodes (for non-cluster mode)"
  type        = number
  default     = 2
}

variable "redis_parameter_group_name" {
  description = "Parameter group name"
  type        = string
  default     = "default.redis7.cluster.on"
}

variable "redis_num_node_groups" {
  description = "Number of node groups (shards) for cluster mode"
  type        = number
  default     = 3
}

variable "redis_replicas_per_node_group" {
  description = "Number of replicas per node group"
  type        = number
  default     = 2
}

variable "redis_auth_token" {
  description = "Auth token for Redis (minimum 16 characters)"
  type        = string
  sensitive   = true
}

variable "redis_maintenance_window" {
  description = "Maintenance window for ElastiCache"
  type        = string
  default     = "sun:05:00-sun:06:00"
}

variable "redis_snapshot_window" {
  description = "Snapshot window for ElastiCache"
  type        = string
  default     = "04:00-04:30"
}

variable "redis_snapshot_retention_limit" {
  description = "Number of days to retain snapshots"
  type        = number
  default     = 7
}

################################################################################
# Amazon MQ (RabbitMQ)
################################################################################

variable "mq_engine_version" {
  description = "RabbitMQ engine version"
  type        = string
  default     = "3.11.20"
}

variable "mq_instance_type" {
  description = "Amazon MQ instance type"
  type        = string
  default     = "mq.m5.large"
}

variable "mq_admin_username" {
  description = "Admin username for Amazon MQ"
  type        = string
  default     = "admin"
  sensitive   = true
}

variable "mq_admin_password" {
  description = "Admin password for Amazon MQ"
  type        = string
  sensitive   = true
}

variable "mq_app_username" {
  description = "Application username for Amazon MQ"
  type        = string
  default     = "ghost_app"
  sensitive   = true
}

variable "mq_app_password" {
  description = "Application password for Amazon MQ"
  type        = string
  sensitive   = true
}

variable "mq_max_connections" {
  description = "Maximum connections for RabbitMQ"
  type        = number
  default     = 1000
}

variable "mq_auto_minor_version_upgrade" {
  description = "Enable auto minor version upgrades"
  type        = bool
  default     = true
}

################################################################################
# ALB
################################################################################

variable "acm_certificate_arn" {
  description = "ARN of ACM certificate for HTTPS"
  type        = string
}

variable "alb_deletion_protection" {
  description = "Enable deletion protection for ALB"
  type        = bool
  default     = true
}

################################################################################
# WAF
################################################################################

variable "waf_rate_limit" {
  description = "Rate limit for WAF (requests per 5 minutes)"
  type        = number
  default     = 2000
}

variable "waf_blocked_countries" {
  description = "List of country codes to block (ISO 3166-1 alpha-2)"
  type        = list(string)
  default     = [] # Add countries if geo-blocking is needed
}

################################################################################
# Monitoring and Logging
################################################################################

variable "log_retention_days" {
  description = "Number of days to retain CloudWatch logs"
  type        = number
  default     = 90
}

variable "alert_email" {
  description = "Email address for alerts"
  type        = string
}

################################################################################
# Tags for Cost Allocation
################################################################################

variable "department" {
  description = "Department for cost allocation"
  type        = string
  default     = "Engineering"
}

variable "application" {
  description = "Application name for cost allocation"
  type        = string
  default     = "Ghost Blog Platform"
}

variable "business_unit" {
  description = "Business unit for cost allocation"
  type        = string
  default     = "Product"
}

variable "project_id" {
  description = "Project ID for tracking"
  type        = string
  default     = "GHOST-PROD-001"
}

################################################################################
# Backup and DR
################################################################################

variable "enable_cross_region_backup" {
  description = "Enable cross-region backup replication"
  type        = bool
  default     = true
}

variable "backup_region" {
  description = "Region for backup replication"
  type        = string
  default     = "us-west-2"
}

variable "dr_region" {
  description = "Disaster recovery region"
  type        = string
  default     = "eu-west-1"
}

################################################################################
# Compliance
################################################################################

variable "compliance_standards" {
  description = "List of compliance standards to adhere to"
  type        = list(string)
  default     = ["SOC2", "GDPR", "HIPAA"]
}

variable "data_retention_policy_days" {
  description = "Days to retain data for compliance"
  type        = number
  default     = 2555 # 7 years
}

variable "enable_audit_logging" {
  description = "Enable comprehensive audit logging"
  type        = bool
  default     = true
}

################################################################################
# Performance
################################################################################

variable "enable_enhanced_monitoring" {
  description = "Enable enhanced monitoring for all resources"
  type        = bool
  default     = true
}

variable "enable_performance_insights" {
  description = "Enable Performance Insights for RDS"
  type        = bool
  default     = true
}

################################################################################
# Disaster Recovery
################################################################################

variable "rto_minutes" {
  description = "Recovery Time Objective in minutes"
  type        = number
  default     = 60
}

variable "rpo_minutes" {
  description = "Recovery Point Objective in minutes"
  type        = number
  default     = 15
}
