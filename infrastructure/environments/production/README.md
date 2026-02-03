# Production Environment - Terraform Configuration

This directory contains the enterprise-grade Terraform configuration for the Ghost Blog Platform production environment.

## 🏗️ Architecture Overview

This configuration deploys a highly available, multi-AZ production infrastructure with:

- **Network**: VPC with public/private subnets across 3 availability zones
- **Compute**: EKS cluster with auto-scaling node groups
- **Database**: RDS PostgreSQL Multi-AZ with read replica
- **Cache**: ElastiCache Redis cluster mode
- **Messaging**: Amazon MQ RabbitMQ cluster
- **Load Balancing**: Application Load Balancer with WAF
- **Security**: KMS encryption, CloudTrail, VPC Flow Logs
- **Monitoring**: CloudWatch, Performance Insights

## 📋 Prerequisites

### Required Tools

```bash
# Terraform
terraform --version  # >= 1.6.0

# AWS CLI
aws --version  # >= 2.0

# kubectl
kubectl version --client  # >= 1.28
```

### Required AWS Resources

1. **ACM Certificate**: Create SSL/TLS certificate in AWS Certificate Manager
2. **Route53 Hosted Zone** (optional): For DNS management
3. **AWS Account**: With appropriate permissions

### IAM Permissions

Your AWS user/role needs permissions for:
- VPC, EC2, EKS, RDS, ElastiCache, Amazon MQ
- IAM role creation
- KMS key management
- CloudWatch, CloudTrail
- S3 bucket creation
- WAF configuration

## 🚀 Quick Start

### 1. Clone and Navigate

```bash
cd /home/rrj/src/github/rudironsoni/Ghost/infrastructure/environments/production
```

### 2. Configure Variables

Create `terraform.tfvars.local` (gitignored) with sensitive values:

```hcl
# Copy from terraform.tfvars and update
acm_certificate_arn = "arn:aws:acm:us-east-1:ACCOUNT_ID:certificate/CERT_ID"
alert_email         = "ops-team@yourdomain.com"

# Credentials (use AWS Secrets Manager in production)
redis_auth_token    = "your-strong-redis-password-min-16-chars"
mq_admin_password   = "your-strong-mq-admin-password"
mq_app_password     = "your-strong-mq-app-password"
```

### 3. Configure Backend

Edit `backend.tf` and choose your backend:

**Option A: Terraform Cloud (Recommended)**
```bash
# 1. Create account at https://app.terraform.io
# 2. Create organization
# 3. Update backend.tf with your organization name
# 4. Login
terraform login
```

**Option B: S3 Backend**
```bash
# Create S3 bucket and DynamoDB table (see backend.tf comments)
# Then uncomment S3 backend configuration
```

### 4. Initialize Terraform

```bash
terraform init
```

### 5. Review Plan

```bash
terraform plan -out=production.tfplan
```

### 6. Deploy Infrastructure

```bash
terraform apply production.tfplan
```

**Estimated deployment time**: 25-35 minutes

## 📊 Cost Estimation

### Monthly Costs (US East 1)

| Service | Specification | On-Demand | Reserved (1yr) |
|---------|--------------|-----------|----------------|
| EKS Nodes | 5× r6i.xlarge | $1,500 | $900 |
| RDS Primary | db.r6i.large Multi-AZ | $800 | $480 |
| RDS Replica | db.r6i.large | $400 | $240 |
| ElastiCache | 9× cache.r7g.large | $1,200 | $720 |
| Amazon MQ | mq.m5.large Multi-AZ | $600 | $360 |
| ALB + Data | Data transfer | $300 | $300 |
| **Total** | | **$4,800** | **$3,000** |

**Savings with Reserved Instances**: ~38% ($1,800/month)

### Additional Costs
- VPC (NAT Gateways): ~$100/month
- CloudWatch Logs: ~$50/month
- KMS keys: ~$5/month
- S3 storage: ~$20/month
- Data transfer out: Variable

**Total estimated**: $4,975/month (on-demand) or $3,175/month (reserved)

## 🔐 Security Features

### Encryption

- ✅ **At Rest**: KMS encryption for EBS, RDS, ElastiCache, Amazon MQ
- ✅ **In Transit**: TLS/SSL for all connections
- ✅ **Secrets**: AWS Secrets Manager integration ready

### Network Security

- ✅ Private subnets for all compute resources
- ✅ Security groups with least privilege
- ✅ VPC Flow Logs enabled
- ✅ WAF with AWS managed rule sets
- ✅ Rate limiting and geo-blocking

### Compliance

- ✅ CloudTrail for audit logging
- ✅ Resource tagging for compliance (SOC2, GDPR, HIPAA)
- ✅ Encryption key rotation
- ✅ Enhanced monitoring for RDS
- ✅ Performance Insights enabled

### Access Control

- ✅ IAM Roles for Service Accounts (IRSA)
- ✅ EKS RBAC configuration
- ✅ Deletion protection on critical resources
- ✅ Multi-AZ for high availability

## 📈 High Availability

### Multi-AZ Deployment

- **EKS**: Nodes distributed across 3 AZs
- **RDS**: Multi-AZ automatic failover
- **ElastiCache**: Cluster mode with replicas in multiple AZs
- **Amazon MQ**: Active/standby brokers across AZs
- **ALB**: Cross-zone load balancing

### Auto-Scaling

- **EKS Nodes**: Cluster Autoscaler (min: 3, max: 10)
- **RDS Storage**: Auto-scaling up to 2 TB
- **Application**: Horizontal Pod Autoscaler (configure in K8s)

### Backup Strategy

- **RDS**: Automated daily backups, 30-day retention
- **ElastiCache**: Daily snapshots, 7-day retention
- **Amazon MQ**: Automated backups
- **Cross-region replication**: Optional (configure in variables)

## 🔧 Post-Deployment

### 1. Configure kubectl

```bash
aws eks update-kubeconfig --region us-east-1 --name ghost-blog-production
kubectl get nodes
```

### 2. Install Cluster Autoscaler

```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/autoscaler/master/cluster-autoscaler/cloudprovider/aws/examples/cluster-autoscaler-autodiscover.yaml
```

### 3. Install AWS Load Balancer Controller

```bash
helm repo add eks https://aws.github.io/eks-charts
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=ghost-blog-production
```

### 4. Deploy Ghost Application

```bash
kubectl apply -f ../../../kubernetes/production/
```

### 5. Configure DNS

```bash
# Get ALB DNS name
terraform output alb_dns_name

# Create Route53 record pointing to ALB
aws route53 change-resource-record-sets --hosted-zone-id ZONE_ID --change-batch file://dns-change.json
```

### 6. Retrieve Secrets

```bash
# RDS password (stored in Secrets Manager)
aws secretsmanager get-secret-value --secret-id ghost-blog-production-rds-password

# Redis auth token
aws secretsmanager get-secret-value --secret-id ghost-blog-production-redis-auth

# MQ passwords
aws secretsmanager get-secret-value --secret-id ghost-blog-production-mq-admin
aws secretsmanager get-secret-value --secret-id ghost-blog-production-mq-app
```

## 🎯 Monitoring & Alerts

### CloudWatch Dashboards

```bash
# Create custom dashboard
aws cloudwatch put-dashboard --dashboard-name ghost-blog-production \
  --dashboard-body file://cloudwatch-dashboard.json
```

### Key Metrics to Monitor

- **EKS**: CPU/Memory utilization, pod count
- **RDS**: Database connections, CPU, storage
- **ElastiCache**: Cache hit rate, evictions
- **ALB**: Request count, latency, 5xx errors
- **WAF**: Blocked requests

### Alerts Configuration

Alert email is sent to: `var.alert_email`

Set up additional SNS topics for:
- Critical: Database failover, node failures
- Warning: High CPU, low disk space
- Info: Scaling events, maintenance windows

## 🔄 Maintenance

### Regular Tasks

1. **Weekly**:
   - Review CloudWatch metrics
   - Check security group rules
   - Verify backup completion

2. **Monthly**:
   - Update EKS add-ons
   - Review and rotate credentials
   - Check for AWS service updates
   - Review cost optimization opportunities

3. **Quarterly**:
   - Kubernetes version upgrades
   - RDS minor version upgrades
   - Disaster recovery testing
   - Security audit

### Upgrade Procedures

#### EKS Cluster Upgrade

```bash
# 1. Check current version
kubectl version --short

# 2. Update control plane
terraform apply -var="eks_cluster_version=1.29"

# 3. Update node groups (rolling update)
# 4. Update add-ons
# 5. Test application
```

#### RDS Upgrade

```bash
# Minor version: Automatic during maintenance window
# Major version: Manual upgrade with testing
terraform apply -var="rds_engine_version=16.0"
```

## 🆘 Disaster Recovery

### Backup Verification

```bash
# List RDS snapshots
aws rds describe-db-snapshots --db-instance-identifier ghost-blog-production

# List ElastiCache snapshots
aws elasticache describe-snapshots --cache-cluster-id ghost-blog-production
```

### Recovery Procedures

1. **Database Recovery**: Restore from RDS snapshot
2. **Configuration Recovery**: Restore from Terraform state
3. **Application Recovery**: Redeploy from Git
4. **Cross-Region Failover**: Switch to DR region

**RTO**: 60 minutes (Recovery Time Objective)  
**RPO**: 15 minutes (Recovery Point Objective)

## 🧹 Cleanup

### Destroy Infrastructure

```bash
# Review what will be destroyed
terraform plan -destroy

# Destroy (requires confirmation)
terraform destroy

# Or auto-approve (use with caution)
terraform destroy -auto-approve
```

**⚠️ WARNING**: This will delete all resources including databases!

### Before Destroying

1. Take final database snapshot
2. Export important data
3. Backup Terraform state
4. Notify team members

## 📚 Additional Resources

- [AWS EKS Best Practices](https://aws.github.io/aws-eks-best-practices/)
- [Terraform AWS Modules](https://registry.terraform.io/namespaces/terraform-aws-modules)
- [Ghost Documentation](https://ghost.org/docs/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)

## 🐛 Troubleshooting

### Common Issues

**Issue**: EKS nodes not joining cluster
```bash
# Check node IAM role permissions
aws eks describe-cluster --name ghost-blog-production --query cluster.resourcesVpcConfig.clusterSecurityGroupId
```

**Issue**: RDS connection timeout
```bash
# Verify security group rules
aws ec2 describe-security-groups --group-ids sg-xxxxx
```

**Issue**: ElastiCache connection refused
```bash
# Check if nodes are in same VPC and security groups allow traffic
kubectl exec -it POD_NAME -- redis-cli -h REDIS_ENDPOINT -a AUTH_TOKEN ping
```

## 📞 Support

For issues or questions:
1. Check Terraform output messages
2. Review CloudWatch logs
3. Check AWS service health dashboard
4. Contact platform team

## 🔖 Version History

- **v1.0.0** (2026-02-03): Initial enterprise-grade production configuration
  - Multi-AZ HA setup
  - Comprehensive security configuration
  - Cost optimization features
  - Compliance-ready setup

## 📄 License

This infrastructure configuration is part of the Ghost Blog Platform project.
