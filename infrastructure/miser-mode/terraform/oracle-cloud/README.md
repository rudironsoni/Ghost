# Oracle Cloud Infrastructure (OCI) Deployment

Deploy Ghost Platform on Oracle Cloud Free Tier with VM.Standard.A1.Flex (4 OCPU, 24GB RAM, 200GB storage) - **100% FREE forever!**

## Prerequisites

1. Oracle Cloud account - [Sign up here](https://www.oracle.com/cloud/free/)
2. API key configured - [Setup guide](https://docs.oracle.com/en-us/iaas/Content/API/Concepts/apisigningkey.htm)
3. SSH key pair generated

## Free Tier Benefits

Oracle Cloud Always Free Tier includes:
- **2 AMD-based Compute VMs** (1/8 OCPU, 1 GB RAM each)
- **4 ARM-based Ampere A1 cores** and **24 GB RAM** (can be split across instances)
- **200 GB Block Volume storage** (2 volumes)
- **10 GB Object Storage**
- **10 TB/month outbound data transfer**

This configuration uses the maximum ARM resources in a single instance.

## Setup API Access

### 1. Generate API Key Pair

```bash
mkdir -p ~/.oci
openssl genrsa -out ~/.oci/oci_api_key.pem 2048
chmod 600 ~/.oci/oci_api_key.pem
openssl rsa -pubout -in ~/.oci/oci_api_key.pem -out ~/.oci/oci_api_key_public.pem
```

### 2. Add Public Key to Oracle Cloud

1. Login to Oracle Cloud Console
2. Navigate to: Profile → User Settings → API Keys
3. Click "Add API Key"
4. Upload `~/.oci/oci_api_key_public.pem`
5. Copy the configuration preview (you'll need these values)

### 3. Get Required OCIDs

- **Tenancy OCID**: Profile → Tenancy → Copy OCID
- **User OCID**: Profile → User Settings → Copy OCID
- **Fingerprint**: Shown when you added the API key

## Configuration

1. Copy the example variables file:
```bash
cp terraform.tfvars.example terraform.tfvars
```

2. Edit `terraform.tfvars` with your settings:
```hcl
tenancy_ocid     = "ocid1.tenancy.oc1..aaaaaa..."
user_ocid        = "ocid1.user.oc1..aaaaaa..."
fingerprint      = "aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99"
private_key_path = "~/.oci/oci_api_key.pem"
region           = "us-ashburn-1"

instance_name            = "ghost-platform-miser"
instance_ocpus           = 4   # Max for free tier
instance_memory_in_gbs   = 24  # Max for free tier
boot_volume_size_in_gbs  = 200 # Max for free tier

ssh_public_key_path = "~/.ssh/id_rsa.pub"
```

## Deployment

```bash
# Initialize Terraform
terraform init

# Preview changes
terraform plan

# Deploy (may take 5-10 minutes)
terraform apply
```

## Outputs

After deployment:

```bash
# Get SSH connection string
terraform output ssh_connection_string

# Get all outputs
terraform output
```

Example output:
```
server_id              = "ocid1.instance.oc1.iad.anuw..."
server_ip              = "129.213.123.456"
ssh_connection_string  = "ssh ubuntu@129.213.123.456"
created_at             = "2026-02-03T16:00:00.000Z"
instance_shape         = "VM.Standard.A1.Flex"
region                 = "us-ashburn-1"
```

## Available Regions

Recommended for Free Tier:
- `us-ashburn-1` - US East (Virginia)
- `us-phoenix-1` - US West (Arizona)
- `eu-frankfurt-1` - EU (Germany)
- `uk-london-1` - UK (London)
- `ap-tokyo-1` - Asia Pacific (Japan)

## Instance Shapes (Free Tier)

| Shape | Architecture | OCPU | RAM | Storage | Cost |
|-------|-------------|------|-----|---------|------|
| VM.Standard.E2.1.Micro | AMD | 1/8 | 1GB | 50GB | **FREE** |
| VM.Standard.A1.Flex | ARM (Ampere) | Up to 4 | Up to 24GB | 200GB | **FREE** |

This configuration uses **VM.Standard.A1.Flex** for maximum performance.

## Firewall Rules

Configured ports:
- 22 - SSH
- 80 - HTTP
- 443 - HTTPS
- 8080 - Application
- 9090 - Monitoring
- 3000 - Ghost

## Important Notes

### Default User
- Oracle Cloud uses **ubuntu** user (not root)
- SSH: `ssh ubuntu@<ip-address>`

### ARM Architecture
- VM.Standard.A1.Flex uses ARM64 (aarch64) architecture
- Most Docker images support ARM64, including Ghost
- Use multi-arch images when available

### Free Tier Limits
- Maximum 2 compute instances across all shapes
- Maximum 4 OCPU and 24GB RAM total (for ARM)
- Can split resources: e.g., 2 instances with 2 OCPU/12GB each

### Resource Availability
- ARM instances can have limited availability in some regions
- If deployment fails due to capacity, try:
  - Different availability domain
  - Different region
  - Retry after a few hours

## Troubleshooting

### "Out of capacity" Error

Try these solutions:

1. **Wait and retry**: ARM capacity fluctuates
```bash
terraform apply -auto-approve
```

2. **Try different region**:
```hcl
region = "us-phoenix-1"  # Instead of us-ashburn-1
```

3. **Use smaller instance**:
```hcl
instance_ocpus         = 2
instance_memory_in_gbs = 12
```

### Authentication Issues

Verify your configuration:
```bash
# Test OCI CLI (if installed)
oci iam user get --user-id <your-user-ocid>
```

### Network Connectivity

Oracle Cloud has additional OS-level firewall (iptables). Cloud-init configures UFW, but if issues persist:

```bash
# SSH to instance
ssh ubuntu@<ip-address>

# Check UFW status
sudo ufw status

# Verify ports are open
sudo ss -tlnp | grep -E '(80|443|8080|9090|3000)'
```

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

**Note**: This removes the instance but doesn't count against free tier limits.

## Cost Estimate

**VM.Standard.A1.Flex (4 OCPU, 24GB RAM, 200GB storage)**: **$0/month (FREE)**

This is part of Oracle Cloud Always Free Tier and remains free indefinitely.

## Additional Resources

- [Oracle Cloud Free Tier](https://www.oracle.com/cloud/free/)
- [OCI Documentation](https://docs.oracle.com/en-us/iaas/Content/home.htm)
- [Terraform OCI Provider](https://registry.terraform.io/providers/oracle/oci/latest/docs)
- [ARM Architecture Guide](https://docs.oracle.com/en-us/iaas/Content/Compute/References/arm.htm)

## Support

For Oracle Cloud issues:
- [Community Forum](https://community.oracle.com/customerconnect/categories/oci)
- [Support Portal](https://support.oracle.com/)
