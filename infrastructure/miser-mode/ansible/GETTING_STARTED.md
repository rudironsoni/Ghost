# Ghost Platform Ultra Miser Mode - Getting Started

This guide will walk you through deploying Ghost Platform on a fresh Ubuntu 22.04 server using Ansible.

## Prerequisites Checklist

### ✅ On Your Local Machine

- [ ] Ansible 2.10+ installed
- [ ] Python 3.8+ installed
- [ ] SSH client installed
- [ ] Git installed

### ✅ On Your Target Server

- [ ] Ubuntu 22.04 LTS (fresh install)
- [ ] 4GB RAM minimum (8GB recommended)
- [ ] 2 CPU cores minimum (4 cores recommended)
- [ ] 20GB disk space minimum (50GB recommended)
- [ ] Root or sudo access
- [ ] SSH access configured
- [ ] Public IP address or domain name

## Step-by-Step Deployment

### Step 1: Install Ansible

On your local machine:

```bash
# Using pip
pip install ansible

# Or using apt (Ubuntu/Debian)
sudo apt update
sudo apt install ansible

# Verify installation
ansible --version
```

### Step 2: Prepare SSH Access

```bash
# Generate SSH key if you don't have one
ssh-keygen -t ed25519 -C "your_email@example.com"

# Copy your public key to the server
ssh-copy-id ubuntu@YOUR_SERVER_IP

# Test connection
ssh ubuntu@YOUR_SERVER_IP
```

### Step 3: Clone the Repository

```bash
# Clone Ghost Platform repository
git clone https://github.com/rudironsoni/Ghost.git
cd Ghost/infrastructure/miser-mode/ansible
```

### Step 4: Install Ansible Dependencies

```bash
# Install required collections
make install

# Or manually
ansible-galaxy collection install -r requirements.yml
```

### Step 5: Configure Inventory

```bash
# Copy example inventory
cp inventory/hosts.yml.example inventory/hosts.yml

# Edit inventory file
vim inventory/hosts.yml
```

Edit `inventory/hosts.yml`:

```yaml
all:
  children:
    ghost_servers:
      hosts:
        ghost-prod:
          ansible_host: 192.168.1.100        # Your server IP
          ansible_user: ubuntu               # SSH user
          ansible_port: 22                   # SSH port
          
          # Server configuration
          domain_name: ghost.example.com     # Your domain (optional)
          enable_ssl: false                  # Set to true for SSL
          admin_email: admin@example.com     # Your email
```

### Step 6: Configure Variables

```bash
# Edit global variables
vim group_vars/all.yml
```

Key variables to configure:

```yaml
# Domain and SSL
domain_name: "ghost.example.com"
enable_ssl: false  # Set to true when you have SSL cert

# Admin email
admin_email: "admin@example.com"

# Backup to S3 (optional)
backup_s3_bucket: "my-ghost-backups"
backup_s3_region: "us-east-1"
# backup_s3_access_key: "YOUR_ACCESS_KEY"
# backup_s3_secret_key: "YOUR_SECRET_KEY"

# Platform features
linkedin_enabled: true
indeed_enabled: true
glassdoor_enabled: false
```

### Step 7: Verify Connectivity

```bash
# Test connection to server
make ping

# Or manually
ansible -i inventory/hosts.yml ghost_servers -m ping
```

Expected output:
```
ghost-prod | SUCCESS => {
    "changed": false,
    "ping": "pong"
}
```

### Step 8: Run Setup (Dry Run)

```bash
# Check what will be changed (doesn't make changes)
make setup-check

# Or manually
ansible-playbook -i inventory/hosts.yml setup.yml --check
```

### Step 9: Run Full Setup

```bash
# Run complete server setup
make setup

# Or manually
ansible-playbook -i inventory/hosts.yml setup.yml
```

This will:
- ✅ Update system packages
- ✅ Configure timezone and locale
- ✅ Install Docker and Docker Compose
- ✅ Configure firewall (UFW)
- ✅ Harden SSH
- ✅ Install fail2ban
- ✅ Setup monitoring (Prometheus node exporter)
- ✅ Configure backup scripts

**Duration**: 10-15 minutes

### Step 10: Deploy Ghost Platform

```bash
# Deploy the application
make deploy

# Or manually
ansible-playbook -i inventory/hosts.yml deploy.yml
```

This will:
- ✅ Clone Ghost Platform repository
- ✅ Configure environment variables
- ✅ Deploy Docker Compose stack
- ✅ Start all services
- ✅ Wait for health checks

**Duration**: 5-10 minutes

### Step 11: Verify Deployment

```bash
# Run health checks
make health

# Or manually
ansible-playbook -i inventory/hosts.yml health-check.yml
```

### Step 12: Access Your Ghost Platform

Open your browser and navigate to:

- **Ghost API**: http://YOUR_SERVER_IP:8080
- **Health Check**: http://YOUR_SERVER_IP:8080/health
- **RabbitMQ Management**: http://YOUR_SERVER_IP:15672 (guest/guest)
- **Grafana**: http://YOUR_SERVER_IP:3000 (admin/[see below])
- **Prometheus**: http://YOUR_SERVER_IP:9090

To find Grafana password:

```bash
# On your local machine
ansible -i inventory/hosts.yml ghost_servers -m shell -a "cat /opt/ghost/deploy/docker/.env | grep GRAFANA_PASSWORD"
```

## Post-Deployment Configuration

### Configure SSL with Let's Encrypt (Optional)

```bash
# SSH to your server
ssh ubuntu@YOUR_SERVER_IP

# Install certbot
sudo apt install certbot

# Generate certificate
sudo certbot certonly --standalone -d ghost.example.com

# Copy certificates
sudo cp /etc/letsencrypt/live/ghost.example.com/fullchain.pem /opt/ghost/deploy/docker/nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/ghost.example.com/privkey.pem /opt/ghost/deploy/docker/nginx/ssl/key.pem

# Update configuration
cd /opt/ghost/deploy/docker
# Edit .env and set enable_ssl=true

# Restart services
ghost-ctl restart
```

### Configure S3 Backups (Optional)

Edit `group_vars/all.yml`:

```yaml
backup_s3_bucket: "my-ghost-backups"
backup_s3_region: "us-east-1"
backup_s3_access_key: "YOUR_ACCESS_KEY"
backup_s3_secret_key: "YOUR_SECRET_KEY"
```

Then re-run:

```bash
make setup-backup
```

### Configure Platform Features

Edit `group_vars/all.yml`:

```yaml
linkedin_enabled: true
linkedin_country: "US"
indeed_enabled: true
indeed_country: "US"
glassdoor_enabled: true
```

Then redeploy:

```bash
make deploy
```

## Daily Operations

### Check System Health

```bash
make health
```

### View Logs

```bash
# SSH to server
ssh ubuntu@YOUR_SERVER_IP

# View all logs
ghost-ctl logs

# View specific service logs
ghost-ctl logs ghost-webapi
ghost-ctl logs postgres
```

### Manual Backup

```bash
make backup

# Or on server
ssh ubuntu@YOUR_SERVER_IP
ghost-ctl backup
```

### Update Ghost Platform

```bash
make update
```

### Restart Services

```bash
# SSH to server
ssh ubuntu@YOUR_SERVER_IP

# Restart all services
ghost-ctl restart

# Or specific service
cd /opt/ghost/deploy/docker
docker compose restart ghost-webapi
```

## Troubleshooting

### Connection Issues

```bash
# Test SSH connection
ssh -v ubuntu@YOUR_SERVER_IP

# Check Ansible connectivity
ansible -i inventory/hosts.yml ghost_servers -m ping -vvv
```

### Service Not Starting

```bash
# SSH to server
ssh ubuntu@YOUR_SERVER_IP

# Check container status
ghost-ctl status

# View logs
ghost-ctl logs

# Check Docker
sudo docker ps -a
sudo docker logs ghost-webapi
```

### Firewall Blocking Access

```bash
# SSH to server
ssh ubuntu@YOUR_SERVER_IP

# Check firewall
sudo ufw status

# Allow port if needed
sudo ufw allow 8080/tcp
```

### Out of Memory

```bash
# Check memory usage
free -h

# Check container resource usage
docker stats

# Consider upgrading server or optimizing container limits
```

## Next Steps

- [ ] Configure DNS to point to your server
- [ ] Setup SSL certificates
- [ ] Configure S3 backups
- [ ] Setup monitoring alerts
- [ ] Review security settings
- [ ] Configure platform features (LinkedIn, Indeed, etc.)
- [ ] Setup log aggregation
- [ ] Create restore procedures document
- [ ] Schedule regular health checks

## Getting Help

### Check Logs

```bash
# Ansible logs
cat ~/.ansible.log

# Server logs
ssh ubuntu@YOUR_SERVER_IP
tail -f /var/log/ghost/backup.log
ghost-ctl logs
```

### Common Issues

1. **Cannot connect to server**: Check SSH keys and firewall
2. **Docker not installed**: Re-run `make setup-docker`
3. **Services not starting**: Check logs with `ghost-ctl logs`
4. **Out of disk space**: Run backups cleanup or expand disk

### Support Resources

- GitHub Issues: https://github.com/rudironsoni/Ghost/issues
- Documentation: ../docs/
- Email: admin@example.com

---

**Congratulations!** You've successfully deployed Ghost Platform Ultra Miser Mode! 🎉
