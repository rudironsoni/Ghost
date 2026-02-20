# Ghost Platform - Enterprise Infrastructure

Enterprise-grade, production-ready infrastructure for the Ghost Platform.

## Overview

This infrastructure provides a scalable, secure, and cost-optimized deployment of the Ghost Platform using industry best practices.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      GHOST PLATFORM ENTERPRISE                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    ENVIRONMENTS                           │   │
│  │  ┌────────────┐  ┌───────────┐  ┌──────────────────┐    │   │
│  │  │Development │  │  Staging  │  │   Production     │    │   │
│  │  └────────────┘  └───────────┘  └──────────────────┘    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    PLATFORM (Kubernetes)                  │   │
│  │  ┌────────────┐  ┌───────────┐  ┌──────────────────┐    │   │
│  │  │   Base     │  │ Services  │  │    Policies      │    │   │
│  │  │(Ingress,   │  │(Ghost App)│  │  (OPA, Security) │    │   │
│  │  │Cert-Manager│  │           │  │                  │    │   │
│  │  └────────────┘  └───────────┘  └──────────────────┘    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    MODULES (Terraform)                    │   │
│  │  ┌───────┐ ┌──────────┐ ┌─────────┐ ┌────────────────┐  │   │
│  │  │Compute│ │Networking│ │ Database│ │     Cache      │  │   │
│  │  └───────┘ └──────────┘ └─────────┘ └────────────────┘  │   │
│  │  ┌───────────────┐  ┌──────────────┐  ┌──────────────┐  │   │
│  │  │   Messaging   │  │  Monitoring  │  │   Security   │  │   │
│  │  │  (RabbitMQ)   │  │(Prom/Grafana)│  │ (Vault, OPA) │  │   │
│  │  └───────────────┘  └──────────────┘  └──────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Directory Structure

```
infrastructure/
├── environments/          # Environment-specific configurations
│   ├── development/       # Dev environment
│   ├── staging/          # Staging environment
│   └── production/       # Production environment
│
├── modules/              # Reusable Terraform modules
│   ├── compute/          # VMs, containers
│   ├── networking/       # VPC, subnets, firewalls
│   ├── database/         # PostgreSQL
│   ├── cache/            # Redis
│   ├── messaging/        # RabbitMQ
│   ├── monitoring/       # Prometheus, Grafana
│   └── security/         # IAM, policies
│
├── platform/             # Kubernetes platform
│   ├── base/             # Core infrastructure
│   ├── services/         # Application services
│   └── policies/         # OPA/Gatekeeper policies
│
├── automation/           # CI/CD & GitOps
│   ├── pipelines/        # Azure DevOps/GitHub Actions
│   ├── scripts/          # Automation scripts
│   └── templates/        # Templates
│
├── observability/        # Monitoring & logging
│   ├── prometheus/       # Metrics collection
│   ├── grafana/          # Dashboards
│   ├── loki/             # Log aggregation
│   └── alerts/           # Alerting rules
│
├── security/             # Security tooling
│   ├── vault/            # HashiCorp Vault
│   ├── policies/         # Security policies
│   └── scanning/         # Security scanning
│
└── docs/                 # Documentation
    ├── runbooks/         # Operational runbooks
    ├── playbooks/        # Ansible playbooks
    └── adr/              # Architecture Decision Records
```

## Quick Start

### Prerequisites

- Terraform 1.5+
- kubectl 1.28+
- Helm 3.12+
- Docker 24+

### Development Environment

```bash
# 1. Initialize Terraform
cd environments/development
terraform init

# 2. Review the plan
terraform plan

# 3. Apply
terraform apply

# 4. Configure kubectl
aws eks update-kubeconfig --region us-east-1 --name ghost-dev
# or
gcloud container clusters get-credentials ghost-dev --region us-east1

# 5. Deploy platform
kubectl apply -k ../../platform/base
kubectl apply -k ../../platform/services
```

## Cost Optimization

### Development
- Single node k3s cluster
- Spot/preemptible instances
- Automated shutdown scheduler
- **Cost: ~$50/month**

### Staging
- 3-node cluster
- Mixed on-demand/spot
- **Cost: ~$150/month**

### Production
- 3+ node HA cluster
- Reserved instances
- Multi-AZ deployment
- **Cost: ~$500-800/month**

## Security

- **Secrets Management:** HashiCorp Vault
- **Policy Enforcement:** OPA/Gatekeeper
- **Network Security:** Calico/Calico Enterprise
- **Image Scanning:** Trivy/Snyk
- **Compliance:** CIS Benchmarks, SOC2

## Monitoring

- **Metrics:** Prometheus + Thanos
- **Logs:** Loki + Grafana
- **Tracing:** Jaeger/Tempo
- **APM:** OpenTelemetry
- **Dashboards:** Pre-configured Grafana dashboards

## Support

- **Issues:** GitHub Issues
- **Documentation:** See `docs/` directory
- **Runbooks:** See `docs/runbooks/` (including `cloud-canary-data-quality-alerts.md` for SLO alert response)
