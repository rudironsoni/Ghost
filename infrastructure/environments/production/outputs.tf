# Production Environment Outputs

################################################################################
# VPC Outputs
################################################################################

output "vpc_id" {
  description = "ID of the VPC"
  value       = module.vpc.vpc_id
}

output "vpc_cidr" {
  description = "CIDR block of the VPC"
  value       = module.vpc.vpc_cidr_block
}

output "private_subnet_ids" {
  description = "IDs of private subnets"
  value       = module.vpc.private_subnets
}

output "public_subnet_ids" {
  description = "IDs of public subnets"
  value       = module.vpc.public_subnets
}

output "database_subnet_ids" {
  description = "IDs of database subnets"
  value       = module.vpc.database_subnets
}

output "nat_gateway_ips" {
  description = "Elastic IPs of NAT Gateways"
  value       = module.vpc.nat_public_ips
}

################################################################################
# EKS Outputs
################################################################################

output "eks_cluster_id" {
  description = "ID of the EKS cluster"
  value       = module.eks.cluster_id
}

output "eks_cluster_arn" {
  description = "ARN of the EKS cluster"
  value       = module.eks.cluster_arn
}

output "eks_cluster_endpoint" {
  description = "Endpoint for EKS control plane"
  value       = module.eks.cluster_endpoint
  sensitive   = true
}

output "eks_cluster_version" {
  description = "Kubernetes version of the EKS cluster"
  value       = module.eks.cluster_version
}

output "eks_cluster_security_group_id" {
  description = "Security group ID attached to the EKS cluster"
  value       = module.eks.cluster_security_group_id
}

output "eks_oidc_provider_arn" {
  description = "ARN of the OIDC Provider for EKS"
  value       = module.eks.oidc_provider_arn
}

output "eks_cluster_certificate_authority_data" {
  description = "Base64 encoded certificate data for cluster authentication"
  value       = module.eks.cluster_certificate_authority_data
  sensitive   = true
}

output "eks_node_group_ids" {
  description = "IDs of EKS node groups"
  value       = module.eks.node_group_ids
}

output "eks_cluster_autoscaler_role_arn" {
  description = "IAM role ARN for Cluster Autoscaler"
  value       = aws_iam_policy.cluster_autoscaler.arn
}

# Kubectl configuration command
output "eks_configure_kubectl" {
  description = "Command to configure kubectl"
  value       = "aws eks update-kubeconfig --region ${data.aws_region.current.name} --name ${module.eks.cluster_id}"
}

################################################################################
# RDS Outputs
################################################################################

output "rds_instance_id" {
  description = "ID of the RDS instance"
  value       = module.rds.db_instance_id
}

output "rds_instance_arn" {
  description = "ARN of the RDS instance"
  value       = module.rds.db_instance_arn
}

output "rds_endpoint" {
  description = "Connection endpoint for RDS"
  value       = module.rds.db_instance_endpoint
  sensitive   = true
}

output "rds_reader_endpoint" {
  description = "Reader endpoint for RDS read replica"
  value       = module.rds_replica.db_instance_endpoint
  sensitive   = true
}

output "rds_database_name" {
  description = "Name of the database"
  value       = module.rds.db_instance_name
}

output "rds_port" {
  description = "Port of the RDS instance"
  value       = module.rds.db_instance_port
}

output "rds_master_username" {
  description = "Master username for the database"
  value       = module.rds.db_instance_username
  sensitive   = true
}

output "rds_security_group_id" {
  description = "Security group ID for RDS"
  value       = module.security_groups.rds_security_group_id
}

output "rds_kms_key_id" {
  description = "KMS key ID used for RDS encryption"
  value       = aws_kms_key.rds.id
}

################################################################################
# ElastiCache Redis Outputs
################################################################################

output "elasticache_cluster_id" {
  description = "ID of the ElastiCache cluster"
  value       = module.elasticache.cluster_id
}

output "elasticache_cluster_arn" {
  description = "ARN of the ElastiCache cluster"
  value       = module.elasticache.cluster_arn
}

output "elasticache_configuration_endpoint" {
  description = "Configuration endpoint for Redis cluster"
  value       = module.elasticache.configuration_endpoint
  sensitive   = true
}

output "elasticache_reader_endpoint" {
  description = "Reader endpoint for Redis cluster"
  value       = module.elasticache.reader_endpoint
  sensitive   = true
}

output "elasticache_port" {
  description = "Port for Redis connection"
  value       = module.elasticache.port
}

output "elasticache_security_group_id" {
  description = "Security group ID for ElastiCache"
  value       = module.security_groups.elasticache_security_group_id
}

################################################################################
# Amazon MQ Outputs
################################################################################

output "mq_broker_id" {
  description = "ID of the Amazon MQ broker"
  value       = module.amazon_mq.broker_id
}

output "mq_broker_arn" {
  description = "ARN of the Amazon MQ broker"
  value       = module.amazon_mq.broker_arn
}

output "mq_console_url" {
  description = "URL of the RabbitMQ management console"
  value       = module.amazon_mq.console_url
  sensitive   = true
}

output "mq_endpoints" {
  description = "Connection endpoints for RabbitMQ"
  value       = module.amazon_mq.endpoints
  sensitive   = true
}

output "mq_security_group_id" {
  description = "Security group ID for Amazon MQ"
  value       = module.security_groups.mq_security_group_id
}

################################################################################
# ALB Outputs
################################################################################

output "alb_id" {
  description = "ID of the Application Load Balancer"
  value       = module.alb.lb_id
}

output "alb_arn" {
  description = "ARN of the Application Load Balancer"
  value       = module.alb.lb_arn
}

output "alb_dns_name" {
  description = "DNS name of the Application Load Balancer"
  value       = module.alb.lb_dns_name
}

output "alb_zone_id" {
  description = "Zone ID of the Application Load Balancer"
  value       = module.alb.lb_zone_id
}

output "alb_target_group_arns" {
  description = "ARNs of ALB target groups"
  value       = module.alb.target_group_arns
}

output "alb_security_group_id" {
  description = "Security group ID for ALB"
  value       = module.security_groups.alb_security_group_id
}

################################################################################
# WAF Outputs
################################################################################

output "waf_web_acl_id" {
  description = "ID of the WAF Web ACL"
  value       = aws_wafv2_web_acl.main.id
}

output "waf_web_acl_arn" {
  description = "ARN of the WAF Web ACL"
  value       = aws_wafv2_web_acl.main.arn
}

output "waf_web_acl_capacity" {
  description = "Web ACL capacity units used"
  value       = aws_wafv2_web_acl.main.capacity
}

################################################################################
# KMS Keys
################################################################################

output "kms_eks_key_id" {
  description = "KMS key ID for EKS encryption"
  value       = aws_kms_key.eks.id
}

output "kms_eks_key_arn" {
  description = "KMS key ARN for EKS encryption"
  value       = aws_kms_key.eks.arn
}

output "kms_rds_key_id" {
  description = "KMS key ID for RDS encryption"
  value       = aws_kms_key.rds.id
}

output "kms_rds_key_arn" {
  description = "KMS key ARN for RDS encryption"
  value       = aws_kms_key.rds.arn
}

output "kms_elasticache_key_id" {
  description = "KMS key ID for ElastiCache encryption"
  value       = aws_kms_key.elasticache.id
}

output "kms_elasticache_key_arn" {
  description = "KMS key ARN for ElastiCache encryption"
  value       = aws_kms_key.elasticache.arn
}

################################################################################
# S3 Buckets
################################################################################

output "alb_logs_bucket_id" {
  description = "ID of S3 bucket for ALB logs"
  value       = aws_s3_bucket.alb_logs.id
}

output "alb_logs_bucket_arn" {
  description = "ARN of S3 bucket for ALB logs"
  value       = aws_s3_bucket.alb_logs.arn
}

################################################################################
# CloudWatch
################################################################################

output "cloudwatch_log_group_elasticache_slow_log" {
  description = "CloudWatch log group for ElastiCache slow logs"
  value       = aws_cloudwatch_log_group.elasticache_slow_log.name
}

output "cloudwatch_log_group_elasticache_engine_log" {
  description = "CloudWatch log group for ElastiCache engine logs"
  value       = aws_cloudwatch_log_group.elasticache_engine_log.name
}

output "cloudwatch_log_group_cloudtrail" {
  description = "CloudWatch log group for CloudTrail"
  value       = aws_cloudwatch_log_group.cloudtrail.name
}

################################################################################
# CloudTrail
################################################################################

output "cloudtrail_id" {
  description = "ID of CloudTrail"
  value       = module.cloudtrail.trail_id
}

output "cloudtrail_arn" {
  description = "ARN of CloudTrail"
  value       = module.cloudtrail.trail_arn
}

################################################################################
# SNS Topics
################################################################################

output "sns_elasticache_events_topic_arn" {
  description = "ARN of SNS topic for ElastiCache events"
  value       = aws_sns_topic.elasticache_events.arn
}

################################################################################
# IAM Roles
################################################################################

output "iam_role_vpc_cni_arn" {
  description = "ARN of IAM role for VPC CNI"
  value       = aws_iam_role.vpc_cni.arn
}

output "iam_role_ebs_csi_arn" {
  description = "ARN of IAM role for EBS CSI driver"
  value       = aws_iam_role.ebs_csi.arn
}

output "iam_role_rds_monitoring_arn" {
  description = "ARN of IAM role for RDS enhanced monitoring"
  value       = aws_iam_role.rds_monitoring.arn
}

################################################################################
# Metadata
################################################################################

output "environment" {
  description = "Environment name"
  value       = local.environment
}

output "region" {
  description = "AWS region"
  value       = data.aws_region.current.name
}

output "account_id" {
  description = "AWS account ID"
  value       = data.aws_caller_identity.current.account_id
}

output "common_tags" {
  description = "Common tags applied to all resources"
  value       = local.common_tags
}

################################################################################
# Connection Strings (for application configuration)
################################################################################

output "connection_info" {
  description = "Connection information for services (use with caution - contains sensitive data)"
  value = {
    postgres_primary = "postgresql://${module.rds.db_instance_username}@${module.rds.db_instance_endpoint}/${module.rds.db_instance_name}"
    postgres_replica = "postgresql://${module.rds.db_instance_username}@${module.rds_replica.db_instance_endpoint}/${module.rds.db_instance_name}"
    redis_cluster    = module.elasticache.configuration_endpoint
    rabbitmq_amqp    = module.amazon_mq.endpoints.amqp
    rabbitmq_console = module.amazon_mq.console_url
  }
  sensitive = true
}

################################################################################
# Cost Estimation Summary
################################################################################

output "cost_estimation_monthly" {
  description = "Estimated monthly costs (USD, on-demand pricing)"
  value = {
    eks_nodes         = "~$1,500 (5x r6i.xlarge)"
    rds_primary       = "~$800 (db.r6i.large Multi-AZ)"
    elasticache_redis = "~$1,200 (9 nodes cache.r7g.large)"
    amazon_mq         = "~$600 (mq.m5.large Multi-AZ)"
    alb_data_transfer = "~$300"
    total_on_demand   = "~$4,400/month"
    total_with_ri     = "~$2,640/month (40% savings with 1-year RIs)"
    note              = "Actual costs may vary based on usage, data transfer, and region"
  }
}

################################################################################
# Next Steps
################################################################################

output "next_steps" {
  description = "Next steps after infrastructure deployment"
  value = <<-EOT
    1. Configure kubectl:
       ${module.eks.cluster_id != "" ? "aws eks update-kubeconfig --region ${data.aws_region.current.name} --name ${module.eks.cluster_id}" : ""}
    
    2. Retrieve database password from AWS Secrets Manager
    
    3. Deploy Kubernetes applications:
       - Install Cluster Autoscaler
       - Install AWS Load Balancer Controller
       - Deploy Ghost application
       - Configure Ingress
    
    4. Configure DNS:
       - Create Route53 hosted zone
       - Point domain to ALB: ${module.alb.lb_dns_name}
    
    5. Set up monitoring:
       - Configure CloudWatch dashboards
       - Set up alerts for critical metrics
       - Enable Container Insights for EKS
    
    6. Security hardening:
       - Rotate database passwords
       - Configure pod security policies
       - Enable GuardDuty
       - Review Security Hub findings
    
    7. Cost optimization:
       - Purchase Reserved Instances
       - Set up billing alerts
       - Review Trusted Advisor recommendations
    
    8. Disaster Recovery:
       - Document recovery procedures
       - Schedule DR testing
       - Verify backup restoration
  EOT
}

################################################################################
# Data Sources
################################################################################

data "aws_region" "current" {}
