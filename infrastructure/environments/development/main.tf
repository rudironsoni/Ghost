# ============================================================================
# MAIN TERRAFORM CONFIGURATION - GHOST PLATFORM DEVELOPMENT ENVIRONMENT
# ============================================================================
# This configuration deploys a cost-optimized development environment for
# the Ghost Platform on AWS with the following components:
# - VPC with public subnets only (no NAT gateway for cost savings)
# - Single k3s node on spot instance (t3.small)
# - RDS PostgreSQL (db.t3.micro)
# - ElastiCache Redis (cache.t3.micro)
# - Self-hosted RabbitMQ on k3s (or optional Amazon MQ)
# - Automated shutdown/startup schedule
# - Cost allocation tags
#
# Estimated cost: ~$50/month
# ============================================================================

# ============================================================================
# PROVIDER CONFIGURATION
# ============================================================================

provider "aws" {
  region  = var.aws_region
  profile = var.aws_profile

  default_tags {
    tags = local.common_tags
  }
}

# ============================================================================
# LOCAL VARIABLES
# ============================================================================

locals {
  name_prefix = "${var.project_name}-${var.environment}"

  common_tags = merge(
    {
      Environment  = var.environment
      Project      = var.project_name
      ManagedBy    = "Terraform"
      CostCenter   = var.cost_center
      Team         = var.team
      Owner        = var.owner
      AutoShutdown = var.enable_auto_shutdown ? "true" : "false"
      GitRepo      = "rudironsoni/Ghost"
    },
    var.additional_tags
  )

  # Select appropriate availability zones
  azs = slice(var.availability_zones, 0, var.enable_multi_az ? 2 : 1)
}

# ============================================================================
# DATA SOURCES
# ============================================================================

# Get latest Amazon Linux 2 AMI
data "aws_ami" "amazon_linux_2" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["amzn2-ami-hvm-*-x86_64-gp2"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# Get current AWS account ID and caller identity
data "aws_caller_identity" "current" {}

data "aws_region" "current" {}

# ============================================================================
# RANDOM RESOURCES FOR PASSWORDS
# ============================================================================

resource "random_password" "db_password" {
  length  = 32
  special = true
  # Exclude characters that might cause issues in connection strings
  override_special = "!#$%&*()-_=+[]{}<>?"
}

resource "random_password" "mq_password" {
  length  = 32
  special = true
  override_special = "!#$%&*()-_=+[]{}<>?"
}

# ============================================================================
# SSH KEY PAIR
# ============================================================================

resource "tls_private_key" "ssh" {
  count = var.ssh_key_name == "" ? 1 : 0

  algorithm = "RSA"
  rsa_bits  = 4096
}

resource "aws_key_pair" "main" {
  count = var.ssh_key_name == "" ? 1 : 0

  key_name   = "${local.name_prefix}-key"
  public_key = tls_private_key.ssh[0].public_key_openssh

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-key"
  })
}

# Save private key locally (for development only)
resource "local_sensitive_file" "ssh_private_key" {
  count = var.ssh_key_name == "" ? 1 : 0

  content         = tls_private_key.ssh[0].private_key_pem
  filename        = "${path.module}/.ssh/${local.name_prefix}-key.pem"
  file_permission = "0600"
}

# ============================================================================
# NETWORKING - VPC
# ============================================================================

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-vpc"
  })
}

# Internet Gateway
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-igw"
  })
}

# Public Subnets
resource "aws_subnet" "public" {
  count = length(local.azs)

  vpc_id                  = aws_vpc.main.id
  cidr_block              = var.public_subnet_cidrs[count.index]
  availability_zone       = local.azs[count.index]
  map_public_ip_on_launch = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-public-${local.azs[count.index]}"
    Tier = "Public"
  })
}

# Route Table for Public Subnets
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-public-rt"
  })
}

# Associate Route Table with Public Subnets
resource "aws_route_table_association" "public" {
  count = length(aws_subnet.public)

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# ============================================================================
# SECURITY GROUPS
# ============================================================================

# Security Group for k3s node
resource "aws_security_group" "k3s" {
  name_description = "${local.name_prefix}-k3s-sg"
  description      = "Security group for k3s node"
  vpc_id           = aws_vpc.main.id

  # SSH access
  ingress {
    description = "SSH from allowed CIDR blocks"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.allowed_ssh_cidr_blocks
  }

  # HTTP
  ingress {
    description = "HTTP from allowed CIDR blocks"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = var.allowed_http_cidr_blocks
  }

  # HTTPS
  ingress {
    description = "HTTPS from allowed CIDR blocks"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = var.allowed_http_cidr_blocks
  }

  # k3s API server
  ingress {
    description = "k3s API server"
    from_port   = 6443
    to_port     = 6443
    protocol    = "tcp"
    cidr_blocks = var.allowed_ssh_cidr_blocks
  }

  # Allow all outbound traffic
  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-k3s-sg"
  })
}

# Security Group for RDS
resource "aws_security_group" "rds" {
  name_prefix = "${local.name_prefix}-rds-sg"
  description = "Security group for RDS PostgreSQL"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "PostgreSQL from k3s"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.k3s.id]
  }

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-rds-sg"
  })
}

# Security Group for Redis
resource "aws_security_group" "redis" {
  name_prefix = "${local.name_prefix}-redis-sg"
  description = "Security group for ElastiCache Redis"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "Redis from k3s"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [aws_security_group.k3s.id]
  }

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-redis-sg"
  })
}

# Security Group for Amazon MQ (if used)
resource "aws_security_group" "mq" {
  count = var.use_self_hosted_rabbitmq ? 0 : 1

  name_prefix = "${local.name_prefix}-mq-sg"
  description = "Security group for Amazon MQ"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "AMQP from k3s"
    from_port       = 5671
    to_port         = 5671
    protocol        = "tcp"
    security_groups = [aws_security_group.k3s.id]
  }

  ingress {
    description     = "Web console from k3s"
    from_port       = 443
    to_port         = 443
    protocol        = "tcp"
    security_groups = [aws_security_group.k3s.id]
  }

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-mq-sg"
  })
}

# ============================================================================
# IAM ROLE FOR EC2 INSTANCE
# ============================================================================

resource "aws_iam_role" "k3s" {
  name               = "${local.name_prefix}-k3s-role"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ec2.amazonaws.com"
      }
    }]
  })

  tags = local.common_tags
}

# Attach policies for CloudWatch, SSM, and ECR
resource "aws_iam_role_policy_attachment" "k3s_cloudwatch" {
  role       = aws_iam_role.k3s.name
  policy_arn = "arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy"
}

resource "aws_iam_role_policy_attachment" "k3s_ssm" {
  role       = aws_iam_role.k3s.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_role_policy_attachment" "k3s_ecr" {
  role       = aws_iam_role.k3s.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
}

resource "aws_iam_instance_profile" "k3s" {
  name = "${local.name_prefix}-k3s-profile"
  role = aws_iam_role.k3s.name

  tags = local.common_tags
}

# ============================================================================
# CLOUD-INIT USER DATA
# ============================================================================

data "cloudinit_config" "k3s" {
  gzip          = true
  base64_encode = true

  part {
    content_type = "text/cloud-config"
    content = templatefile("${path.module}/user-data.yaml", {
      db_host     = aws_db_instance.main.address
      db_port     = aws_db_instance.main.port
      db_name     = var.db_name
      db_username = var.db_username
      db_password = random_password.db_password.result
      redis_host  = aws_elasticache_cluster.main.cache_nodes[0].address
      redis_port  = aws_elasticache_cluster.main.cache_nodes[0].port
      mq_host     = var.use_self_hosted_rabbitmq ? "localhost" : aws_mq_broker.main[0].instances[0].endpoints[0]
      mq_username = var.mq_username
      mq_password = random_password.mq_password.result
      environment = var.environment
      project     = var.project_name
    })
  }
}

# ============================================================================
# COMPUTE - K3S NODE
# ============================================================================

resource "aws_launch_template" "k3s" {
  name_prefix   = "${local.name_prefix}-k3s-"
  image_id      = data.aws_ami.amazon_linux_2.id
  instance_type = var.instance_type
  key_name      = var.ssh_key_name != "" ? var.ssh_key_name : aws_key_pair.main[0].key_name
  user_data     = data.cloudinit_config.k3s.rendered

  iam_instance_profile {
    name = aws_iam_instance_profile.k3s.name
  }

  vpc_security_group_ids = [aws_security_group.k3s.id]

  block_device_mappings {
    device_name = "/dev/xvda"

    ebs {
      volume_size           = var.disk_size_gb
      volume_type           = "gp3"
      delete_on_termination = true
      encrypted             = var.enable_encryption_at_rest
    }
  }

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
    instance_metadata_tags      = "enabled"
  }

  monitoring {
    enabled = var.enable_detailed_monitoring
  }

  # Spot instance configuration
  instance_market_options {
    market_type = var.use_spot_instances ? "spot" : null

    dynamic "spot_options" {
      for_each = var.use_spot_instances ? [1] : []
      content {
        max_price          = var.spot_max_price != "" ? var.spot_max_price : null
        spot_instance_type = "one-time"
      }
    }
  }

  tag_specifications {
    resource_type = "instance"
    tags = merge(local.common_tags, {
      Name = "${local.name_prefix}-k3s"
      Role = "k3s-server"
    })
  }

  tag_specifications {
    resource_type = "volume"
    tags = merge(local.common_tags, {
      Name = "${local.name_prefix}-k3s-volume"
    })
  }
}

resource "aws_instance" "k3s" {
  launch_template {
    id      = aws_launch_template.k3s.id
    version = "$Latest"
  }

  subnet_id = aws_subnet.public[0].id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-k3s"
  })
}

# ============================================================================
# DATABASE - RDS POSTGRESQL
# ============================================================================

# DB Subnet Group
resource "aws_db_subnet_group" "main" {
  name       = "${local.name_prefix}-db-subnet"
  subnet_ids = aws_subnet.public[*].id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-db-subnet"
  })
}

# RDS Instance
resource "aws_db_instance" "main" {
  identifier = "${local.name_prefix}-db"

  # Engine
  engine         = "postgres"
  engine_version = var.db_engine_version

  # Instance
  instance_class = var.db_instance_class

  # Storage
  allocated_storage     = var.db_allocated_storage
  max_allocated_storage = var.db_max_allocated_storage
  storage_type          = "gp3"
  storage_encrypted     = var.enable_encryption_at_rest

  # Database
  db_name  = var.db_name
  username = var.db_username
  password = random_password.db_password.result

  # Network
  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = false

  # Backup
  backup_retention_period   = var.db_backup_retention_days
  backup_window             = "03:00-04:00"
  maintenance_window        = "Mon:04:00-Mon:05:00"
  skip_final_snapshot       = var.db_skip_final_snapshot
  final_snapshot_identifier = var.db_skip_final_snapshot ? null : "${local.name_prefix}-db-final-snapshot-${formatdate("YYYY-MM-DD-hhmm", timestamp())}"

  # Monitoring
  enabled_cloudwatch_logs_exports = var.enable_cloudwatch_logs ? ["postgresql", "upgrade"] : []
  performance_insights_enabled    = false # Disabled for cost savings

  # Options
  auto_minor_version_upgrade = true
  deletion_protection        = false # Set to true for production
  copy_tags_to_snapshot      = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-db"
  })
}

# ============================================================================
# CACHE - ELASTICACHE REDIS
# ============================================================================

# ElastiCache Subnet Group
resource "aws_elasticache_subnet_group" "main" {
  name       = "${local.name_prefix}-cache-subnet"
  subnet_ids = aws_subnet.public[*].id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-cache-subnet"
  })
}

# ElastiCache Redis Cluster
resource "aws_elasticache_cluster" "main" {
  cluster_id      = "${local.name_prefix}-redis"
  engine          = "redis"
  engine_version  = var.redis_engine_version
  node_type       = var.redis_node_type
  num_cache_nodes = var.redis_num_cache_nodes
  port            = 6379

  parameter_group_name = aws_elasticache_parameter_group.main.name
  subnet_group_name    = aws_elasticache_subnet_group.main.name
  security_group_ids   = [aws_security_group.redis.id]

  snapshot_retention_limit = 5
  snapshot_window          = "03:00-04:00"
  maintenance_window       = "Mon:04:00-Mon:05:00"

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-redis"
  })
}

# ElastiCache Parameter Group
resource "aws_elasticache_parameter_group" "main" {
  name   = "${local.name_prefix}-redis-params"
  family = var.redis_parameter_group_family

  # Optimize for development (not production-grade)
  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-redis-params"
  })
}

# ============================================================================
# MESSAGING - AMAZON MQ (RABBITMQ) - OPTIONAL
# ============================================================================

resource "aws_mq_broker" "main" {
  count = var.use_self_hosted_rabbitmq ? 0 : 1

  broker_name        = "${local.name_prefix}-mq"
  engine_type        = "RabbitMQ"
  engine_version     = var.mq_engine_version
  host_instance_type = var.mq_instance_type
  deployment_mode    = var.mq_deployment_mode

  user {
    username = var.mq_username
    password = random_password.mq_password.result
  }

  subnet_ids         = var.mq_deployment_mode == "SINGLE_INSTANCE" ? [aws_subnet.public[0].id] : aws_subnet.public[*].id
  security_groups    = [aws_security_group.mq[0].id]
  publicly_accessible = false

  logs {
    general = var.enable_cloudwatch_logs
  }

  encryption_options {
    use_aws_owned_key = true
  }

  auto_minor_version_upgrade = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-mq"
  })
}

# ============================================================================
# CLOUDWATCH - LOGS
# ============================================================================

resource "aws_cloudwatch_log_group" "k3s" {
  count = var.enable_cloudwatch_logs ? 1 : 0

  name              = "/aws/ec2/${local.name_prefix}/k3s"
  retention_in_days = var.log_retention_days

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-k3s-logs"
  })
}

# ============================================================================
# AUTO SHUTDOWN/STARTUP SCHEDULE (OPTIONAL)
# ============================================================================

# EventBridge rule for shutdown
resource "aws_cloudwatch_event_rule" "shutdown" {
  count = var.enable_auto_shutdown ? 1 : 0

  name                = "${local.name_prefix}-auto-shutdown"
  description         = "Trigger auto-shutdown for development environment"
  schedule_expression = "cron(${var.auto_shutdown_schedule})"

  tags = local.common_tags
}

resource "aws_cloudwatch_event_target" "shutdown" {
  count = var.enable_auto_shutdown ? 1 : 0

  rule      = aws_cloudwatch_event_rule.shutdown[0].name
  target_id = "StopEC2Instances"
  arn       = "arn:aws:automation:::action:Stop/EC2Instances"
  role_arn  = aws_iam_role.scheduler[0].arn

  input = jsonencode({
    InstanceIds = [aws_instance.k3s.id]
  })
}

# EventBridge rule for startup
resource "aws_cloudwatch_event_rule" "startup" {
  count = var.enable_auto_shutdown ? 1 : 0

  name                = "${local.name_prefix}-auto-startup"
  description         = "Trigger auto-startup for development environment"
  schedule_expression = "cron(${var.auto_startup_schedule})"

  tags = local.common_tags
}

resource "aws_cloudwatch_event_target" "startup" {
  count = var.enable_auto_shutdown ? 1 : 0

  rule      = aws_cloudwatch_event_rule.startup[0].name
  target_id = "StartEC2Instances"
  arn       = "arn:aws:automation:::action:Start/EC2Instances"
  role_arn  = aws_iam_role.scheduler[0].arn

  input = jsonencode({
    InstanceIds = [aws_instance.k3s.id]
  })
}

# IAM role for EventBridge scheduler
resource "aws_iam_role" "scheduler" {
  count = var.enable_auto_shutdown ? 1 : 0

  name = "${local.name_prefix}-scheduler-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "events.amazonaws.com"
      }
    }]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy" "scheduler" {
  count = var.enable_auto_shutdown ? 1 : 0

  name = "${local.name_prefix}-scheduler-policy"
  role = aws_iam_role.scheduler[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "ec2:StartInstances",
        "ec2:StopInstances"
      ]
      Resource = "arn:aws:ec2:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:instance/${aws_instance.k3s.id}"
    }]
  })
}

# ============================================================================
# NOTES AND ALTERNATIVES
# ============================================================================

# GCP Alternative Architecture:
# - Use GKE Autopilot or single-node GKE cluster
# - Cloud SQL for PostgreSQL
# - Memorystore for Redis
# - CloudAMQP or self-hosted RabbitMQ
# - Cloud Scheduler for auto-shutdown
# - Estimated cost: ~$60/month

# Azure Alternative Architecture:
# - Use AKS with single-node pool
# - Azure Database for PostgreSQL - Flexible Server
# - Azure Cache for Redis (Basic tier)
# - Azure Service Bus or self-hosted RabbitMQ
# - Azure Automation for auto-shutdown
# - Estimated cost: ~$70/month
