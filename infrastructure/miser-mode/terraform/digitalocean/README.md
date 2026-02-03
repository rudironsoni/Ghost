# DigitalOcean Deployment

Deploy Ghost Platform on DigitalOcean with s-2vcpu-4gb droplet (2 vCPU, 4GB RAM, 80GB SSD).

## Prerequisites

1. DigitalOcean account - [Sign up here](https://www.digitalocean.com/)
2. API token - Generate at https://cloud.digitalocean.com/account/api/tokens
3. SSH key uploaded to DigitalOcean

## Configuration

1. Copy the example variables file:
```bash
cp terraform.tfvars.example terraform.tfvars
```

2. Edit `terraform.tfvars` with your settings:
```hcl
do_token     = "your-digitalocean-api-token-here"
droplet_name = "ghost-platform-miser"
droplet_size = "s-2vcpu-4gb"  # 2 vCPU, 4GB RAM, 80GB SSD
region       = "nyc3"          # New York 3
```

3. (Optional) Add SSH key fingerprints:
```hcl
ssh_keys = ["aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99"]
```

## Getting Your SSH Key Fingerprint

```bash
# Using DigitalOcean CLI (doctl)
doctl compute ssh-key list

# Or check in DigitalOcean Console under Settings > Security > SSH Keys
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

After deployment, get your droplet details:

```bash
# Get SSH connection string
terraform output ssh_connection_string

# Get all outputs
terraform output
```

Example output:
```
server_id              = "123456789"
server_ip              = "142.93.123.456"
ssh_connection_string  = "ssh root@142.93.123.456"
created_at             = "2026-02-03T16:00:00Z"
server_name            = "ghost-platform-miser"
droplet_size           = "s-2vcpu-4gb"
region                 = "nyc3"
```

## Available Regions

- `nyc1`, `nyc3` - New York (Recommended)
- `sfo3` - San Francisco
- `sgp1` - Singapore
- `lon1` - London
- `fra1` - Frankfurt
- `tor1` - Toronto
- `blr1` - Bangalore

## Droplet Sizes

| Size | vCPU | RAM | Storage | Transfer | Price/month |
|------|------|-----|---------|----------|-------------|
| s-1vcpu-2gb | 1 | 2GB | 50GB SSD | 2TB | $12 |
| s-2vcpu-4gb | 2 | 4GB | 80GB SSD | 4TB | $24 |
| s-4vcpu-8gb | 4 | 8GB | 160GB SSD | 5TB | $48 |

## Firewall Rules

Configured ports:
- 22 - SSH
- 80 - HTTP
- 443 - HTTPS
- 8080 - Application
- 9090 - Monitoring
- 3000 - Ghost

## Monitoring

DigitalOcean monitoring is enabled by default. View metrics at:
https://cloud.digitalocean.com/monitoring

## Backups

Backups are disabled by default to minimize costs. To enable:

```hcl
backups = true  # Adds 20% to droplet cost
```

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

## Cost Estimate

**s-2vcpu-4gb**: $24/month

Includes:
- 2 vCPU
- 4GB RAM
- 80GB SSD
- 4TB transfer
- Monitoring included
- Backups optional (+20%)

## Support

- [DigitalOcean Documentation](https://docs.digitalocean.com/)
- [Terraform DigitalOcean Provider](https://registry.terraform.io/providers/digitalocean/digitalocean/latest/docs)
