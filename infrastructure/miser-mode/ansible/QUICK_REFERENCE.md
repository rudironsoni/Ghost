# Ghost Platform Ansible - Quick Reference

## 🚀 Quick Commands

### Setup & Deployment
```bash
make install              # Install Ansible dependencies
make ping                 # Test server connectivity
make setup                # Complete server setup
make deploy               # Deploy Ghost Platform
make update               # Update to latest version
```

### Health & Monitoring
```bash
make health               # Run health checks
make backup               # Trigger backup
```

### Targeted Operations
```bash
make setup-docker         # Install Docker only
make setup-security       # Configure security only
make setup-monitoring     # Setup monitoring only
make setup-backup         # Configure backups only
```

## 📁 Important Files

| File | Purpose |
|------|---------|
| `inventory/hosts.yml` | Server inventory |
| `group_vars/all.yml` | Global variables |
| `setup.yml` | Server setup playbook |
| `deploy.yml` | Application deployment |
| `update.yml` | Update playbook |
| `health-check.yml` | Health check playbook |
| `backup.yml` | Backup playbook |

## 🎯 Common Variables

```yaml
# Domain & SSL
domain_name: "ghost.example.com"
enable_ssl: true
admin_email: "admin@example.com"

# Backup
backup_s3_bucket: "my-backups"
backup_retention_days: 30

# Platform Features
linkedin_enabled: true
indeed_enabled: true

# Security
ssh_port: 22
fail2ban_enabled: true
```

## 🔧 Server Management

### Using ghost-ctl (on server)
```bash
ghost-ctl start           # Start services
ghost-ctl stop            # Stop services
ghost-ctl restart         # Restart services
ghost-ctl status          # Show status
ghost-ctl logs            # View logs
ghost-ctl logs SERVICE    # View specific service logs
ghost-ctl update          # Update Ghost
ghost-ctl backup          # Run backup
```

### Using systemd (on server)
```bash
sudo systemctl start ghost-platform
sudo systemctl stop ghost-platform
sudo systemctl restart ghost-platform
sudo systemctl status ghost-platform
```

## 📊 Service URLs

| Service | URL |
|---------|-----|
| Ghost API | http://SERVER:8080 |
| Health Check | http://SERVER:8080/health |
| RabbitMQ | http://SERVER:15672 |
| Grafana | http://SERVER:3000 |
| Prometheus | http://SERVER:9090 |
| Node Exporter | http://SERVER:9100/metrics |

## 🔐 Default Credentials

Generated passwords are stored in:
- `.env` file on server: `/opt/ghost/deploy/docker/.env`
- Retrieve via Ansible:
  ```bash
  ansible -i inventory/hosts.yml ghost_servers -m shell -a "cat /opt/ghost/deploy/docker/.env"
  ```

## 📂 Important Directories

| Directory | Purpose |
|-----------|---------|
| `/opt/ghost` | Installation directory |
| `/opt/ghost/deploy/docker` | Docker Compose files |
| `/var/lib/ghost` | Application data |
| `/var/backups/ghost` | Local backups |
| `/var/log/ghost` | Application logs |

## 🔄 Update Process

```bash
# 1. Update Ansible playbooks (if needed)
git pull

# 2. Run update playbook
make update

# 3. Verify deployment
make health
```

## 💾 Backup & Restore

### Backup
```bash
# From local machine
make backup

# Or on server
ghost-ctl backup
```

### Restore
```bash
# SSH to server
ssh ubuntu@SERVER_IP

# Stop services
cd /opt/ghost/deploy/docker
docker compose stop ghost-webapi

# Restore PostgreSQL
gunzip -c /var/backups/ghost/postgres/latest.sql.gz | \
  docker exec -i ghost-postgres psql -U ghost

# Start services
docker compose start ghost-webapi
```

## 🐛 Troubleshooting

### Check Service Status
```bash
# On server
ghost-ctl status
docker ps
docker compose ps
```

### View Logs
```bash
# All services
ghost-ctl logs

# Specific service
ghost-ctl logs ghost-webapi

# Docker logs directly
docker logs ghost-webapi -f
```

### Check Resources
```bash
# Memory and CPU
free -h
top

# Disk space
df -h
du -sh /var/lib/docker
du -sh /opt/ghost
```

### Network Issues
```bash
# Test API
curl http://localhost:8080/health

# Check firewall
sudo ufw status

# Check listening ports
sudo netstat -tlnp
```

## 📦 Ansible Tags

| Tag | Purpose |
|-----|---------|
| `common` | System updates, packages |
| `docker` | Docker installation |
| `security` | Firewall, SSH, fail2ban |
| `monitoring` | Prometheus, metrics |
| `backup` | Backup configuration |
| `ghost` | Application deployment |
| `update` | Update application |

### Usage
```bash
# Run specific role
ansible-playbook -i inventory/hosts.yml setup.yml --tags docker

# Skip specific role
ansible-playbook -i inventory/hosts.yml setup.yml --skip-tags security
```

## 🎨 Ansible Options

```bash
# Dry run (no changes)
--check

# Show differences
--diff

# Verbose output
-v, -vv, -vvv

# Limit to specific hosts
--limit ghost-prod

# Extra variables
--extra-vars "domain_name=ghost.example.com"
```

## 📋 Pre-flight Checklist

Before running setup:
- [ ] Server is Ubuntu 22.04
- [ ] SSH access configured
- [ ] Inventory file updated
- [ ] Variables configured
- [ ] Connectivity tested (`make ping`)

Before deploying to production:
- [ ] Backups configured
- [ ] SSL certificates ready
- [ ] DNS configured
- [ ] Monitoring setup
- [ ] Security hardened
- [ ] Tested in staging

## 🆘 Emergency Procedures

### Service Down
```bash
ssh ubuntu@SERVER
ghost-ctl restart
```

### High CPU/Memory
```bash
ssh ubuntu@SERVER
docker stats
docker compose restart
```

### Disk Full
```bash
ssh ubuntu@SERVER
docker system prune -a
/usr/local/bin/ghost-backup/cleanup-backups.sh
```

### Restore from Backup
```bash
# See /var/backups/ghost/RESTORE.md on server
ssh ubuntu@SERVER
cat /var/backups/ghost/RESTORE.md
```

## 📞 Support

- GitHub: https://github.com/rudironsoni/Ghost
- Docs: ../docs/
- Issues: https://github.com/rudironsoni/Ghost/issues

---

**Need more details?** See full documentation in [README.md](README.md) and [GETTING_STARTED.md](GETTING_STARTED.md)
