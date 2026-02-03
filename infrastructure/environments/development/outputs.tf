# ============================================================================
# OUTPUTS - GHOST PLATFORM DEVELOPMENT ENVIRONMENT
# ============================================================================

# ============================================================================
# COMPUTE OUTPUTS
# ============================================================================

output "k3s_instance_id" {
  description = "ID of the k3s EC2 instance"
  value       = aws_instance.k3s.id
}

output "k3s_public_ip" {
  description = "Public IP address of the k3s node"
  value       = aws_instance.k3s.public_ip
}

output "k3s_private_ip" {
  description = "Private IP address of the k3s node"
  value       = aws_instance.k3s.private_ip
}

output "ssh_command" {
  description = "SSH command to connect to the k3s node"
  value       = "ssh -i ${var.ssh_key_name != "" ? var.ssh_key_name : "${local.name_prefix}-key.pem"} ec2-user@${aws_instance.k3s.public_ip}"
}

output "ssh_private_key_path" {
  description = "Path to the SSH private key (if generated)"
  value       = var.ssh_key_name == "" ? "${path.module}/.ssh/${local.name_prefix}-key.pem" : "Using existing key: ${var.ssh_key_name}"
  sensitive   = true
}

# ============================================================================
# NETWORKING OUTPUTS
# ============================================================================

output "vpc_id" {
  description = "ID of the VPC"
  value       = aws_vpc.main.id
}

output "vpc_cidr" {
  description = "CIDR block of the VPC"
  value       = aws_vpc.main.cidr_block
}

output "public_subnet_ids" {
  description = "IDs of the public subnets"
  value       = aws_subnet.public[*].id
}

output "internet_gateway_id" {
  description = "ID of the Internet Gateway"
  value       = aws_internet_gateway.main.id
}

# ============================================================================
# DATABASE OUTPUTS
# ============================================================================

output "db_instance_id" {
  description = "ID of the RDS instance"
  value       = aws_db_instance.main.id
}

output "db_endpoint" {
  description = "Connection endpoint for the RDS instance"
  value       = aws_db_instance.main.endpoint
}

output "db_address" {
  description = "Hostname of the RDS instance"
  value       = aws_db_instance.main.address
}

output "db_port" {
  description = "Port of the RDS instance"
  value       = aws_db_instance.main.port
}

output "db_name" {
  description = "Name of the database"
  value       = aws_db_instance.main.db_name
}

output "db_username" {
  description = "Master username for the database"
  value       = var.db_username
  sensitive   = true
}

output "db_password" {
  description = "Master password for the database"
  value       = random_password.db_password.result
  sensitive   = true
}

output "db_connection_string" {
  description = "PostgreSQL connection string"
  value       = "postgresql://${var.db_username}:${random_password.db_password.result}@${aws_db_instance.main.endpoint}/${var.db_name}"
  sensitive   = true
}

# ============================================================================
# CACHE OUTPUTS
# ============================================================================

output "redis_cluster_id" {
  description = "ID of the ElastiCache Redis cluster"
  value       = aws_elasticache_cluster.main.id
}

output "redis_endpoint" {
  description = "Endpoint of the Redis cluster"
  value       = aws_elasticache_cluster.main.cache_nodes[0].address
}

output "redis_port" {
  description = "Port of the Redis cluster"
  value       = aws_elasticache_cluster.main.cache_nodes[0].port
}

output "redis_connection_string" {
  description = "Redis connection string"
  value       = "redis://${aws_elasticache_cluster.main.cache_nodes[0].address}:${aws_elasticache_cluster.main.cache_nodes[0].port}"
}

# ============================================================================
# MESSAGING OUTPUTS
# ============================================================================

output "mq_broker_id" {
  description = "ID of the Amazon MQ broker (if used)"
  value       = var.use_self_hosted_rabbitmq ? null : aws_mq_broker.main[0].id
}

output "mq_console_url" {
  description = "URL of the Amazon MQ web console (if used)"
  value       = var.use_self_hosted_rabbitmq ? "Self-hosted RabbitMQ on k3s" : aws_mq_broker.main[0].instances[0].console_url
}

output "mq_endpoint" {
  description = "AMQP endpoint of the Amazon MQ broker (if used)"
  value       = var.use_self_hosted_rabbitmq ? "localhost:5672" : aws_mq_broker.main[0].instances[0].endpoints[0]
}

output "mq_username" {
  description = "Username for RabbitMQ"
  value       = var.mq_username
  sensitive   = true
}

output "mq_password" {
  description = "Password for RabbitMQ"
  value       = random_password.mq_password.result
  sensitive   = true
}

# ============================================================================
# SECURITY OUTPUTS
# ============================================================================

output "k3s_security_group_id" {
  description = "ID of the k3s security group"
  value       = aws_security_group.k3s.id
}

output "rds_security_group_id" {
  description = "ID of the RDS security group"
  value       = aws_security_group.rds.id
}

output "redis_security_group_id" {
  description = "ID of the Redis security group"
  value       = aws_security_group.redis.id
}

output "mq_security_group_id" {
  description = "ID of the Amazon MQ security group (if used)"
  value       = var.use_self_hosted_rabbitmq ? null : aws_security_group.mq[0].id
}

# ============================================================================
# IAM OUTPUTS
# ============================================================================

output "k3s_iam_role_arn" {
  description = "ARN of the k3s IAM role"
  value       = aws_iam_role.k3s.arn
}

output "k3s_instance_profile_arn" {
  description = "ARN of the k3s instance profile"
  value       = aws_iam_instance_profile.k3s.arn
}

# ============================================================================
# MONITORING OUTPUTS
# ============================================================================

output "cloudwatch_log_group_name" {
  description = "Name of the CloudWatch log group (if enabled)"
  value       = var.enable_cloudwatch_logs ? aws_cloudwatch_log_group.k3s[0].name : "CloudWatch logs not enabled"
}

# ============================================================================
# AUTO-SHUTDOWN OUTPUTS
# ============================================================================

output "auto_shutdown_enabled" {
  description = "Whether auto-shutdown is enabled"
  value       = var.enable_auto_shutdown
}

output "shutdown_schedule" {
  description = "Cron expression for auto-shutdown"
  value       = var.enable_auto_shutdown ? var.auto_shutdown_schedule : "Not configured"
}

output "startup_schedule" {
  description = "Cron expression for auto-startup"
  value       = var.enable_auto_shutdown ? var.auto_startup_schedule : "Not configured"
}

# ============================================================================
# COST ESTIMATION
# ============================================================================

output "estimated_monthly_cost" {
  description = "Estimated monthly cost in USD (approximate)"
  value = <<-EOT
    Estimated monthly cost breakdown (approximate):
    - k3s EC2 instance (${var.instance_type}, ${var.use_spot_instances ? "spot" : "on-demand"}): $${var.use_spot_instances ? "8-12" : "15-18"}
    - RDS PostgreSQL (${var.db_instance_class}): $12-15
    - ElastiCache Redis (${var.redis_node_type}): $12-15
    - Amazon MQ (${var.use_self_hosted_rabbitmq ? "self-hosted" : var.mq_instance_type}): $${var.use_self_hosted_rabbitmq ? "0" : "30-35"}
    - Data transfer & storage: $5-10
    - CloudWatch logs: $2-5
    ----------------------------------------
    Total estimated: $${var.use_self_hosted_rabbitmq ? "40-60" : "70-90"}/month
    
    Note: Costs can be further reduced with:
    - Auto-shutdown during off-hours (enabled: ${var.enable_auto_shutdown})
    - Reserved instances for production
    - Savings Plans
  EOT
}

# ============================================================================
# CONNECTION INFORMATION
# ============================================================================

output "connection_info" {
  description = "Connection information for all services"
  value = {
    k3s = {
      public_ip  = aws_instance.k3s.public_ip
      private_ip = aws_instance.k3s.private_ip
      ssh        = "ssh -i ${var.ssh_key_name != "" ? var.ssh_key_name : "${local.name_prefix}-key.pem"} ec2-user@${aws_instance.k3s.public_ip}"
      kubeconfig = "Run on server: sudo cat /etc/rancher/k3s/k3s.yaml"
    }
    database = {
      host     = aws_db_instance.main.address
      port     = aws_db_instance.main.port
      name     = aws_db_instance.main.db_name
      username = var.db_username
    }
    redis = {
      host = aws_elasticache_cluster.main.cache_nodes[0].address
      port = aws_elasticache_cluster.main.cache_nodes[0].port
    }
    rabbitmq = {
      type     = var.use_self_hosted_rabbitmq ? "self-hosted" : "amazon-mq"
      endpoint = var.use_self_hosted_rabbitmq ? "localhost:5672" : aws_mq_broker.main[0].instances[0].endpoints[0]
      console  = var.use_self_hosted_rabbitmq ? "http://localhost:15672" : aws_mq_broker.main[0].instances[0].console_url
      username = var.mq_username
    }
  }
  sensitive = true
}

# ============================================================================
# GETTING STARTED
# ============================================================================

output "getting_started" {
  description = "Quick start instructions"
  value = <<-EOT
    🚀 Ghost Platform Development Environment Ready!
    
    1. Connect to k3s node:
       ${ssh_command.value}
    
    2. Get kubeconfig:
       sudo cat /etc/rancher/k3s/k3s.yaml > kubeconfig.yaml
       # Replace 127.0.0.1 with ${aws_instance.k3s.public_ip} in kubeconfig.yaml
    
    3. Set KUBECONFIG locally:
       export KUBECONFIG=./kubeconfig.yaml
       kubectl get nodes
    
    4. Deploy Ghost Platform:
       kubectl apply -k ../../platform/base
       kubectl apply -k ../../platform/services
    
    5. Access services:
       - Ghost: http://${aws_instance.k3s.public_ip}
       - RabbitMQ Console: ${var.use_self_hosted_rabbitmq ? "http://localhost:15672" : aws_mq_broker.main[0].instances[0].console_url}
    
    6. View sensitive outputs:
       terraform output -json | jq '.connection_info.value'
       terraform output db_password
    
    📝 Important Notes:
    - Auto-shutdown is ${var.enable_auto_shutdown ? "ENABLED" : "DISABLED"}
    - Using ${var.use_spot_instances ? "SPOT" : "ON-DEMAND"} instances
    - Estimated monthly cost: $${var.use_self_hosted_rabbitmq ? "40-60" : "70-90"}
    - This is a DEVELOPMENT environment - not production-ready!
    
    📚 Documentation: See infrastructure/docs/
  EOT
}

# ============================================================================
# AWS ACCOUNT INFORMATION
# ============================================================================

output "aws_account_id" {
  description = "AWS account ID"
  value       = data.aws_caller_identity.current.account_id
}

output "aws_region" {
  description = "AWS region"
  value       = data.aws_region.current.name
}

# ============================================================================
# TAGS
# ============================================================================

output "common_tags" {
  description = "Common tags applied to all resources"
  value       = local.common_tags
}
