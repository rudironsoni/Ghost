# Ghost Platform - Ultra Miser Mode Infrastructure

Cost-optimized infrastructure for Ghost Platform ($0-15/month) maintaining full architectural integrity.

## Overview

Ultra Miser Mode provides a single-node deployment that preserves all enterprise patterns:
- ✅ Transactional Outbox Pattern (PostgreSQL)
- ✅ Event-Driven Architecture (RabbitMQ)
- ✅ Circuit Breakers & Resilience
- ✅ Distributed State (Redis)
- ✅ Comprehensive Monitoring
- ✅ Automated Backups

**Cost:** $0-15/month vs $1,250+ for distributed architecture

## Quick Start

```bash
cd docker
cp .env.example .env
# Edit .env with your settings
docker-compose up -d
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│              Single Node (4 vCPU / 8GB RAM)         │
├─────────────────────────────────────────────────────┤
│  PostgreSQL (2GB) + Redis (1GB) + RabbitMQ (1GB)   │
│  Ghost App (3GB) + Nginx + Prometheus + Grafana     │
└─────────────────────────────────────────────────────┘
```

## Directory Structure

```
miser-mode/
├── docker/                 # Docker Compose stack
│   ├── docker-compose.yml  # Main orchestration
│   ├── .env.example        # Configuration template
│   ├── nginx/              # Reverse proxy config
│   ├── rabbitmq/           # Message broker topology
│   ├── init-scripts/       # Database initialization
│   └── monitoring/         # Prometheus & Grafana
├── terraform/              # Infrastructure as Code
│   ├── modules/            # Provider modules
│   ├── main.tf             # Main configuration
│   └── variables.tf        # Input variables
├── ansible/                # Configuration management
│   ├── setup.yml           # Server provisioning
│   ├── deploy.yml          # Application deployment
│   └── roles/              # Ansible roles
├── scripts/                # Operational scripts
│   ├── backup.sh           # Backup automation
│   ├── restore.sh          # Restore from backup
│   └── health-check.sh     # Health validation
├── docs/                   # Documentation
│   ├── DEPLOYMENT.md       # Deployment guide
│   └── OPERATIONS.md       # Operations runbook
├── MIGRATION.md            # Migration from distributed
└── README.md               # This file
```

## Deployment Options

### Option 1: Local Development
```bash
cd docker
docker-compose up -d
```

### Option 2: Hetzner Cloud ($11/month)
```bash
cd terraform
terraform init
terraform apply -var="provider=hetzner"
```

### Option 3: Oracle Cloud Free Tier ($0)
```bash
cd terraform
terraform apply -var="provider=oracle"
```

### Option 4: Ansible Deployment
```bash
cd ansible
ansible-playbook -i inventory/hosts.yml setup.yml
ansible-playbook -i inventory/hosts.yml deploy.yml
```

## Services

| Service | Port | Purpose |
|---------|------|---------|
| Ghost API | 8080 | Main application API |
| Nginx | 80/443 | Reverse proxy & SSL |
| PostgreSQL | 5432 | Database + Outbox pattern |
| Redis | 6379 | Cache & session store |
| RabbitMQ | 5672 | Message broker |
| RabbitMQ Mgmt | 15672 | Queue management UI |
| Prometheus | 9090 | Metrics collection |
| Grafana | 3000 | Metrics dashboards |

## Configuration

Copy `.env.example` to `.env` and configure:

```bash
# Database
DB_PASSWORD=your-secure-password

# RabbitMQ
RABBITMQ_PASSWORD=your-secure-password

# Platform Credentials (optional)
LINKEDIN_ENABLED=true
LINKEDIN_USERNAME=your-username
LINKEDIN_PASSWORD=your-password

# Backup (optional)
S3_BUCKET=your-backup-bucket
AWS_ACCESS_KEY_ID=your-key
AWS_SECRET_ACCESS_KEY=your-secret
```

## Operations

### Health Check
```bash
./scripts/health-check.sh
```

### Backup
```bash
# Manual backup
./scripts/backup.sh --full

# Automated backups run daily at 2 AM
```

### Restore
```bash
./scripts/restore.sh backups/archives/backup.tar.gz
```

### View Logs
```bash
docker-compose logs -f ghost-webapi
```

## Migration from Distributed Architecture

See [MIGRATION.md](MIGRATION.md) for detailed migration procedures.

Quick migration:
```bash
./scripts/migrate.sh --source=current-system --target=new-system
```

## Monitoring

Access Grafana at http://localhost:3000 (admin/admin)

Dashboards included:
- Infrastructure Overview
- Application Performance
- Database Metrics
- Cache Performance
- Message Queue
- Business Metrics

## Scaling Path

| Revenue | Action | Cost |
|---------|--------|------|
| $0-100 | Optimize single node | $11/mo |
| $100-500 | Split DB to dedicated node | $30/mo |
| $500-1K | Horizontal scaling | $65/mo |
| $1K-5K | Kubernetes cluster | $150/mo |

## Tech Stack

- **Container Orchestration**: Docker Compose
- **Database**: PostgreSQL 16
- **Cache**: Redis 7
- **Message Broker**: RabbitMQ 3.13
- **Reverse Proxy**: Nginx
- **Monitoring**: Prometheus + Grafana
- **IaC**: Terraform + Ansible
- **Scripts**: Bash

## Contributing

This infrastructure follows:
- Infrastructure as Code principles
- Idempotent automation
- Security-first design
- Cost optimization
- Clear migration paths

## License

Same as Ghost Platform project