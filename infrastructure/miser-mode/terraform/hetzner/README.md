# Hetzner Cloud Deployment

Deploy Ghost Platform on Hetzner Cloud with CPX21 instance (4 vCPU, 8GB RAM, 160GB NVMe).

## Prerequisites

1. Hetzner Cloud account - [Sign up here](https://www.hetzner.com/cloud)
2. API token - Generate at https://console.hetzner.cloud/
3. SSH key uploaded to Hetzner Cloud

## Configuration

1. Copy the example variables file:
```bash
cp terraform.tfvars.example terraform.tfvars
```

2. Edit `terraform.tfvars` with your settings:
```hcl
hcloud_token = "your-hetzner-api-token-here"
server_name  = "ghost-platform-miser"
server_type  = "cpx21"  # 4 vCPU, 8GB RAM, 160GB NVMe
location     = "nbg1"   # Nuremberg, Germany
```

3. (Optional) Add SSH key IDs:
```hcl
ssh_keys = ["12345678"]
```

## Getting Your SSH Key ID

```bash
# Using Hetzner CLI
hcloud ssh-key list

# Or check in Hetzner Cloud Console under Security > SSH Keys
```

## Deployment

```bash
# Initialize Terraform
terraform init

# Preview changes
terraform plan

# Deploy
terraform apply
```

## Outputs

After deployment, get your server details:

```bash
# Get SSH connection string
terraform output ssh_connection_string

# Get all outputs
terraform output
```

Example output:
```
server_id              = "12345678"
server_ip              = "95.217.123.456"
ssh_connection_string  = "ssh root@95.217.123.456"
created_at             = "2026-02-03T16:00:00Z"
server_name            = "ghost-platform-miser"
server_type            = "cpx21"
location               = "nbg1"
```

## Available Locations

- `nbg1` - Nuremberg, Germany (Recommended)
- `fsn1` - Falkenstein, Germany
- `hel1` - Helsinki, Finland
- `ash` - Ashburn, USA
- `hil` - Hillsboro, USA

## Server Types

| Type | vCPU | RAM | Storage | Price/month |
|------|------|-----|---------|-------------|
| cpx21 | 4 | 8GB | 160GB NVMe | €8.40 |
| cpx31 | 8 | 16GB | 320GB NVMe | €16.80 |
| cpx41 | 16 | 32GB | 640GB NVMe | €33.60 |

## Firewall Rules

Configured ports:
- 22 - SSH
- 80 - HTTP
- 443 - HTTPS
- 8080 - Application
- 9090 - Monitoring
- 3000 - Ghost

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

## Cost Estimate

**CPX21**: ~€8.40/month (~$9.20/month)

Includes:
- 4 vCPU
- 8GB RAM
- 160GB NVMe SSD
- 20TB traffic
- Unlimited incoming traffic

## Support

- [Hetzner Cloud Documentation](https://docs.hetzner.com/cloud/)
- [Terraform Hetzner Provider](https://registry.terraform.io/providers/hetznercloud/hcloud/latest/docs)
