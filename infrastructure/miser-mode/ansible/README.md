# Ghost Platform Ultra Miser Mode - Ansible Automation

Production-ready Ansible playbooks for deploying and managing Ghost Platform on a single Ubuntu 22.04 server.

## 🎯 Overview

This Ansible automation provides:
- Complete server setup and hardening
- Docker and Docker Compose installation
- Ghost Platform deployment
- Automated backups (local + S3)
- Monitoring with Prometheus and Grafana
- Security hardening (firewall, fail2ban, SSH)

## 📋 Requirements

### Control Machine (Your Laptop/Workstation)
- Ansible 2.10 or later
- Python 3.8+
- SSH access to target server

```bash
# Install Ansible
pip install ansible

# Verify installation
ansible --version
```

### Target Server
- Ubuntu 22.04 LTS
- Minimum 4GB RAM, 2 CPU cores, 20GB disk
- SSH access with sudo privileges
- Python 3.8+ installed

## 🚀 Quick Start

### 1. Configure Inventory

Copy the example inventory and customize:

```bash
cp inventory/hosts.yml.example inventory/hosts.yml
```

Edit `inventory/hosts.yml`:

```yaml
all:
  children:
    ghost_servers:
      hosts:
        ghost-prod:
          ansible_host: YOUR_SERVER_IP
          ansible_user: ubuntu
          
          # Configuration
          domain_name: ghost.example.com
          enable_ssl: true
          admin_email: admin@example.com
```

### 2. Configure Variables

Edit `group_vars/all.yml` to customize:

```yaml
domain_name: "ghost.example.com"
enable_ssl: true
admin_email: "admin@example.com"
backup_s3_bucket: "my-ghost-backups"
```

### 3. Run Server Setup

```bash
# Full setup (recommended for new servers)
ansible-playbook -i inventory/hosts.yml setup.yml

# Check mode (dry-run)
ansible-playbook -i inventory/hosts.yml setup.yml --check

# Run specific roles
ansible-playbook -i inventory/hosts.yml setup.yml --tags docker
```

### 4. Deploy Ghost Platform

```bash
# Deploy application
ansible-playbook -i inventory/hosts.yml deploy.yml

# Deploy with custom version
ansible-playbook -i inventory/hosts.yml deploy.yml --extra-vars "ghost_version=v1.0.0"
```

## 📁 Directory Structure

```
ansible/
├── ansible.cfg                 # Ansible configuration
├── setup.yml                   # Server setup playbook
├── deploy.yml                  # Application deployment playbook
├── inventory/                  # Server inventory
│   ├── hosts.yml.example       # YAML inventory example
│   └── hosts.ini.example       # INI inventory example
├── group_vars/
│   └── all.yml                 # Global variables
└── roles/
    ├── common/                 # System updates, timezone, packages
    ├── docker/                 # Docker installation
    ├── monitoring/             # Prometheus node exporter
    ├── backup/                 # Backup scripts and cron
    ├── security/               # Firewall, fail2ban, SSH hardening
    └── ghost/                  # Ghost Platform deployment
```

## 🎭 Roles

### Common Role
- System updates and package installation
- Timezone and locale configuration
- Swap file creation
- System limits and kernel tuning
- Unattended security updates

### Docker Role
- Docker CE installation
- Docker Compose plugin
- Docker daemon configuration
- Network setup

### Security Role
- UFW firewall configuration
- SSH hardening
- Fail2ban installation
- Security auditing tools

### Monitoring Role
- Prometheus node exporter
- System monitoring tools
- Sysstat configuration

### Backup Role
- PostgreSQL backup scripts
- Redis backup scripts
- Full system backup
- S3 sync (optional)
- Automated cleanup

### Ghost Role
- Application deployment
- Docker Compose orchestration
- Nginx configuration
- SSL certificate management
- Systemd service

## ⚙️ Configuration Variables

### Required Variables

```yaml
domain_name: "ghost.example.com"
admin_email: "admin@example.com"
```

### Optional Variables

```yaml
# SSL Configuration
enable_ssl: true

# Backup Configuration
backup_s3_bucket: "my-ghost-backups"
backup_s3_region: "us-east-1"
backup_s3_access_key: "ACCESS_KEY"
backup_s3_secret_key: "SECRET_KEY"
backup_retention_days: 30

# Docker Configuration
docker_network_range: "172.20.0.0/16"

# Platform Features
linkedin_enabled: true
indeed_enabled: true
glassdoor_enabled: false

# Security
ssh_port: 22
fail2ban_enabled: true
firewall_trusted_ips:
  - "1.2.3.4/32"
```

## 🎯 Usage Examples

### Complete New Server Setup

```bash
# 1. Prepare inventory
cp inventory/hosts.yml.example inventory/hosts.yml
vim inventory/hosts.yml

# 2. Run setup (installs everything)
ansible-playbook -i inventory/hosts.yml setup.yml

# 3. Deploy Ghost Platform
ansible-playbook -i inventory/hosts.yml deploy.yml
```

### Update Existing Deployment

```bash
# Update Ghost Platform to latest version
ansible-playbook -i inventory/hosts.yml deploy.yml --tags update

# Update specific service
ansible-playbook -i inventory/hosts.yml deploy.yml --tags ghost --extra-vars "ghost_version=latest"
```

### Run Specific Tasks

```bash
# Install Docker only
ansible-playbook -i inventory/hosts.yml setup.yml --tags docker

# Configure firewall only
ansible-playbook -i inventory/hosts.yml setup.yml --tags security,firewall

# Setup backups only
ansible-playbook -i inventory/hosts.yml setup.yml --tags backup
```

### Dry Run (Check Mode)

```bash
# Test without making changes
ansible-playbook -i inventory/hosts.yml setup.yml --check --diff

# Verify deployment
ansible-playbook -i inventory/hosts.yml deploy.yml --check
```

## 🔐 Security Features

- **UFW Firewall**: Blocks all incoming except specified ports
- **Fail2ban**: Automatic IP banning for failed login attempts
- **SSH Hardening**: 
  - Root login disabled
  - Password authentication disabled
  - Key-based authentication only
- **Automatic Updates**: Unattended security updates enabled
- **Audit Logging**: System activity monitoring
- **Container Isolation**: Docker network segmentation

## 💾 Backup System

### Automated Backups

- **PostgreSQL**: Daily at 2 AM
- **Redis**: Daily at 2 AM
- **Full System**: Weekly (Sunday at 2 AM)
- **S3 Sync**: Daily at 2:30 AM (if configured)
- **Cleanup**: Daily at 4 AM

### Manual Backup

```bash
# On the target server
/usr/local/bin/ghost-backup/backup-full.sh

# Or using ghost-ctl
ghost-ctl backup
```

### Restore

See `{{ ghost_backup_dir }}/RESTORE.md` on the server for detailed restore procedures.

## 📊 Monitoring

After deployment, access:

- **Ghost API**: `http://your-server:8080`
- **Grafana**: `http://your-server:3000` (admin/[generated-password])
- **Prometheus**: `http://your-server:9090`
- **RabbitMQ Management**: `http://your-server:15672`
- **Node Exporter**: `http://your-server:9100/metrics`

## 🛠️ Management

### Using ghost-ctl Script

```bash
# Start services
ghost-ctl start

# Stop services
ghost-ctl stop

# Restart services
ghost-ctl restart

# Check status
ghost-ctl status

# View logs
ghost-ctl logs
ghost-ctl logs ghost-webapi

# Update to latest version
ghost-ctl update

# Run backup
ghost-ctl backup
```

### Using Systemd

```bash
# Start Ghost Platform
sudo systemctl start ghost-platform

# Enable auto-start on boot
sudo systemctl enable ghost-platform

# Check status
sudo systemctl status ghost-platform
```

## 🔧 Troubleshooting

### Ansible Connection Issues

```bash
# Test connectivity
ansible -i inventory/hosts.yml ghost_servers -m ping

# Run with verbose output
ansible-playbook -i inventory/hosts.yml setup.yml -vvv
```

### Check Service Status

```bash
# On target server
ghost-ctl status
docker compose ps

# Check logs
ghost-ctl logs
journalctl -u ghost-platform -f
```

### Firewall Issues

```bash
# Check firewall rules
sudo ufw status verbose

# Allow additional port
sudo ufw allow 8080/tcp
```

## 📚 Additional Resources

- [Ghost Platform Documentation](../docs/)
- [Docker Compose Configuration](../docker/)
- [Terraform Infrastructure](../terraform/)

## 🤝 Contributing

Improvements and suggestions are welcome! Please ensure:
- Playbooks remain idempotent
- Variables are properly documented
- Security best practices are followed

## 📄 License

Part of Ghost Platform Ultra Miser Mode - See main repository for license.

## 📞 Support

For issues or questions:
- Email: admin@example.com
- GitHub Issues: [Ghost Platform Repository]

---

**Note**: Always test in a staging environment before deploying to production.
