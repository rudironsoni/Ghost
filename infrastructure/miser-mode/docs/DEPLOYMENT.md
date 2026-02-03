# Ghost Platform - Ultra Miser Mode Deployment Guide

Complete deployment guide for the Ultra Miser Mode infrastructure ($0-15/month).

## Quick Start (5 minutes)

```bash
# 1. Clone and navigate
cd /path/to/Ghost/infrastructure/miser-mode/docker

# 2. Configure environment
cp .env.example .env
# Edit .env with your settings

# 3. Start services
docker-compose up -d

# 4. Verify
curl http://localhost:8080/health
```

## Prerequisites

- Docker 20.10+ and Docker Compose 2.0+
- 4 CPU cores, 8GB RAM, 100GB disk
- Linux (Ubuntu 22.04 recommended) or macOS

## Deployment Options

### Option 1: Local Development

```bash
cd infrastructure/miser-mode/docker
docker-compose up -d
```

### Option 2: Cloud VPS (Hetzner)

```bash
# Using Terraform
cd infrastructure/miser-mode/terraform
terraform init
terraform apply -var="provider=hetzner"

# Then SSH and deploy
ssh root@<server-ip>
cd /opt/ghost
docker-compose up -d
```

### Option 3: Oracle Cloud Free Tier

```bash
# Using Terraform
cd infrastructure/miser-mode/terraform
terraform init
terraform apply -var="provider=oracle"
```

### Option 4: Ansible Deployment

```bash
# Using Ansible
cd infrastructure/miser-mode/ansible
ansible-playbook -i inventory/hosts.yml setup.yml
ansible-playbook -i inventory/hosts.yml deploy.yml
```

## Post-Deployment

### Verify Installation

```bash
# Check all services
docker-compose ps

# Run health check
./scripts/health-check.sh

# View logs
docker-compose logs -f ghost-webapi
```

### Access Points

| Service | URL | Default Credentials |
|---------|-----|---------------------|
| Ghost API | http://localhost:8080 | - |
| RabbitMQ | http://localhost:15672 | guest/guest |
| Grafana | http://localhost:3000 | admin/admin |
| Prometheus | http://localhost:9090 | - |

### Configure Platforms

Edit `.env` and enable desired platforms:

```env
LINKEDIN_ENABLED=true
LINKEDIN_COUNTRY=US
LINKEDIN_USERNAME=your-username
LINKEDIN_PASSWORD=your-password

INDEED_ENABLED=true
INDEED_API_KEY=your-api-key
```

Restart to apply:
```bash
docker-compose restart ghost-webapi
```

## Troubleshooting

### Service Won't Start

```bash
# Check logs
docker-compose logs <service-name>

# Check resources
docker stats

# Verify configuration
docker-compose config
```

### Database Connection Failed

```bash
# Check PostgreSQL is running
docker-compose ps postgres

# Check logs
docker-compose logs postgres

# Verify credentials in .env
cat .env | grep DB_PASSWORD
```

### Out of Memory

Reduce memory limits in `docker-compose.yml`:

```yaml
services:
  postgres:
    deploy:
      resources:
        limits:
          memory: 1G  # Reduce from 2G
```

## Backup and Restore

### Automated Backups

Backups run daily at 2 AM automatically via the backup service.

### Manual Backup

```bash
./scripts/backup.sh --full
```

### Restore

```bash
./scripts/restore.sh backups/archives/ghost-backup-20240203_120000.tar.gz
```

## Updating

```bash
# Pull latest images
docker-compose pull

# Restart services
docker-compose up -d

# Verify
./scripts/health-check.sh
```

## Production Checklist

- [ ] Changed default passwords in `.env`
- [ ] Enabled SSL/TLS certificates
- [ ] Configured backups to S3
- [ ] Set up monitoring alerts
- [ ] Documented custom configurations
- [ ] Tested backup/restore procedure
- [ ] Verified health checks pass