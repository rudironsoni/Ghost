# Quick Start Guide - Ghost Platform Ultra Miser Mode Terraform

Get your Ghost Platform running in under 10 minutes!

## Choose Your Cloud Provider

### 🟢 Oracle Cloud (Recommended - FREE Forever)
**Cost: $0/month** | 4 OCPU, 24GB RAM, 200GB storage

```bash
cd oracle-cloud
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your OCI credentials
terraform init
terraform apply
```

[Detailed Oracle Cloud Setup →](./oracle-cloud/README.md)

---

### 🔵 Hetzner Cloud (Best Price/Performance)
**Cost: ~$9/month** | 4 vCPU, 8GB RAM, 160GB NVMe

```bash
cd hetzner
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your Hetzner API token
terraform init
terraform apply
```

[Detailed Hetzner Setup →](./hetzner/README.md)

---

### 🔷 DigitalOcean (Easiest Setup)
**Cost: $24/month** | 2 vCPU, 4GB RAM, 80GB SSD

```bash
cd digitalocean
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your DO API token
terraform init
terraform apply
```

[Detailed DigitalOcean Setup →](./digitalocean/README.md)

---

## After Deployment

### 1. Get Your Server IP

```bash
terraform output server_ip
```

### 2. Connect via SSH

```bash
# Copy the connection string
terraform output ssh_connection_string

# Connect (wait 2-3 minutes for cloud-init to complete)
ssh ubuntu@<your-ip>  # Oracle Cloud
ssh root@<your-ip>    # Hetzner/DigitalOcean
```

### 3. Verify Docker Installation

```bash
docker --version
docker compose version
```

### 4. Deploy Ghost Platform

```bash
cd /opt/ghost-platform
# Clone your Ghost Platform repository
git clone <your-ghost-repo> .
# Run your docker-compose setup
docker compose up -d
```

### 5. Configure DNS

Point your domain to the server IP:
```
A     @     <your-server-ip>
A     www   <your-server-ip>
```

## Comparison Table

| Provider | vCPU/OCPU | RAM | Storage | Monthly Cost | Best For |
|----------|-----------|-----|---------|--------------|----------|
| **Oracle Cloud** | 4 | 24GB | 200GB | **FREE** | Maximum resources, zero cost |
| **Hetzner** | 4 | 8GB | 160GB NVMe | ~$9 | Best price/performance ratio |
| **DigitalOcean** | 2 | 4GB | 80GB SSD | $24 | Simplicity and reliability |

## Need Help?

- [Full README](./README.md)
- [Oracle Cloud Guide](./oracle-cloud/README.md)
- [Hetzner Guide](./hetzner/README.md)
- [DigitalOcean Guide](./digitalocean/README.md)

## Common Issues

### Can't connect via SSH?
- Wait 3-5 minutes for cloud-init to complete
- Check security group/firewall rules allow port 22
- Verify SSH key is correctly configured

### Terraform authentication error?
- Verify API token/credentials in terraform.tfvars
- Check token has correct permissions
- For Oracle Cloud, verify OCID values are correct

### Oracle Cloud "Out of capacity"?
- ARM instances have limited availability
- Try different region or availability domain
- Reduce resources (2 OCPU, 12GB RAM still free)
- Wait and retry after a few hours

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

## Next Steps

1. ✅ Deploy infrastructure with Terraform
2. 🔧 Install Ghost Platform application
3. 🌐 Configure domain and SSL
4. 📊 Set up monitoring (optional)
5. 🔒 Harden security (optional)

Happy deploying! 🚀
