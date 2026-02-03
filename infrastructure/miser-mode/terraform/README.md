# Ghost Platform Ultra Miser Mode - Terraform Infrastructure

This directory contains Terraform configurations for deploying Ghost Platform in Ultra Miser Mode across three cloud providers:

- **Hetzner Cloud** - CPX21 (4 vCPU, 8GB RAM, 160GB NVMe) in nbg1
- **DigitalOcean** - s-2vcpu-4gb (2 vCPU, 4GB RAM, 80GB SSD) in nyc3
- **Oracle Cloud** - VM.Standard.A1.Flex (4 OCPU, 24GB RAM, 200GB) in us-ashburn-1 (Free Tier)

## Directory Structure

```
terraform/
├── hetzner/           # Hetzner Cloud configuration
├── digitalocean/      # DigitalOcean configuration
├── oracle-cloud/      # Oracle Cloud Infrastructure configuration
└── modules/
    └── cloud-init/    # Shared cloud-init configuration for Docker setup
```

## Features

Each provider configuration includes:

- Single server deployment with appropriate specs
- Automatic Docker and Docker Compose installation via cloud-init
- Firewall rules for ports: 22 (SSH), 80 (HTTP), 443 (HTTPS), 8080, 9090, 3000
- Output values: server_id, server_ip, ssh_connection_string, created_at
- Example terraform.tfvars files for easy configuration

## Prerequisites

1. **Terraform** >= 1.5.0 installed
2. **Cloud provider account** with API credentials
3. **SSH key** configured in your cloud provider

## Quick Start

### 1. Choose Your Provider

Navigate to your preferred provider directory:

```bash
cd hetzner/          # For Hetzner Cloud
cd digitalocean/     # For DigitalOcean
cd oracle-cloud/     # For Oracle Cloud
```

### 2. Configure Variables

Copy the example tfvars file and edit with your credentials:

```bash
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your API credentials and preferences
```

### 3. Initialize Terraform

```bash
terraform init
```

### 4. Plan and Apply

```bash
# Review the plan
terraform plan

# Apply the configuration
terraform apply
```

### 5. Get Connection Details

After successful deployment:

```bash
terraform output ssh_connection_string
```

## Provider-Specific Documentation

See individual README files in each provider directory:

- [Hetzner Cloud Setup](./hetzner/README.md)
- [DigitalOcean Setup](./digitalocean/README.md)
- [Oracle Cloud Setup](./oracle-cloud/README.md)

## Cloud-Init Configuration

All servers are provisioned with:

- Ubuntu 22.04 LTS
- Docker Engine (latest)
- Docker Compose v2
- Basic security (UFW firewall)
- Required ports opened automatically
- `/opt/ghost-platform` directory created

## Cost Comparison

| Provider | Instance Type | vCPU | RAM | Storage | Est. Monthly Cost |
|----------|---------------|------|-----|---------|-------------------|
| Hetzner | CPX21 | 4 | 8GB | 160GB NVMe | ~€8.40 |
| DigitalOcean | s-2vcpu-4gb | 2 | 4GB | 80GB SSD | $24.00 |
| Oracle Cloud | VM.Standard.A1.Flex | 4 | 24GB | 200GB | **FREE** (Always Free Tier) |

## Security Considerations

1. **Change default SSH keys** - Add your SSH keys to the configuration
2. **Restrict firewall rules** - Update source IPs in firewall rules if needed
3. **Keep secrets safe** - Never commit `terraform.tfvars` with real credentials
4. **Use separate environments** - Consider using Terraform workspaces for dev/prod

## Common Commands

```bash
# Initialize Terraform
terraform init

# Validate configuration
terraform validate

# Format configuration files
terraform fmt

# Plan changes
terraform plan

# Apply changes
terraform apply

# Show outputs
terraform output

# Destroy infrastructure
terraform destroy
```

## Troubleshooting

### Provider Authentication Errors

- **Hetzner**: Verify API token at https://console.hetzner.cloud/
- **DigitalOcean**: Verify API token at https://cloud.digitalocean.com/account/api/tokens
- **Oracle**: Verify API key configuration at https://cloud.oracle.com/identity/domains/my-profile/api-keys

### SSH Connection Issues

1. Ensure your SSH key is added to the cloud provider
2. Check firewall rules allow port 22
3. Wait 2-3 minutes after deployment for cloud-init to complete

### Resource Quota Issues

- **Oracle Free Tier**: Limited to 2 instances with 4 OCPU total
- Check your cloud provider dashboard for quota limits

## Next Steps

After deployment:

1. SSH into your server using the connection string from outputs
2. Deploy Ghost Platform using Docker Compose
3. Configure DNS records to point to your server IP
4. Set up SSL certificates (Let's Encrypt recommended)

## Support

For issues specific to:
- Terraform configurations: Check this repository's issues
- Cloud provider: Consult respective provider documentation
- Ghost Platform: See main Ghost Platform documentation

## License

See main Ghost Platform repository for license information.
