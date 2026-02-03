# Ghost Blog Platform - Production Environment
# Enterprise-grade HA deployment with multi-AZ configuration

terraform {
  required_version = ">= 1.6.0"
}

# Data sources for availability zones
data "aws_availability_zones" "available" {
  state = "available"
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

# Local variables for common tags
locals {
  environment = "production"
  project     = "ghost-blog"
  owner       = var.owner
  cost_center = var.cost_center

  common_tags = merge(
    var.common_tags,
    {
      Environment        = local.environment
      Project            = local.project
      Owner              = local.owner
      CostCenter         = local.cost_center
      ManagedBy          = "Terraform"
      Compliance         = "SOC2,GDPR,HIPAA"
      BackupPolicy       = "Daily"
      DisasterRecovery   = "Enabled"
      BusinessCriticality = "High"
      DataClassification = "Confidential"
    }
  )
}

################################################################################
# VPC and Networking
################################################################################

module "vpc" {
  source = "../../modules/networking"

  name                 = "${var.project_name}-${local.environment}"
  cidr                 = var.vpc_cidr
  azs                  = slice(data.aws_availability_zones.available.names, 0, 3)
  private_subnets      = var.private_subnet_cidrs
  public_subnets       = var.public_subnet_cidrs
  database_subnets     = var.database_subnet_cidrs
  elasticache_subnets  = var.elasticache_subnet_cidrs

  # HA Configuration
  enable_nat_gateway   = true
  single_nat_gateway   = false # Multi-AZ NAT for HA
  one_nat_gateway_per_az = true

  # DNS
  enable_dns_hostnames = true
  enable_dns_support   = true

  # VPC Flow Logs for compliance
  enable_flow_log                      = true
  create_flow_log_cloudwatch_iam_role  = true
  create_flow_log_cloudwatch_log_group = true
  flow_log_retention_in_days           = var.flow_log_retention_days

  # Network segmentation
  create_database_subnet_group      = true
  create_elasticache_subnet_group   = true
  create_database_subnet_route_table = true

  # VPC Endpoints for cost optimization and security
  enable_s3_endpoint       = true
  enable_ecr_endpoint      = true
  enable_ecr_dkr_endpoint  = true
  enable_logs_endpoint     = true
  enable_sts_endpoint      = true
  enable_ssm_endpoint      = true

  tags = local.common_tags
}

################################################################################
# Security Groups
################################################################################

module "security_groups" {
  source = "../../modules/security"

  vpc_id      = module.vpc.vpc_id
  environment = local.environment

  # ALB Security Group
  alb_ingress_cidrs = var.alb_ingress_cidrs
  
  # EKS Security Group
  eks_cluster_name = "${var.project_name}-${local.environment}"
  
  # RDS Security Group
  rds_allowed_security_groups = [module.eks.cluster_security_group_id]
  
  # ElastiCache Security Group
  elasticache_allowed_security_groups = [module.eks.cluster_security_group_id]
  
  # Amazon MQ Security Group
  mq_allowed_security_groups = [module.eks.cluster_security_group_id]

  tags = local.common_tags
}

################################################################################
# EKS Cluster (High Availability)
################################################################################

module "eks" {
  source = "../../modules/compute"

  cluster_name    = "${var.project_name}-${local.environment}"
  cluster_version = var.eks_cluster_version

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  # Control plane logging for compliance
  cluster_enabled_log_types = [
    "api",
    "audit",
    "authenticator",
    "controllerManager",
    "scheduler"
  ]

  # Encryption at rest
  cluster_encryption_config = {
    provider_key_arn = aws_kms_key.eks.arn
    resources        = ["secrets"]
  }

  # Node groups with multi-AZ distribution
  node_groups = {
    primary = {
      name           = "primary-node-group"
      instance_types = var.eks_node_instance_types
      capacity_type  = "ON_DEMAND" # Reserved instances for production
      
      min_size     = var.eks_node_min_size
      max_size     = var.eks_node_max_size
      desired_size = var.eks_node_desired_size

      # Spread across all AZs
      subnet_ids = module.vpc.private_subnets

      # Labels for pod scheduling
      labels = {
        Environment = local.environment
        NodeGroup   = "primary"
        Workload    = "general"
      }

      # Taints for specialized workloads (none for primary)
      taints = []

      # EBS encryption
      block_device_mappings = {
        xvda = {
          device_name = "/dev/xvda"
          ebs = {
            volume_size           = 100
            volume_type           = "gp3"
            iops                  = 3000
            throughput            = 125
            encrypted             = true
            kms_key_id            = aws_kms_key.ebs.arn
            delete_on_termination = true
          }
        }
      }

      # IAM role for nodes
      iam_role_additional_policies = [
        "arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy",
        "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
      ]

      tags = merge(local.common_tags, {
        "k8s.io/cluster-autoscaler/${var.project_name}-${local.environment}" = "owned"
        "k8s.io/cluster-autoscaler/enabled"                                   = "true"
      })
    }

    # High-memory node group for database-heavy workloads
    highmem = {
      name           = "highmem-node-group"
      instance_types = var.eks_highmem_instance_types
      capacity_type  = "ON_DEMAND"
      
      min_size     = 1
      max_size     = 5
      desired_size = 2

      subnet_ids = module.vpc.private_subnets

      labels = {
        Environment = local.environment
        NodeGroup   = "highmem"
        Workload    = "memory-intensive"
      }

      taints = [
        {
          key    = "workload"
          value  = "memory-intensive"
          effect = "NoSchedule"
        }
      ]

      block_device_mappings = {
        xvda = {
          device_name = "/dev/xvda"
          ebs = {
            volume_size           = 150
            volume_type           = "gp3"
            iops                  = 3000
            encrypted             = true
            kms_key_id            = aws_kms_key.ebs.arn
            delete_on_termination = true
          }
        }
      }

      tags = merge(local.common_tags, {
        "k8s.io/cluster-autoscaler/${var.project_name}-${local.environment}" = "owned"
        "k8s.io/cluster-autoscaler/enabled"                                   = "true"
      })
    }
  }

  # OIDC provider for IRSA (IAM Roles for Service Accounts)
  enable_irsa = true

  # Cluster add-ons
  cluster_addons = {
    coredns = {
      most_recent = true
    }
    kube-proxy = {
      most_recent = true
    }
    vpc-cni = {
      most_recent              = true
      service_account_role_arn = aws_iam_role.vpc_cni.arn
    }
    aws-ebs-csi-driver = {
      most_recent              = true
      service_account_role_arn = aws_iam_role.ebs_csi.arn
    }
  }

  tags = local.common_tags
}

################################################################################
# RDS PostgreSQL (Multi-AZ for High Availability)
################################################################################

module "rds" {
  source = "../../modules/database"

  identifier = "${var.project_name}-${local.environment}"

  # Engine configuration
  engine               = "postgres"
  engine_version       = var.rds_engine_version
  family               = var.rds_parameter_group_family
  major_engine_version = var.rds_major_engine_version
  instance_class       = var.rds_instance_class

  # Storage configuration
  allocated_storage     = var.rds_allocated_storage
  max_allocated_storage = var.rds_max_allocated_storage
  storage_encrypted     = true
  kms_key_id            = aws_kms_key.rds.arn
  storage_type          = "gp3"
  iops                  = var.rds_iops

  # Database configuration
  db_name  = var.rds_database_name
  username = var.rds_master_username
  port     = 5432

  # High Availability
  multi_az               = true
  db_subnet_group_name   = module.vpc.database_subnet_group_name
  vpc_security_group_ids = [module.security_groups.rds_security_group_id]

  # Maintenance and backups
  maintenance_window              = var.rds_maintenance_window
  backup_window                   = var.rds_backup_window
  backup_retention_period         = var.rds_backup_retention_period
  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]
  skip_final_snapshot             = false
  final_snapshot_identifier       = "${var.project_name}-${local.environment}-final-${formatdate("YYYY-MM-DD-hhmm", timestamp())}"
  copy_tags_to_snapshot           = true

  # Performance Insights
  performance_insights_enabled          = true
  performance_insights_retention_period = 7
  performance_insights_kms_key_id       = aws_kms_key.rds.arn

  # Monitoring
  monitoring_interval             = 60
  monitoring_role_arn             = aws_iam_role.rds_monitoring.arn
  create_monitoring_role          = false
  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  # Deletion protection
  deletion_protection = var.rds_deletion_protection

  # Parameter group
  parameters = [
    {
      name  = "log_connections"
      value = "1"
    },
    {
      name  = "log_disconnections"
      value = "1"
    },
    {
      name  = "log_duration"
      value = "1"
    },
    {
      name  = "shared_preload_libraries"
      value = "pg_stat_statements"
    },
    {
      name  = "max_connections"
      value = var.rds_max_connections
    }
  ]

  tags = local.common_tags
}

# RDS Read Replica for reporting/analytics
module "rds_replica" {
  source = "../../modules/database"

  identifier             = "${var.project_name}-${local.environment}-replica"
  replicate_source_db    = module.rds.db_instance_id
  
  # Replica configuration
  instance_class         = var.rds_replica_instance_class
  allocated_storage      = var.rds_allocated_storage
  storage_encrypted      = true
  kms_key_id             = aws_kms_key.rds.arn

  # Network
  vpc_security_group_ids = [module.security_groups.rds_security_group_id]

  # Monitoring
  performance_insights_enabled = true
  monitoring_interval          = 60

  # Backups not needed for replica
  backup_retention_period = 0
  skip_final_snapshot     = true

  tags = merge(local.common_tags, {
    Role = "ReadReplica"
  })
}

################################################################################
# ElastiCache Redis (Cluster Mode)
################################################################################

module "elasticache" {
  source = "../../modules/cache"

  cluster_id           = "${var.project_name}-${local.environment}"
  engine               = "redis"
  engine_version       = var.redis_engine_version
  node_type            = var.redis_node_type
  num_cache_nodes      = var.redis_num_cache_nodes
  parameter_group_name = var.redis_parameter_group_name

  # High Availability
  automatic_failover_enabled = true
  multi_az_enabled           = true
  num_node_groups            = var.redis_num_node_groups
  replicas_per_node_group    = var.redis_replicas_per_node_group

  # Network
  subnet_group_name  = module.vpc.elasticache_subnet_group_name
  security_group_ids = [module.security_groups.elasticache_security_group_id]

  # Encryption
  at_rest_encryption_enabled = true
  kms_key_id                 = aws_kms_key.elasticache.arn
  transit_encryption_enabled = true
  auth_token                 = var.redis_auth_token

  # Maintenance and backups
  maintenance_window       = var.redis_maintenance_window
  snapshot_window          = var.redis_snapshot_window
  snapshot_retention_limit = var.redis_snapshot_retention_limit

  # Monitoring
  notification_topic_arn = aws_sns_topic.elasticache_events.arn

  # Logging
  log_delivery_configuration = [
    {
      destination      = aws_cloudwatch_log_group.elasticache_slow_log.name
      destination_type = "cloudwatch-logs"
      log_format       = "json"
      log_type         = "slow-log"
    },
    {
      destination      = aws_cloudwatch_log_group.elasticache_engine_log.name
      destination_type = "cloudwatch-logs"
      log_format       = "json"
      log_type         = "engine-log"
    }
  ]

  tags = local.common_tags
}

################################################################################
# Amazon MQ (RabbitMQ)
################################################################################

module "amazon_mq" {
  source = "../../modules/messaging"

  broker_name        = "${var.project_name}-${local.environment}"
  engine_type        = "RabbitMQ"
  engine_version     = var.mq_engine_version
  host_instance_type = var.mq_instance_type
  deployment_mode    = "CLUSTER_MULTI_AZ" # HA across AZs

  # Network
  subnet_ids         = slice(module.vpc.private_subnets, 0, 2) # Requires exactly 2 subnets for cluster mode
  security_group_ids = [module.security_groups.mq_security_group_id]
  publicly_accessible = false

  # Users
  users = [
    {
      username         = var.mq_admin_username
      password         = var.mq_admin_password
      console_access   = true
      groups           = ["admin"]
    },
    {
      username         = var.mq_app_username
      password         = var.mq_app_password
      console_access   = false
      groups           = []
    }
  ]

  # Configuration
  configuration = {
    data = templatefile("${path.module}/rabbitmq-config.xml", {
      max_connections = var.mq_max_connections
    })
  }

  # Encryption
  encryption_options = {
    kms_key_id        = aws_kms_key.mq.arn
    use_aws_owned_key = false
  }

  # Logging
  logs = {
    general = true
    audit   = false
  }

  # Maintenance
  maintenance_window_start_time = {
    day_of_week = "SUNDAY"
    time_of_day = "03:00"
    time_zone   = "UTC"
  }

  # Auto minor version upgrade
  auto_minor_version_upgrade = var.mq_auto_minor_version_upgrade

  tags = local.common_tags
}

################################################################################
# Application Load Balancer
################################################################################

module "alb" {
  source = "../../modules/networking/alb"

  name               = "${var.project_name}-${local.environment}"
  load_balancer_type = "application"
  internal           = false

  # Network
  vpc_id  = module.vpc.vpc_id
  subnets = module.vpc.public_subnets
  security_groups = [module.security_groups.alb_security_group_id]

  # Access logs for compliance
  access_logs = {
    bucket  = aws_s3_bucket.alb_logs.id
    prefix  = "alb"
    enabled = true
  }

  # Target groups
  target_groups = [
    {
      name             = "${var.project_name}-${local.environment}-http"
      backend_protocol = "HTTP"
      backend_port     = 80
      target_type      = "ip"
      
      health_check = {
        enabled             = true
        interval            = 30
        path                = "/health"
        port                = "traffic-port"
        healthy_threshold   = 3
        unhealthy_threshold = 3
        timeout             = 6
        protocol            = "HTTP"
        matcher             = "200-299"
      }

      stickiness = {
        enabled         = true
        type            = "lb_cookie"
        cookie_duration = 86400
      }

      deregistration_delay = 30
    }
  ]

  # HTTPS listener
  https_listeners = [
    {
      port               = 443
      protocol           = "HTTPS"
      certificate_arn    = var.acm_certificate_arn
      ssl_policy         = "ELBSecurityPolicy-TLS-1-2-2017-01"
      target_group_index = 0
    }
  ]

  # HTTP listener (redirect to HTTPS)
  http_tcp_listeners = [
    {
      port     = 80
      protocol = "HTTP"
      action_type = "redirect"
      redirect = {
        port        = "443"
        protocol    = "HTTPS"
        status_code = "HTTP_301"
      }
    }
  ]

  # Drop invalid headers for security
  drop_invalid_header_fields = true
  enable_deletion_protection = var.alb_deletion_protection
  enable_http2              = true
  enable_cross_zone_load_balancing = true
  idle_timeout              = 60

  tags = local.common_tags
}

################################################################################
# WAF (Web Application Firewall)
################################################################################

resource "aws_wafv2_web_acl" "main" {
  name  = "${var.project_name}-${local.environment}-waf"
  scope = "REGIONAL"

  default_action {
    allow {}
  }

  # AWS Managed Rules
  rule {
    name     = "AWSManagedRulesCommonRuleSet"
    priority = 1

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesCommonRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWSManagedRulesCommonRuleSetMetric"
      sampled_requests_enabled   = true
    }
  }

  rule {
    name     = "AWSManagedRulesKnownBadInputsRuleSet"
    priority = 2

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesKnownBadInputsRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWSManagedRulesKnownBadInputsRuleSetMetric"
      sampled_requests_enabled   = true
    }
  }

  rule {
    name     = "AWSManagedRulesSQLiRuleSet"
    priority = 3

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesSQLiRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWSManagedRulesSQLiRuleSetMetric"
      sampled_requests_enabled   = true
    }
  }

  # Rate limiting
  rule {
    name     = "RateLimitRule"
    priority = 4

    action {
      block {}
    }

    statement {
      rate_based_statement {
        limit              = var.waf_rate_limit
        aggregate_key_type = "IP"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "RateLimitRuleMetric"
      sampled_requests_enabled   = true
    }
  }

  # Geo blocking (if needed)
  dynamic "rule" {
    for_each = length(var.waf_blocked_countries) > 0 ? [1] : []
    content {
      name     = "GeoBlockRule"
      priority = 5

      action {
        block {}
      }

      statement {
        geo_match_statement {
          country_codes = var.waf_blocked_countries
        }
      }

      visibility_config {
        cloudwatch_metrics_enabled = true
        metric_name                = "GeoBlockRuleMetric"
        sampled_requests_enabled   = true
      }
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "${var.project_name}-${local.environment}-waf"
    sampled_requests_enabled   = true
  }

  tags = local.common_tags
}

# Associate WAF with ALB
resource "aws_wafv2_web_acl_association" "main" {
  resource_arn = module.alb.lb_arn
  web_acl_arn  = aws_wafv2_web_acl.main.arn
}

################################################################################
# KMS Keys for Encryption
################################################################################

resource "aws_kms_key" "eks" {
  description             = "EKS Secret Encryption Key"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-eks"
  })
}

resource "aws_kms_alias" "eks" {
  name          = "alias/${var.project_name}-${local.environment}-eks"
  target_key_id = aws_kms_key.eks.key_id
}

resource "aws_kms_key" "ebs" {
  description             = "EBS Volume Encryption Key"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-ebs"
  })
}

resource "aws_kms_alias" "ebs" {
  name          = "alias/${var.project_name}-${local.environment}-ebs"
  target_key_id = aws_kms_key.ebs.key_id
}

resource "aws_kms_key" "rds" {
  description             = "RDS Encryption Key"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-rds"
  })
}

resource "aws_kms_alias" "rds" {
  name          = "alias/${var.project_name}-${local.environment}-rds"
  target_key_id = aws_kms_key.rds.key_id
}

resource "aws_kms_key" "elasticache" {
  description             = "ElastiCache Encryption Key"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-elasticache"
  })
}

resource "aws_kms_alias" "elasticache" {
  name          = "alias/${var.project_name}-${local.environment}-elasticache"
  target_key_id = aws_kms_key.elasticache.key_id
}

resource "aws_kms_key" "mq" {
  description             = "Amazon MQ Encryption Key"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-mq"
  })
}

resource "aws_kms_alias" "mq" {
  name          = "alias/${var.project_name}-${local.environment}-mq"
  target_key_id = aws_kms_key.mq.key_id
}

################################################################################
# S3 Buckets
################################################################################

# ALB Access Logs
resource "aws_s3_bucket" "alb_logs" {
  bucket = "${var.project_name}-${local.environment}-alb-logs-${data.aws_caller_identity.current.account_id}"

  tags = merge(local.common_tags, {
    Name = "${var.project_name}-${local.environment}-alb-logs"
  })
}

resource "aws_s3_bucket_versioning" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  rule {
    id     = "log-expiration"
    status = "Enabled"

    transition {
      days          = 90
      storage_class = "GLACIER"
    }

    expiration {
      days = 365
    }
  }
}

################################################################################
# CloudWatch Log Groups
################################################################################

resource "aws_cloudwatch_log_group" "elasticache_slow_log" {
  name              = "/aws/elasticache/${var.project_name}-${local.environment}/slow-log"
  retention_in_days = var.log_retention_days

  tags = local.common_tags
}

resource "aws_cloudwatch_log_group" "elasticache_engine_log" {
  name              = "/aws/elasticache/${var.project_name}-${local.environment}/engine-log"
  retention_in_days = var.log_retention_days

  tags = local.common_tags
}

################################################################################
# CloudTrail
################################################################################

module "cloudtrail" {
  source = "../../modules/monitoring/cloudtrail"

  name                          = "${var.project_name}-${local.environment}"
  enable_log_file_validation    = true
  is_multi_region_trail         = true
  include_global_service_events = true
  enable_logging                = true

  # S3 bucket for CloudTrail logs
  s3_bucket_name = "${var.project_name}-${local.environment}-cloudtrail-${data.aws_caller_identity.current.account_id}"

  # CloudWatch Logs integration
  cloud_watch_logs_group_arn = "${aws_cloudwatch_log_group.cloudtrail.arn}:*"
  cloud_watch_logs_role_arn  = aws_iam_role.cloudtrail_cloudwatch.arn

  # Event selectors for data events
  event_selector = [
    {
      read_write_type           = "All"
      include_management_events = true

      data_resource = [
        {
          type   = "AWS::S3::Object"
          values = ["arn:aws:s3:::${aws_s3_bucket.alb_logs.id}/*"]
        }
      ]
    }
  ]

  tags = local.common_tags
}

resource "aws_cloudwatch_log_group" "cloudtrail" {
  name              = "/aws/cloudtrail/${var.project_name}-${local.environment}"
  retention_in_days = var.log_retention_days

  tags = local.common_tags
}

################################################################################
# SNS Topics for Alerts
################################################################################

resource "aws_sns_topic" "elasticache_events" {
  name = "${var.project_name}-${local.environment}-elasticache-events"

  tags = local.common_tags
}

resource "aws_sns_topic_subscription" "elasticache_email" {
  topic_arn = aws_sns_topic.elasticache_events.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

################################################################################
# IAM Roles
################################################################################

data "aws_caller_identity" "current" {}

# VPC CNI IAM Role
resource "aws_iam_role" "vpc_cni" {
  name = "${var.project_name}-${local.environment}-vpc-cni"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRoleWithWebIdentity"
        Effect = "Allow"
        Principal = {
          Federated = module.eks.oidc_provider_arn
        }
        Condition = {
          StringEquals = {
            "${module.eks.oidc_provider}:sub" = "system:serviceaccount:kube-system:aws-node"
          }
        }
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "vpc_cni" {
  role       = aws_iam_role.vpc_cni.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy"
}

# EBS CSI IAM Role
resource "aws_iam_role" "ebs_csi" {
  name = "${var.project_name}-${local.environment}-ebs-csi"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRoleWithWebIdentity"
        Effect = "Allow"
        Principal = {
          Federated = module.eks.oidc_provider_arn
        }
        Condition = {
          StringEquals = {
            "${module.eks.oidc_provider}:sub" = "system:serviceaccount:kube-system:ebs-csi-controller-sa"
          }
        }
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "ebs_csi" {
  role       = aws_iam_role.ebs_csi.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEBSCSIDriverPolicy"
}

# RDS Enhanced Monitoring Role
resource "aws_iam_role" "rds_monitoring" {
  name = "${var.project_name}-${local.environment}-rds-monitoring"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "monitoring.rds.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "rds_monitoring" {
  role       = aws_iam_role.rds_monitoring.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
}

# CloudTrail CloudWatch Logs Role
resource "aws_iam_role" "cloudtrail_cloudwatch" {
  name = "${var.project_name}-${local.environment}-cloudtrail-cloudwatch"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "cloudtrail.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy" "cloudtrail_cloudwatch" {
  name = "${var.project_name}-${local.environment}-cloudtrail-cloudwatch"
  role = aws_iam_role.cloudtrail_cloudwatch.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Effect   = "Allow"
        Resource = "${aws_cloudwatch_log_group.cloudtrail.arn}:*"
      }
    ]
  })
}

################################################################################
# Auto Scaling Policies
################################################################################

# Cluster Autoscaler IAM Policy
resource "aws_iam_policy" "cluster_autoscaler" {
  name        = "${var.project_name}-${local.environment}-cluster-autoscaler"
  description = "Policy for Cluster Autoscaler"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "autoscaling:DescribeAutoScalingGroups",
          "autoscaling:DescribeAutoScalingInstances",
          "autoscaling:DescribeLaunchConfigurations",
          "autoscaling:DescribeScalingActivities",
          "autoscaling:DescribeTags",
          "ec2:DescribeImages",
          "ec2:DescribeInstanceTypes",
          "ec2:DescribeLaunchTemplateVersions",
          "ec2:GetInstanceTypesFromInstanceRequirements",
          "eks:DescribeNodegroup"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "autoscaling:SetDesiredCapacity",
          "autoscaling:TerminateInstanceInAutoScalingGroup"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "autoscaling:ResourceTag/k8s.io/cluster-autoscaler/${var.project_name}-${local.environment}" = "owned"
          }
        }
      }
    ]
  })

  tags = local.common_tags
}
