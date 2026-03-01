# Ghost Platform - Infrastructure Documentation

## Overview

Ghost Platform provides enterprise-grade, production-ready infrastructure supporting multi-environment deployments with comprehensive security, observability, and automation.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    GHOST PLATFORM ENTERPRISE                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    ENVIRONMENTS                           │   │
│  │  ┌────────────┐  ┌───────────┐  ┌──────────────────┐    │   │
│  │  │Development │  │  Staging  │  │   Production     │    │   │
│  │  │  (k3s)     │  │  (EKS)    │  │   (EKS HA)       │    │   │
│  │  │  $50/mo    │  │  $150/mo  │  │   $500-800/mo    │    │   │
│  │  └────────────┘  └───────────┘  └──────────────────┘    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    KUBERNETES PLATFORM                    │   │
│  │  ┌────────────┐  ┌───────────┐  ┌──────────────────┐    │   │
│  │  │   Base     │  │ Services  │  │    Policies      │    │   │
│  │  │(Ingress,   │  │(Ghost App)│  │  (OPA, Network)  │    │   │
│  │  │Cert-Manager│  │           │  │                  │    │   │
│  │  └────────────┘  └───────────┘  └──────────────────┘    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    ENTERPRISE STACK                       │   │
│  │  ┌────────┐ ┌──────────┐ ┌─────────┐ ┌────────────────┐  │   │
│  │  │Terraform│ │   EKS    │ │HashiCorp│ │   Prometheus   │  │   │
│  │  │  IaC   │ │Kubernetes│ │  Vault  │ │    Grafana     │  │   │
│  │  └────────┘ └──────────┘ └─────────┘ └────────────────┘  │   │
│  │  ┌────────────────┐  ┌──────────────┐  ┌──────────────┐  │   │
│  │  │  GitOps/ArgoCD │  │  OPA/Falco   │  │ Trivy/Scan   │  │   │
│  │  │   CI/CD        │  │   Security   │  │  Compliance  │  │   │
│  │  └────────────────┘  └──────────────┘  └──────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Quick Start

### Prerequisites

- Terraform 1.5+
- kubectl 1.28+
- Helm 3.12+
- AWS CLI configured

### Development Environment

```bash
cd infrastructure/environments/development
terraform init
terraform plan
terraform apply

# Configure kubectl
aws eks update-kubeconfig --region us-east-1 --name ghost-dev

# Deploy platform
kubectl apply -k ../../platform/base
kubectl apply -k ../../platform/services
```

### Production Environment

```bash
cd infrastructure/environments/production
terraform init
terraform plan
terraform apply

# Configure kubectl
aws eks update-kubeconfig --region us-east-1 --name ghost-prod

# Deploy via ArgoCD
kubectl apply -f ../../automation/pipelines/argocd-application.yaml
```

## Directory Structure

```
infrastructure/
├── README.md                    # Executive overview
├── ARCHITECTURE.md              # Architecture Decision Records
├── FINAL_SUMMARY.md             # Complete implementation summary
│
├── environments/                # Environment-specific configs
│   ├── development/             # k3s, spot instances ($50/mo)
│   └── production/              # EKS HA, multi-AZ ($500-800/mo)
│
├── modules/                     # Reusable Terraform modules
│   ├── compute/                 # EKS, node groups, Karpenter
│   └── networking/              # VPC, subnets, security groups
│
├── platform/                    # Kubernetes manifests
│   ├── base/                    # Core infrastructure
│   ├── services/                # Application workloads
│   └── policies/                # Security policies
│
├── observability/               # Monitoring & logging
│   ├── prometheus/              # Metrics collection
│   ├── grafana/                 # Dashboards
│   ├── loki/                    # Log aggregation
│   └── alerts/                  # Alerting rules
│
├── security/                    # Enterprise security
│   ├── vault/                   # HashiCorp Vault
│   ├── policies/                # OPA Gatekeeper
│   └── scanning/                # Trivy, Falco
│
├── automation/                  # CI/CD & GitOps
│   ├── pipelines/               # GitHub Actions, Azure DevOps
│   ├── scripts/                 # Deployment scripts
│   └── templates/helm-chart/    # Helm chart
│
└── docs/                        # Operational documentation
    └── runbooks/                # Deployment, incident response, DR
```

## Environments

### Development
- **Platform**: k3s single-node cluster
- **Cost**: ~$50/month
- **Features**: Spot instances, automated shutdown
- **Use Case**: Development, testing, experimentation

### Staging
- **Platform**: EKS 3-node cluster
- **Cost**: ~$150/month
- **Features**: Production-like, mixed on-demand/spot
- **Use Case**: Pre-production validation

### Production
- **Platform**: EKS multi-AZ HA cluster
- **Cost**: ~$500-800/month
- **Features**: Reserved instances, multi-AZ, full HA
- **Use Case**: Production workloads

## Enterprise Features

### Security
- **HashiCorp Vault**: Secrets management with HA
- **OPA Gatekeeper**: Policy enforcement
- **Falco**: Runtime security monitoring
- **Trivy**: Container image scanning
- **Network Policies**: Micro-segmentation

### Observability
- **Prometheus**: Metrics collection with Thanos
- **Grafana**: 6 pre-configured dashboards
- **Loki**: Log aggregation
- **Alerting**: PagerDuty/Slack integration
- **Tracing**: Jaeger/Tempo ready

### CI/CD
- **GitHub Actions**: Automated workflows
- **Azure DevOps**: Alternative pipeline
- **ArgoCD**: GitOps-based deployments
- **Helm**: Package management

### Compliance
- SOC2 Type II ready
- ISO 27001 ready
- CIS Benchmarks
- Encryption at rest/transit
- Audit logging

## Cost Optimization

| Environment | Configuration | Monthly Cost |
|-------------|--------------|--------------|
| Development | k3s + spot | $50 |
| Staging | EKS 3-node | $150 |
| Production | EKS HA multi-AZ | $500-800 |

### Cost Saving Strategies
- Spot instances for non-critical workloads
- Single NAT gateway in development
- Automated shutdown for dev environments
- Reserved instances for production

## Troubleshooting

### Common Issues

**Terraform state lock**
```bash
terraform force-unlock <lock-id>
```

**kubectl connection issues**
```bash
aws eks update-kubeconfig --region us-east-1 --name <cluster-name>
```

**Pod stuck in Pending**
```bash
kubectl describe pod <pod-name>
kubectl get events --sort-by='.lastTimestamp'
```

## Support

- **Documentation**: See `infrastructure/docs/`
- **Runbooks**: See `infrastructure/docs/runbooks/`
- **Issues**: GitHub Issues

## Migration from miser-mode

The old `infrastructure/miser-mode/` directory is deprecated. All functionality has been migrated to the enterprise structure with significant improvements:

| Aspect | miser-mode | Enterprise |
|--------|-----------|------------|
| Orchestration | Docker Compose | Kubernetes |
| Scaling | Manual | Auto-scaling |
| Security | Basic | Vault + OPA + Falco |
| Observability | Basic Prometheus | Full stack |
| Environments | Single | Multi-environment |
| Cost | $11/mo | $50-800/mo |

To migrate:
1. Review `infrastructure/FINAL_SUMMARY.md`
2. Deploy new infrastructure
3. Migrate data using provided scripts
4. Remove old directory: `rm -rf infrastructure/miser-mode`

---

For complete details, see [infrastructure/FINAL_SUMMARY.md](infrastructure/FINAL_SUMMARY.md)