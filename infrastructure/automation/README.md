# Ghost CMS - Enterprise CI/CD Automation

Complete enterprise-grade CI/CD automation infrastructure for Ghost CMS with multi-cloud support, security scanning, and GitOps workflows.

## 📁 Directory Structure

```
automation/
├── pipelines/
│   ├── github-actions-deploy.yml    # GitHub Actions workflow
│   ├── azure-devops-pipeline.yml    # Azure DevOps pipeline
│   └── argocd-application.yaml      # ArgoCD Application manifests
├── scripts/
│   ├── deploy.sh                    # Main deployment script
│   └── rollback.sh                  # Rollback script
└── templates/
    └── helm-chart/                  # Complete Helm chart
        ├── Chart.yaml
        ├── values.yaml
        └── templates/
            ├── deployment.yaml
            ├── service.yaml
            ├── ingress.yaml
            ├── hpa.yaml
            ├── pdb.yaml
            ├── networkpolicy.yaml
            ├── servicemonitor.yaml
            └── prometheusrule.yaml
```

## 🚀 Features

### CI/CD Pipelines

#### GitHub Actions
- **Multi-stage pipeline** with security scanning, testing, and deployment
- **Security features:**
  - Trivy vulnerability scanning
  - TruffleHog secret detection
  - SBOM generation (SPDX format)
  - Container image signing with Cosign
- **Deployment strategies:**
  - Rolling updates
  - Blue-green deployments
  - Canary releases
- **Automated rollback** on failure
- **Slack notifications**

#### Azure DevOps
- **Comprehensive pipeline** with build, test, and deploy stages
- **Security scanning:**
  - Aqua Security integration
  - NPM audit
  - Secret scanning
- **Multi-environment support** (dev, staging, production)
- **Artifact management** with backup retention
- **Blue-green deployment** for production

#### ArgoCD
- **GitOps-based** continuous delivery
- **Multi-environment configurations:**
  - Development (single replica, minimal resources)
  - Staging (2 replicas, moderate resources)
  - Production (3+ replicas, high availability)
- **Automated sync** with health checks
- **Rollback capabilities**
- **RBAC integration** with project-based access control

### Deployment Scripts

#### deploy.sh
Enterprise deployment script with:
- **Pre-flight checks** (kubectl, helm, AWS CLI)
- **Multiple deployment strategies:**
  - Rolling update (default)
  - Blue-green deployment
  - Canary deployment
- **Automated backups** (application + database)
- **Health checks** (pod readiness, HTTP endpoints)
- **Dry-run mode** for validation
- **Slack notifications**
- **Automatic rollback** on failure

**Usage:**
```bash
# Deploy to development
./deploy.sh -e development -v v1.2.3 -s rolling

# Deploy to production with blue-green
./deploy.sh -e production -v v1.2.3 -s bluegreen

# Dry run
./deploy.sh -e staging -v v1.2.3 --dry-run
```

#### rollback.sh
Production-grade rollback script with:
- **Multiple rollback methods:**
  - Helm revision rollback
  - Backup-based restore
- **Database restore** capabilities
- **Pre-rollback snapshot** creation
- **Verification** after rollback
- **Manual approval** for production

**Usage:**
```bash
# Rollback to previous release
./rollback.sh -e production

# Rollback to specific revision
./rollback.sh -e production -r 5

# Rollback to specific backup
./rollback.sh -e production -t 20260203-120000
```

### Helm Chart

Production-ready Helm chart featuring:

#### Application
- **Deployment** with configurable replicas
- **Rolling updates** with zero downtime
- **Pod anti-affinity** for high availability
- **Resource limits** and requests
- **Liveness, readiness, and startup probes**
- **Security context** (non-root, read-only filesystem)

#### Storage
- **Persistent volumes** for content
- **MySQL** (Bitnami chart dependency)
- **Redis** (Bitnami chart dependency)

#### Networking
- **ClusterIP service**
- **Ingress** with TLS support
- **Network policies** (ingress/egress rules)

#### Autoscaling
- **Horizontal Pod Autoscaler** (CPU/memory based)
- **Pod Disruption Budget** for availability

#### Monitoring
- **ServiceMonitor** for Prometheus
- **PrometheusRules** for alerting:
  - Ghost instance down
  - High memory usage
  - High CPU usage

#### Security
- **Pod Security Context**
- **Security Context** (capabilities dropped)
- **Network Policies**
- **Secret management** (External Secrets ready)

## 🔧 Configuration

### Environment Setup

1. **GitHub Actions Setup:**
```yaml
# Required secrets:
- AWS_ROLE_ARN_DEV
- AWS_ROLE_ARN_STAGING
- AWS_ROLE_ARN_PROD
- SLACK_WEBHOOK
- GITHUB_TOKEN (automatic)
```

2. **Azure DevOps Setup:**
```yaml
# Required service connections:
- ghost-acr-connection (Azure Container Registry)
- ghost-k8s-connection-dev
- ghost-k8s-connection-staging
- ghost-k8s-connection-prod

# Required variable groups:
- ghost-secrets-development
- ghost-secrets-staging
- ghost-secrets-production
- ghost-config-development
- ghost-config-staging
- ghost-config-production
```

3. **ArgoCD Setup:**
```bash
# Apply ArgoCD applications
kubectl apply -f pipelines/argocd-application.yaml

# Verify applications
argocd app list
argocd app sync ghost-cms-production
```

### Helm Values

Customize for each environment:

**Development (`environments/development/values.yaml`):**
```yaml
replicaCount: 1
autoscaling:
  enabled: false
resources:
  limits:
    cpu: 500m
    memory: 1Gi
```

**Staging (`environments/staging/values.yaml`):**
```yaml
replicaCount: 2
autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 5
```

**Production (`environments/production/values.yaml`):**
```yaml
replicaCount: 3
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
podDisruptionBudget:
  enabled: true
  minAvailable: 2
```

## 📊 Monitoring & Alerting

### Prometheus Metrics
- Application health (`up` metric)
- Container resource usage
- Request rate and latency
- Error rates

### Alerts
- **Critical:**
  - Ghost instance down (>5 minutes)
- **Warning:**
  - High memory usage (>90%)
  - High CPU usage (>80%)

### Dashboards
Import included Grafana dashboards:
- Ghost Overview
- Performance Metrics
- Resource Usage

## 🔐 Security

### Image Scanning
- **Trivy** scans for vulnerabilities
- **Aqua Security** (Azure DevOps)
- **Continuous monitoring** in registries

### Secret Management
- **External Secrets** operator integration
- **Sealed Secrets** support
- **AWS Secrets Manager** integration
- **Azure Key Vault** integration

### SBOM Generation
- SPDX-format SBOM
- Stored as pipeline artifacts
- 90-day retention

### Container Signing
- **Cosign** integration
- Signature verification
- Provenance attestation

## 🔄 Deployment Strategies

### Rolling Update
Default strategy with gradual pod replacement:
- `maxSurge: 1`
- `maxUnavailable: 0`
- Zero downtime
- Automatic rollback on failure

### Blue-Green Deployment
Two complete environments with traffic switching:
1. Deploy green version
2. Run health checks
3. Switch traffic
4. Monitor for issues
5. Cleanup blue version

### Canary Deployment
Gradual traffic shift to new version:
1. Deploy canary (single replica)
2. Monitor for 5 minutes
3. If healthy, full rollout
4. Otherwise, automatic rollback

## 🧪 Testing

### Pre-deployment Tests
- Unit tests
- Integration tests
- Security scans
- Linting

### Post-deployment Tests
- Smoke tests (HTTP health checks)
- Database connectivity
- Redis connectivity
- End-to-end tests

## 📦 Backup & Restore

### Automated Backups
- **Application state** (Helm values, manifests)
- **Database backups** to S3/Azure Blob
- **Retention:** 30 days (staging), 90 days (production)

### Restore Process
```bash
# List available backups
ls -la backups/production/

# Restore from backup
./rollback.sh -e production -t 20260203-120000
```

## 🚨 Incident Response

### Rollback Procedures

**Automatic Rollback:**
- Triggered on deployment failure
- Health check failures
- Pod crash loops

**Manual Rollback:**
```bash
# Quick rollback to previous version
./rollback.sh -e production

# Rollback to specific version
./rollback.sh -e production -r 5
```

### Health Check Endpoints
- `/ghost/api/v3/admin/site/` - Admin API health
- `/` - Frontend health

## 📝 Maintenance

### Regular Tasks
- Review and update base images
- Security patch management
- Dependency updates
- Backup verification
- Capacity planning

### Upgrade Process
1. Test in development
2. Deploy to staging
3. Run full test suite
4. Production deployment (off-peak hours)
5. Monitor metrics for 24 hours

## 🤝 Contributing

1. Create feature branch
2. Update appropriate pipeline
3. Test in development environment
4. Submit PR with test results
5. Await approvals

## 📖 Additional Resources

- [Ghost Documentation](https://ghost.org/docs/)
- [Kubernetes Best Practices](https://kubernetes.io/docs/concepts/configuration/overview/)
- [Helm Chart Development](https://helm.sh/docs/chart_template_guide/)
- [ArgoCD Documentation](https://argo-cd.readthedocs.io/)

## 🆘 Support

For issues or questions:
1. Check logs: `kubectl logs -n ghost-production -l app=ghost`
2. Review metrics in Grafana
3. Check ArgoCD application status
4. Contact platform team

## 📄 License

Enterprise internal use only.
