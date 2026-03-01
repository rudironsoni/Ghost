# Ghost Platform - Development Environment

Enterprise-grade, cost-optimized Terraform configuration for the Ghost Platform development environment on AWS.

## Overview

This configuration deploys a complete development environment with:

- **Compute**: Single k3s node on spot instance (t3.small)
- **Database**: RDS PostgreSQL (db.t3.micro) 
- **Cache**: ElastiCache Redis (cache.t3.micro)
- **Messaging**: Self-hosted RabbitMQ on k3s (or optional Amazon MQ)
- **Networking**: VPC with public subnets
- **Monitoring**: CloudWatch Logs and Metrics
- **Cost Control**: Automated shutdown/startup schedule

**Estimated Monthly Cost**: $40-60 (with auto-shutdown enabled)

## Prerequisites

- [Terraform](https://www.terraform.io/downloads.html) >= 1.5.0
- [AWS CLI](https://aws.amazon.com/cli/) configured with credentials
- AWS account with appropriate permissions
- (Optional) [Terraform Cloud](https://app.terraform.io/) account

## Quick Start

### 1. Configure Backend

Edit `backend.tf` and choose your backend:

```bash
# Option A: Terraform Cloud (Recommended)
# Update organization name in backend.tf

# Option B: AWS S3
# Uncomment S3 backend section and create S3 bucket + DynamoDB table
aws s3 mb s3://ghost-terraform-state
aws dynamodb create-table \
  --table-name ghost-terraform-locks \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST
```

### 2. Customize Configuration

Edit `terraform.tfvars` to customize your deployment:

```hcl
# Example: Use larger instance
instance_type = "t3.medium"

# Example: Disable auto-shutdown
enable_auto_shutdown = false

# Example: Use Amazon MQ instead of self-hosted
use_self_hosted_rabbitmq = false
```

### 3. Deploy

```bash
# Initialize Terraform
terraform init

# Review the plan
terraform plan

# Apply the configuration
terraform apply

# Save sensitive outputs
terraform output -json > outputs.json
chmod 600 outputs.json
```

### 4. Connect to k3s Node

```bash
# Get SSH command
terraform output ssh_command

# Connect
ssh -i .ssh/ghost-development-key.pem ec2-user@<PUBLIC_IP>

# Get kubeconfig
sudo cat /etc/rancher/k3s/k3s.yaml > kubeconfig.yaml

# Update server address in kubeconfig.yaml (replace 127.0.0.1 with public IP)
sed -i "s/127.0.0.1/<PUBLIC_IP>/" kubeconfig.yaml

# Use kubeconfig locally
export KUBECONFIG=$(pwd)/kubeconfig.yaml
kubectl get nodes
```

### 5. Deploy Ghost Platform

```bash
# Deploy base platform components
kubectl apply -k ../../platform/base

# Deploy Ghost application
kubectl apply -k ../../platform/services

# Check deployment status
kubectl get pods -A
```

## Configuration Options

### Instance Types

| Type | vCPU | RAM | On-Demand | Spot | Use Case |
|------|------|-----|-----------|------|----------|
| t3.micro | 1 | 1GB | $7/mo | $3/mo | Minimal testing |
| **t3.small** | 2 | 2GB | $15/mo | $5/mo | **Light dev (default)** |
| t3.medium | 2 | 4GB | $30/mo | $10/mo | Active development |
| t3.large | 2 | 8GB | $60/mo | $20/mo | Performance testing |

### Database Options

| Class | vCPU | RAM | Cost | Use Case |
|-------|------|-----|------|----------|
| **db.t3.micro** | 2 | 1GB | $12/mo | **Dev (default)** |
| db.t4g.micro | 2 | 1GB | $10/mo | Dev (ARM) |
| db.t3.small | 2 | 2GB | $25/mo | Staging |

### Cache Options

| Type | vCPU | RAM | Cost | Use Case |
|------|------|-----|------|----------|
| **cache.t3.micro** | 2 | 0.5GB | $12/mo | **Dev (default)** |
| cache.t4g.micro | 2 | 0.5GB | $10/mo | Dev (ARM) |
| cache.t3.small | 2 | 1.37GB | $25/mo | Staging |

### Messaging Options

| Option | Cost | Pros | Cons |
|--------|------|------|------|
| **Self-hosted on k3s** | $0 | **Free, flexible** | **More management** |
| Amazon MQ (single) | $30/mo | Managed, reliable | Costs more |
| Amazon MQ (HA) | $60/mo | High availability | Expensive for dev |

## Cost Optimization

### Current Configuration

With default settings (`terraform.tfvars`):

```
EC2 (t3.small spot):        $5-8/month
RDS (db.t3.micro):          $12-15/month
ElastiCache (cache.t3.micro): $12-15/month
RabbitMQ (self-hosted):     $0/month
Storage & networking:       $5-10/month
CloudWatch:                 $2-5/month
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TOTAL:                      $40-60/month
```

### Auto-Shutdown Savings

With auto-shutdown enabled (12 hours/day, weekdays only):

```
Hours running: ~130/month (vs 730 full-time)
Compute savings: ~40%
New total: $25-40/month
```

### Ultra-Minimal Configuration

For minimal costs (~$25-30/month):

```hcl
instance_type = "t3.micro"
db_instance_class = "db.t4g.micro"
redis_node_type = "cache.t4g.micro"
use_self_hosted_rabbitmq = true
enable_auto_shutdown = true
enable_cloudwatch_logs = false
```

### Production-Like Development

For more realistic testing (~$150-200/month):

```hcl
instance_type = "t3.large"
db_instance_class = "db.t3.small"
redis_node_type = "cache.t3.small"
mq_deployment_mode = "ACTIVE_STANDBY_MULTI_AZ"
use_self_hosted_rabbitmq = false
enable_multi_az = true
enable_auto_shutdown = false
```

## Security

### Development Defaults (NOT for production!)

- SSH and HTTP access from anywhere (`0.0.0.0/0`)
- Public subnets only (no NAT gateway)
- Single availability zone
- Skip final RDS snapshot on destroy

### Recommended Production Changes

```hcl
# Restrict access
allowed_ssh_cidr_blocks = ["YOUR_IP/32"]
allowed_http_cidr_blocks = ["YOUR_IP/32"]

# Enable high availability
enable_multi_az = true
enable_nat_gateway = true

# Add private subnets
# Enable RDS multi-AZ
# Enable deletion protection
db_skip_final_snapshot = false
```

## Outputs

Get all outputs:

```bash
# View all outputs
terraform output

# View connection info (sensitive)
terraform output -json connection_info | jq

# View specific outputs
terraform output k3s_public_ip
terraform output db_endpoint
terraform output redis_endpoint
```

Important outputs:

- `k3s_public_ip`: Public IP of k3s node
- `ssh_command`: Command to SSH into k3s node
- `db_endpoint`: Database connection endpoint
- `db_password`: Database password (sensitive)
- `redis_endpoint`: Redis connection endpoint
- `connection_info`: All service connection details
- `getting_started`: Quick start instructions

## Auto-Shutdown Schedule

Default schedule (EST):

- **Shutdown**: 8 PM weekdays (Midnight UTC Mon-Fri)
- **Startup**: 8 AM weekdays (Noon UTC Mon-Fri)

Customize in `terraform.tfvars`:

```hcl
# Shutdown at 6 PM EST
auto_shutdown_schedule = "0 22 * * MON-FRI"

# Startup at 9 AM EST  
auto_startup_schedule = "0 13 * * MON-FRI"

# Disable auto-shutdown
enable_auto_shutdown = false
```

## Troubleshooting

### Spot Instance Interrupted

Spot instances can be interrupted by AWS. If this happens:

```bash
# Wait for new instance to launch
terraform refresh

# Or force recreation
terraform apply -replace=aws_instance.k3s
```

### Cannot Connect to k3s

```bash
# Check instance status
aws ec2 describe-instances --instance-ids $(terraform output -raw k3s_instance_id)

# Check security group rules
aws ec2 describe-security-groups --group-ids $(terraform output -raw k3s_security_group_id)

# View cloud-init logs
ssh ec2-user@<PUBLIC_IP>
sudo tail -f /var/log/cloud-init-output.log
```

### Database Connection Issues

```bash
# Test database connectivity from k3s node
ssh ec2-user@<PUBLIC_IP>
pg_isready -h $(terraform output -raw db_address) -p 5432 -U ghostadmin

# Check security group rules
# Ensure k3s security group can access RDS security group on port 5432
```

### High Costs

```bash
# Check what's running
terraform state list

# Enable auto-shutdown
terraform apply -var="enable_auto_shutdown=true"

# Manually stop instance
aws ec2 stop-instances --instance-ids $(terraform output -raw k3s_instance_id)
```

## Cleanup

To destroy all resources:

```bash
# Review what will be destroyed
terraform plan -destroy

# Destroy everything
terraform destroy

# Confirm destruction
# Type: yes
```

**Warning**: This will delete:
- All EC2 instances
- RDS database (no final snapshot in dev)
- ElastiCache cluster
- All networking components

## Multi-Cloud Alternatives

### GCP (Google Cloud Platform)

Estimated cost: ~$60/month

```hcl
# Use GKE Autopilot or single-node GKE
# Cloud SQL for PostgreSQL
# Memorystore for Redis
# CloudAMQP or self-hosted RabbitMQ
# Cloud Scheduler for auto-shutdown
```

### Azure

Estimated cost: ~$70/month

```hcl
# Use AKS with single-node pool
# Azure Database for PostgreSQL - Flexible Server
# Azure Cache for Redis (Basic tier)
# Azure Service Bus or self-hosted RabbitMQ
# Azure Automation for auto-shutdown
```

See comments in `main.tf` for alternative resource configurations.

## Support

- **Issues**: GitHub Issues
- **Documentation**: `infrastructure/docs/`
- **Runbooks**: `infrastructure/docs/runbooks/`

## License

See repository LICENSE file.
