# Terraform Infrastructure Files Index

## Directory Structure

```
terraform/
├── .gitignore                           # Git ignore for sensitive files
├── README.md                            # Main documentation
├── QUICKSTART.md                        # Quick start guide
│
├── modules/
│   └── cloud-init/
│       ├── cloud-init.yaml             # Cloud-init configuration for Docker setup
│       └── outputs.tf                   # Module outputs
│
├── hetzner/                            # Hetzner Cloud configuration
│   ├── versions.tf                     # Terraform & provider versions
│   ├── providers.tf                    # Provider configuration
│   ├── variables.tf                    # Input variables
│   ├── main.tf                         # Main infrastructure config
│   ├── outputs.tf                      # Output values
│   ├── terraform.tfvars.example        # Example configuration
│   └── README.md                       # Hetzner-specific docs
│
├── digitalocean/                       # DigitalOcean configuration
│   ├── versions.tf                     # Terraform & provider versions
│   ├── providers.tf                    # Provider configuration
│   ├── variables.tf                    # Input variables
│   ├── main.tf                         # Main infrastructure config
│   ├── outputs.tf                      # Output values
│   ├── terraform.tfvars.example        # Example configuration
│   └── README.md                       # DigitalOcean-specific docs
│
└── oracle-cloud/                       # Oracle Cloud configuration
    ├── versions.tf                     # Terraform & provider versions
    ├── providers.tf                    # Provider configuration
    ├── variables.tf                    # Input variables
    ├── main.tf                         # Main infrastructure config
    ├── outputs.tf                      # Output values
    ├── terraform.tfvars.example        # Example configuration
    └── README.md                       # Oracle Cloud-specific docs
```

## File Counts

- **Total Files**: 27
- **Terraform Files (.tf)**: 18
- **Documentation (.md)**: 5
- **Configuration Files**: 4 (cloud-init.yaml + 3 x .tfvars.example)

## Provider Resources

### Hetzner Cloud
- **Main Resources**: hcloud_server, hcloud_firewall
- **Server Spec**: CPX21 (4 vCPU, 8GB RAM, 160GB NVMe)
- **Location**: nbg1 (Nuremberg, Germany)
- **Files**: 7

### DigitalOcean
- **Main Resources**: digitalocean_droplet, digitalocean_firewall
- **Droplet Spec**: s-2vcpu-4gb (2 vCPU, 4GB RAM, 80GB SSD)
- **Region**: nyc3 (New York)
- **Files**: 7

### Oracle Cloud Infrastructure
- **Main Resources**: oci_core_instance, oci_core_vcn, oci_core_subnet, oci_core_security_list
- **Instance Spec**: VM.Standard.A1.Flex (4 OCPU, 24GB RAM, 200GB storage)
- **Region**: us-ashburn-1
- **Files**: 7

## Shared Module

### cloud-init
- **Purpose**: Automated Docker installation and system setup
- **Features**: Docker Engine, Docker Compose, UFW firewall, port configuration
- **OS**: Ubuntu 22.04 LTS
- **Files**: 2

## Configuration Files

Each provider includes:
- ✅ versions.tf - Terraform and provider version constraints
- ✅ providers.tf - Provider authentication configuration
- ✅ variables.tf - Configurable input variables with defaults
- ✅ main.tf - Resource definitions (compute, network, firewall)
- ✅ outputs.tf - Exported values (IPs, connection strings, metadata)
- ✅ terraform.tfvars.example - Example configuration template
- ✅ README.md - Provider-specific documentation

## Port Configuration

All providers configure firewall rules for:
- **22**: SSH access
- **80**: HTTP
- **443**: HTTPS
- **8080**: Application port
- **9090**: Monitoring port
- **3000**: Ghost platform port

## Output Values

Each provider outputs:
- `server_id` - Cloud provider instance ID
- `server_ip` - Public IPv4 address
- `ssh_connection_string` - Ready-to-use SSH command
- `created_at` - Instance creation timestamp
- Additional provider-specific metadata

## Usage

See [QUICKSTART.md](./QUICKSTART.md) for quick start instructions or [README.md](./README.md) for detailed documentation.

## Cost Comparison

| Provider | Instance | Monthly Cost |
|----------|----------|--------------|
| Oracle Cloud | VM.Standard.A1.Flex (4 OCPU, 24GB RAM) | **FREE** |
| Hetzner | CPX21 (4 vCPU, 8GB RAM) | ~$9 |
| DigitalOcean | s-2vcpu-4gb (2 vCPU, 4GB RAM) | $24 |
