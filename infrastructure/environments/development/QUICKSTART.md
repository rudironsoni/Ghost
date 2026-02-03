# Ghost Platform Development - Quick Start

Get your development environment running in 5 minutes!

## Prerequisites

```bash
# Install required tools
# - Terraform: https://www.terraform.io/downloads
# - AWS CLI: https://aws.amazon.com/cli/
# - kubectl: https://kubernetes.io/docs/tasks/tools/
# - jq: https://stedolan.github.io/jq/

# Configure AWS credentials
aws configure
```

## Option 1: Automated Deployment (Recommended)

```bash
# Run the deployment script
./deploy.sh

# Or with auto-approve (no prompts)
./deploy.sh --auto
```

The script will:
1. ✓ Check dependencies
2. ✓ Initialize Terraform
3. ✓ Plan infrastructure
4. ✓ Deploy everything
5. ✓ Wait for services
6. ✓ Get kubeconfig
7. ✓ Show connection info

## Option 2: Manual Deployment

```bash
# 1. Initialize
terraform init

# 2. Plan
terraform plan

# 3. Deploy
terraform apply

# 4. Get outputs
terraform output
```

## Option 3: Using Makefile

```bash
# See all available commands
make help

# Quick deployment
make init
make plan
make apply

# Get kubeconfig
make get-config

# Connect to k3s
make ssh
```

## Post-Deployment

### 1. Set up kubectl

```bash
# Export kubeconfig
export KUBECONFIG=$(pwd)/kubeconfig.yaml

# Test connection
kubectl get nodes
```

### 2. Deploy Ghost Platform

```bash
# Deploy base platform
kubectl apply -k ../../platform/base

# Deploy Ghost services
kubectl apply -k ../../platform/services

# Check status
kubectl get pods -A
```

### 3. Access Your Environment

```bash
# Get connection info
terraform output connection_info

# SSH to k3s node
terraform output ssh_command | bash

# View all outputs
terraform output
```

## Common Tasks

### Start/Stop Instance (Cost Saving)

```bash
# Stop instance when not in use
make stop
# or
aws ec2 stop-instances --instance-ids $(terraform output -raw k3s_instance_id)

# Start instance
make start
# or
aws ec2 start-instances --instance-ids $(terraform output -raw k3s_instance_id)
```

### Check Status

```bash
# Quick status check
make status

# Test all connections
make health-check
```

### View Logs

```bash
# CloudWatch logs
make logs

# Or via SSH
ssh -i .ssh/ghost-development-key.pem ec2-user@$(terraform output -raw k3s_public_ip)
sudo journalctl -u k3s -f
```

## Customization

### Change Instance Size

Edit `terraform.tfvars`:

```hcl
instance_type = "t3.medium"  # Upgrade from t3.small
```

Apply changes:

```bash
terraform apply
```

### Disable Auto-Shutdown

Edit `terraform.tfvars`:

```hcl
enable_auto_shutdown = false
```

Apply changes:

```bash
terraform apply
```

### Use Amazon MQ Instead of Self-Hosted

Edit `terraform.tfvars`:

```hcl
use_self_hosted_rabbitmq = false
```

This adds ~$30/month but provides managed RabbitMQ.

## Costs

### Default Configuration
- **With auto-shutdown**: $25-40/month
- **Without auto-shutdown**: $40-60/month

### Cost Breakdown
```
EC2 (t3.small spot):      $5-8/month
RDS (db.t3.micro):        $12-15/month
Redis (cache.t3.micro):   $12-15/month
RabbitMQ (self-hosted):   $0/month
Storage & misc:           $8-15/month
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TOTAL:                    $40-60/month
```

## Troubleshooting

### Can't Connect to k3s

```bash
# Check instance status
aws ec2 describe-instances --instance-ids $(terraform output -raw k3s_instance_id)

# Check cloud-init logs
ssh ec2-user@$(terraform output -raw k3s_public_ip)
sudo tail -f /var/log/cloud-init-output.log
```

### Database Connection Failed

```bash
# Test from k3s node
ssh ec2-user@$(terraform output -raw k3s_public_ip)
pg_isready -h $(terraform output -raw db_address) -p 5432 -U ghostadmin
```

### Spot Instance Interrupted

AWS can interrupt spot instances. The instance will be replaced automatically:

```bash
# Check status
terraform refresh

# Force recreation if needed
terraform apply -replace=aws_instance.k3s
```

## Cleanup

### Temporary Cleanup

```bash
# Stop instances (keeps infrastructure, no charges for stopped instances)
make stop
```

### Permanent Cleanup

```bash
# Destroy everything (WARNING: Data will be lost!)
terraform destroy

# Or using make
make destroy

# Or using script
./deploy.sh --destroy
```

## Next Steps

1. **Deploy Ghost**: Follow the deployment guide in `../../platform/services/`
2. **Set up monitoring**: Configure Prometheus and Grafana
3. **Configure CI/CD**: Set up GitHub Actions or GitLab CI
4. **Add custom domain**: Configure Route53 and SSL certificates

## Support

- **Documentation**: See `README.md` for detailed documentation
- **Issues**: GitHub Issues
- **Infrastructure Docs**: `infrastructure/docs/`
- **Runbooks**: `infrastructure/docs/runbooks/`

## Estimated Setup Time

- **First time**: 15-20 minutes (includes infrastructure + k3s initialization)
- **Subsequent deploys**: 10-15 minutes
- **From scratch to Ghost running**: 20-30 minutes

---

**Happy Coding! 🚀**
