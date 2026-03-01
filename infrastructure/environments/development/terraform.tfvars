# ============================================================================
# TERRAFORM VARIABLES - DEVELOPMENT ENVIRONMENT
# ============================================================================
# This file contains cost-optimized default values for the development
# environment. Customize as needed for your specific requirements.
# ============================================================================

# ============================================================================
# GENERAL CONFIGURATION
# ============================================================================

environment  = "development"
project_name = "ghost"
aws_region   = "us-east-1"
aws_profile  = "default"

# ============================================================================
# NETWORKING
# ============================================================================

vpc_cidr             = "10.0.0.0/16"
public_subnet_cidrs  = ["10.0.1.0/24", "10.0.2.0/24"]
availability_zones   = ["us-east-1a", "us-east-1b"]
enable_nat_gateway   = false # Cost saving: no private subnets needed

# ============================================================================
# COMPUTE - K3S NODE
# ============================================================================

instance_type        = "t3.small"  # 2 vCPU, 2GB RAM (~$15/month on-demand, ~$5/month spot)
use_spot_instances   = true        # Use spot instances for ~70% cost savings
spot_max_price       = ""          # Empty = use current spot price (recommended)
disk_size_gb         = 30          # 30GB should be sufficient for dev

# Auto-shutdown to save costs during off-hours
enable_auto_shutdown    = true
auto_shutdown_schedule  = "0 0 * * MON-FRI"  # Midnight UTC (8 PM EST) on weekdays
auto_startup_schedule   = "0 12 * * MON-FRI" # Noon UTC (8 AM EST) on weekdays

# Alternative instance types for different workloads:
# - t3.micro:  1 vCPU, 1GB RAM  - minimal workload (~$7/month, ~$3 spot)
# - t3.small:  2 vCPU, 2GB RAM  - light workload (~$15/month, ~$5 spot) [DEFAULT]
# - t3.medium: 2 vCPU, 4GB RAM  - moderate workload (~$30/month, ~$10 spot)
# - t3.large:  2 vCPU, 8GB RAM  - heavier workload (~$60/month, ~$20 spot)

# ============================================================================
# DATABASE - RDS POSTGRESQL
# ============================================================================

db_instance_class          = "db.t3.micro"  # Smallest RDS instance (~$12/month)
db_allocated_storage       = 20             # 20GB minimum for gp3
db_max_allocated_storage   = 50             # Auto-scale up to 50GB if needed
db_engine_version          = "15.4"         # PostgreSQL 15.4
db_name                    = "ghost"
db_username                = "ghostadmin"
db_backup_retention_days   = 7              # 7 days minimum for dev
db_skip_final_snapshot     = true           # Skip final snapshot on deletion (dev only!)

# Alternative database tiers:
# - db.t3.micro:  2 vCPU, 1GB RAM   (~$12/month) [DEFAULT]
# - db.t4g.micro: 2 vCPU, 1GB RAM   (~$10/month, ARM-based, slightly cheaper)
# - db.t3.small:  2 vCPU, 2GB RAM   (~$25/month)

# ============================================================================
# CACHE - ELASTICACHE REDIS
# ============================================================================

redis_node_type              = "cache.t3.micro"  # Smallest Redis node (~$12/month)
redis_num_cache_nodes        = 1                  # Single node for dev
redis_engine_version         = "7.0"
redis_parameter_group_family = "redis7"

# Alternative Redis tiers:
# - cache.t3.micro:  2 vCPU, 0.5GB RAM (~$12/month) [DEFAULT]
# - cache.t4g.micro: 2 vCPU, 0.5GB RAM (~$10/month, ARM-based)
# - cache.t3.small:  2 vCPU, 1.37GB RAM (~$25/month)

# ============================================================================
# MESSAGING - RABBITMQ
# ============================================================================

use_self_hosted_rabbitmq = true  # Use self-hosted on k3s to save ~$30/month

# If using Amazon MQ (set use_self_hosted_rabbitmq = false):
mq_deployment_mode = "SINGLE_INSTANCE"  # Single instance for dev (~$30/month)
mq_instance_type   = "mq.t3.micro"      # Smallest MQ instance
mq_engine_version  = "3.11.20"          # RabbitMQ version
mq_username        = "ghostadmin"

# Note: Amazon MQ minimum cost is ~$30/month even for t3.micro
# Self-hosted RabbitMQ on k3s is free but requires more management

# ============================================================================
# MONITORING & OBSERVABILITY
# ============================================================================

enable_cloudwatch_logs     = true   # Enable CloudWatch Logs
log_retention_days         = 7      # Keep logs for 7 days
enable_detailed_monitoring = false  # Detailed monitoring costs extra

# ============================================================================
# SECURITY
# ============================================================================

# WARNING: These settings are permissive for development convenience
# MUST be restricted in staging/production!
allowed_ssh_cidr_blocks  = ["0.0.0.0/0"]  # SSH access from anywhere
allowed_http_cidr_blocks = ["0.0.0.0/0"]  # HTTP/HTTPS access from anywhere

# Encryption
enable_encryption_at_rest  = true  # Encrypt EBS volumes, RDS, etc.
enable_backup_encryption   = true  # Encrypt backups

# SSH Key
ssh_key_name = ""  # Leave empty to generate new key pair

# To use an existing SSH key:
# ssh_key_name = "my-existing-key"

# ============================================================================
# COST ALLOCATION & TAGGING
# ============================================================================

cost_center = "engineering"
team        = "platform"
owner       = "platform-team@example.com"

additional_tags = {
  "CostOptimized" = "true"
  "AutoShutdown"  = "enabled"
  "Terraform"     = "true"
  "Repository"    = "rudironsoni/Ghost"
}

# ============================================================================
# FEATURE FLAGS
# ============================================================================

enable_bastion_host = false  # Not needed for dev with public access
enable_load_balancer = false # Single node doesn't need LB
enable_autoscaling  = false  # No autoscaling for single node
enable_multi_az     = false  # Single AZ for cost savings

# ============================================================================
# COST ESTIMATION SUMMARY
# ============================================================================
# Based on the above configuration:
#
# Monthly Costs (approximate):
# - EC2 k3s node (t3.small spot):     $5-8
# - RDS PostgreSQL (db.t3.micro):     $12-15
# - ElastiCache Redis (cache.t3.micro): $12-15
# - RabbitMQ (self-hosted):           $0
# - EBS storage (30GB):               $3
# - Data transfer & misc:             $5-10
# - CloudWatch Logs:                  $2-5
# ----------------------------------------
# TOTAL:                              $40-60/month
#
# With auto-shutdown 12hrs/day:       $25-40/month
#
# If using Amazon MQ instead:         Add $30/month
#
# Cost Optimization Tips:
# 1. Enable auto-shutdown (saves ~40% on compute)
# 2. Use spot instances (saves ~70% on compute)
# 3. Use self-hosted RabbitMQ (saves $30/month)
# 4. Stop instances when not in use
# 5. Use AWS Savings Plans for long-term (production)
# 6. Set up billing alerts
#
# ============================================================================

# ============================================================================
# ALTERNATIVE CONFIGURATIONS
# ============================================================================

# Ultra-Minimal Configuration (~$25-30/month):
# instance_type = "t3.micro"
# db_instance_class = "db.t4g.micro"
# redis_node_type = "cache.t4g.micro"
# use_self_hosted_rabbitmq = true
# enable_auto_shutdown = true
# enable_cloudwatch_logs = false

# Comfortable Development Configuration (~$60-80/month):
# instance_type = "t3.medium"
# db_instance_class = "db.t3.small"
# redis_node_type = "cache.t3.small"
# use_self_hosted_rabbitmq = true
# enable_auto_shutdown = true

# Production-Like Development (~$150-200/month):
# instance_type = "t3.large"
# db_instance_class = "db.t3.small"
# redis_node_type = "cache.t3.small"
# mq_deployment_mode = "ACTIVE_STANDBY_MULTI_AZ"
# use_self_hosted_rabbitmq = false
# enable_multi_az = true
# enable_auto_shutdown = false
