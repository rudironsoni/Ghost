# Deployment Runbook - Ghost Platform

**Version:** 1.0  
**Last Updated:** 2026-02-03  
**Owner:** Platform Engineering Team  
**Severity:** P1 (Production Critical)

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Pre-Deployment Checklist](#pre-deployment-checklist)
4. [Deployment Procedures](#deployment-procedures)
5. [Rollback Procedures](#rollback-procedures)
6. [Post-Deployment Verification](#post-deployment-verification)
7. [Troubleshooting](#troubleshooting)
8. [Emergency Contacts](#emergency-contacts)

---

## Overview

This runbook covers all deployment scenarios for the Ghost Platform across development, staging, and production environments. It includes both infrastructure (Terraform) and application (Docker/Kubernetes) deployments.

### Deployment Types

- **Infrastructure Deployment**: Terraform-managed cloud resources
- **Application Deployment**: Ghost application and services
- **Configuration Changes**: Environment variables, secrets, policies
- **Database Migrations**: Schema changes and data migrations
- **Hotfix Deployment**: Emergency patches

---

## Prerequisites

### Required Tools

```bash
# Verify all tools are installed
terraform version    # >= 1.5.0
kubectl version      # >= 1.28.0
helm version         # >= 3.12.0
docker version       # >= 24.0.0
ansible --version    # >= 2.15.0
```

### Required Access

- [ ] Cloud provider credentials (AWS/GCP/Azure)
- [ ] Kubernetes cluster access (kubeconfig)
- [ ] Container registry access (Docker Hub/ECR/GCR)
- [ ] VPN/Bastion access for production
- [ ] HashiCorp Vault access for secrets
- [ ] Git repository access
- [ ] Monitoring/alerting access (Grafana, PagerDuty)

### Required Approvals

| Environment | Approval Required | Notice Period |
|------------|------------------|---------------|
| Development | Self-approval | None |
| Staging | Tech Lead | 1 hour |
| Production | Change Advisory Board | 24 hours |

---

## Pre-Deployment Checklist

### Planning Phase (T-24 hours)

- [ ] Review deployment ticket and change request
- [ ] Identify maintenance window
- [ ] Notify stakeholders via Slack/Email
- [ ] Schedule deployment meeting (for production)
- [ ] Prepare rollback plan
- [ ] Review recent incidents and alerts
- [ ] Check system health metrics
- [ ] Verify backup completion status

### Preparation Phase (T-4 hours)

- [ ] Pull latest code from main branch
- [ ] Review all changes since last deployment
- [ ] Run full test suite locally
- [ ] Build and tag container images
- [ ] Push images to container registry
- [ ] Update version in deployment manifests
- [ ] Prepare deployment commands
- [ ] Test in development environment
- [ ] Create deployment runsheet

### Pre-Deployment Phase (T-30 minutes)

- [ ] Join deployment bridge/call (production only)
- [ ] Verify all team members are ready
- [ ] Take final production backup
- [ ] Enable maintenance mode (if applicable)
- [ ] Silence non-critical alerts
- [ ] Verify monitoring dashboards are accessible
- [ ] Confirm rollback procedure is understood

---

## Deployment Procedures

### 1. Infrastructure Deployment (Terraform)

#### Development Environment

```bash
# Navigate to environment directory
cd infrastructure/environments/development

# Initialize Terraform (first time or after module changes)
terraform init

# Validate configuration
terraform validate

# Review changes
terraform plan -out=tfplan

# Review the plan carefully!
# Check for any resource replacements or deletions

# Apply changes
terraform apply tfplan

# Save deployment info
echo "Deployed at: $(date)" >> deployment.log
```

#### Production Environment

```bash
# Navigate to production directory
cd infrastructure/environments/production

# Get latest Terraform state
terraform refresh

# Create detailed plan
terraform plan -out=production.tfplan | tee plan-output.txt

# MANDATORY: Have another engineer review the plan
# Share plan-output.txt for review

# After approval, apply with extra caution
terraform apply production.tfplan

# Verify all resources
terraform output

# Document changes
git tag -a "infra-$(date +%Y%m%d-%H%M)" -m "Production infrastructure deployment"
git push origin --tags
```

#### Rollback Infrastructure Changes

```bash
# Rollback to previous Terraform state
terraform state pull > current-state.json
terraform state push previous-state.json

# Or revert specific resource
terraform import aws_instance.example i-abc12345
terraform apply
```

---

### 2. Application Deployment (Docker Compose - Miser Mode)

#### Initial Deployment

```bash
# Navigate to miser-mode directory
cd infrastructure/miser-mode/ansible

# Verify inventory
ansible-inventory -i inventory/hosts.yml --list

# Run setup playbook (first time only)
ansible-playbook -i inventory/hosts.yml setup.yml

# Deploy application
ansible-playbook -i inventory/hosts.yml deploy.yml

# Verify deployment
ansible-playbook -i inventory/hosts.yml health-check.yml
```

#### Update Deployment

```bash
# Pull latest images
ansible-playbook -i inventory/hosts.yml deploy.yml --tags update

# Or specify version
ansible-playbook -i inventory/hosts.yml deploy.yml \
  --extra-vars "ghost_version=5.87.1"

# Restart services if needed
ansible-playbook -i inventory/hosts.yml deploy.yml --tags restart
```

#### Zero-Downtime Deployment

```bash
# For single-server setup with minimal downtime
# 1. Deploy to staging slot
docker compose -f docker-compose.yml -f docker-compose.staging.yml up -d

# 2. Health check staging
curl -f http://localhost:8081/health || exit 1

# 3. Swap staging to production
docker compose -f docker-compose.yml -f docker-compose.blue-green.yml up -d

# 4. Remove old containers
docker compose rm -f ghost-old
```

---

### 3. Application Deployment (Kubernetes)

#### Deploy to Development

```bash
# Configure kubectl context
export KUBECONFIG=~/.kube/config-dev
kubectl config use-context ghost-dev

# Apply manifests
kubectl apply -k infrastructure/platform/base
kubectl apply -k infrastructure/platform/services

# Verify deployment
kubectl get pods -n ghost
kubectl rollout status deployment/ghost-api -n ghost
```

#### Deploy to Production

```bash
# Set production context
export KUBECONFIG=~/.kube/config-prod
kubectl config use-context ghost-prod

# MANDATORY: Backup current deployment
kubectl get deployment ghost-api -n ghost -o yaml > backup-deployment.yaml

# Update image tag
kubectl set image deployment/ghost-api \
  ghost-api=ghost/api:v1.2.3 \
  -n ghost

# Or apply updated manifests
kubectl apply -k infrastructure/platform/services

# Watch rollout
kubectl rollout status deployment/ghost-api -n ghost --timeout=10m

# Verify pods are healthy
kubectl get pods -n ghost -l app=ghost-api
kubectl describe pod <pod-name> -n ghost
```

#### Canary Deployment

```bash
# Create canary deployment (10% traffic)
kubectl apply -f manifests/canary-deployment.yaml

# Monitor metrics for 10 minutes
watch -n 10 'kubectl top pods -n ghost | grep canary'

# Check error rates in Grafana
# URL: https://grafana.example.com/d/ghost-api/ghost-api-dashboard

# If healthy, increase traffic to 50%
kubectl patch deployment ghost-api-canary -n ghost \
  -p '{"spec":{"replicas":3}}'

# Monitor for another 10 minutes

# If all good, promote canary to production
kubectl apply -f manifests/production-deployment.yaml
kubectl delete -f manifests/canary-deployment.yaml
```

---

### 4. Database Migration Deployment

#### Pre-Migration Steps

```bash
# Take full database backup
ansible-playbook -i inventory/hosts.yml backup.yml --tags postgres

# Download backup for safety
scp ghost-server:/var/lib/ghost/backups/postgres-*.gz /tmp/

# Verify backup integrity
gunzip -t /tmp/postgres-*.gz

# Test migration in development
export DATABASE_URL="postgresql://user:pass@dev-db:5432/ghost"
npm run migrate:up
```

#### Production Migration

```bash
# Enable maintenance mode
kubectl scale deployment ghost-api -n ghost --replicas=0

# Or update maintenance page
kubectl apply -f manifests/maintenance-mode.yaml

# Connect to database
kubectl exec -it postgres-0 -n ghost -- psql -U ghost

# Run migration
\i /migrations/001-add-user-roles.sql

# Verify migration
\dt
SELECT * FROM schema_migrations ORDER BY version DESC LIMIT 5;

# Test with read-only queries
SELECT COUNT(*) FROM users;

# Exit maintenance mode
kubectl scale deployment ghost-api -n ghost --replicas=3
kubectl delete -f manifests/maintenance-mode.yaml
```

#### Rollback Migration

```bash
# If migration fails, rollback
\i /migrations/001-add-user-roles-rollback.sql

# Or restore from backup
kubectl exec -it postgres-0 -n ghost -- bash
pg_restore -U ghost -d ghost /backups/postgres-backup.gz

# Verify data integrity
SELECT COUNT(*) FROM users;
SELECT * FROM users ORDER BY created_at DESC LIMIT 5;
```

---

### 5. Configuration Changes

#### Update Environment Variables

```bash
# Kubernetes ConfigMap
kubectl create configmap ghost-config \
  --from-file=config.json \
  --dry-run=client -o yaml | kubectl apply -f -

# Restart pods to pick up changes
kubectl rollout restart deployment/ghost-api -n ghost

# Docker Compose
# Edit docker-compose.yml or .env file
vim infrastructure/miser-mode/docker/.env

# Restart service
docker compose restart ghost-api
```

#### Update Secrets

```bash
# Retrieve from Vault
export VAULT_ADDR="https://vault.example.com"
vault login -method=oidc

# Get secret
vault kv get secret/ghost/production/database

# Update Kubernetes secret
kubectl create secret generic ghost-secrets \
  --from-literal=db-password='new-password' \
  --dry-run=client -o yaml | kubectl apply -f -

# Restart pods
kubectl rollout restart deployment/ghost-api -n ghost

# Verify connection
kubectl logs deployment/ghost-api -n ghost | grep "Database connected"
```

---

### 6. Hotfix Deployment

#### Emergency Hotfix Procedure

```bash
# 1. Create hotfix branch
git checkout main
git pull origin main
git checkout -b hotfix/critical-security-fix

# 2. Make minimal changes
# Edit only what's necessary
vim src/api/middleware/auth.js

# 3. Commit and push
git add .
git commit -m "hotfix: patch critical security vulnerability CVE-2024-12345"
git push origin hotfix/critical-security-fix

# 4. Build and tag image
docker build -t ghost/api:hotfix-$(date +%Y%m%d-%H%M) .
docker push ghost/api:hotfix-$(date +%Y%m%d-%H%M)

# 5. Deploy immediately
kubectl set image deployment/ghost-api \
  ghost-api=ghost/api:hotfix-$(date +%Y%m%d-%H%M) \
  -n ghost

# 6. Monitor closely
kubectl logs -f deployment/ghost-api -n ghost

# 7. Create post-deploy PR for review
# Merge hotfix to main after deployment succeeds
```

---

## Rollback Procedures

### Quick Rollback Decision Tree

```
Is the issue critical? (Service down, data corruption, security breach)
├── YES → Immediate rollback
│   └── Follow "Emergency Rollback" procedure
└── NO → Can we fix forward?
    ├── YES → Deploy hotfix
    └── NO → Follow "Standard Rollback" procedure
```

### Emergency Rollback (< 5 minutes)

```bash
# Kubernetes - Rollback to previous version
kubectl rollout undo deployment/ghost-api -n ghost

# Check status
kubectl rollout status deployment/ghost-api -n ghost

# Verify rollback
kubectl get pods -n ghost -l app=ghost-api
kubectl logs deployment/ghost-api -n ghost | tail -50
```

### Standard Rollback (< 15 minutes)

```bash
# Kubernetes - Rollback to specific revision
# List deployment history
kubectl rollout history deployment/ghost-api -n ghost

# Rollback to specific revision
kubectl rollout undo deployment/ghost-api -n ghost --to-revision=3

# Docker Compose - Rollback to previous image
cd infrastructure/miser-mode/docker
git log --oneline docker-compose.yml
git checkout <commit-hash> docker-compose.yml
docker compose pull
docker compose up -d

# Verify services
docker compose ps
docker compose logs ghost-api | tail -50
```

### Database Rollback

```bash
# Restore from backup
kubectl exec -it postgres-0 -n ghost -- bash

# Stop application
kubectl scale deployment ghost-api -n ghost --replicas=0

# Restore database
dropdb ghost
createdb ghost
pg_restore -U ghost -d ghost /backups/postgres-backup-pre-deploy.gz

# Restart application
kubectl scale deployment ghost-api -n ghost --replicas=3

# Verify data
psql -U ghost -c "SELECT COUNT(*) FROM users;"
```

---

## Post-Deployment Verification

### Automated Health Checks

```bash
# Run health check playbook
cd infrastructure/miser-mode/ansible
ansible-playbook -i inventory/hosts.yml health-check.yml

# Check all endpoints
curl -f https://api.ghost.example.com/health
curl -f https://api.ghost.example.com/metrics

# Kubernetes health checks
kubectl get pods -n ghost
kubectl top pods -n ghost
kubectl get endpoints -n ghost
```

### Manual Verification Steps

#### 1. Service Health (5 minutes)

- [ ] All pods/containers are running
- [ ] No crash loops or restart counts
- [ ] Health endpoints return 200 OK
- [ ] Metrics endpoints are accessible
- [ ] No error logs in last 5 minutes

```bash
# Check pod health
kubectl get pods -n ghost -o wide

# Check logs for errors
kubectl logs -l app=ghost-api -n ghost --since=5m | grep -i error

# Check metrics
curl http://localhost:9090/api/v1/query?query=up{job="ghost-api"}
```

#### 2. Database Connectivity (3 minutes)

- [ ] Database connections are healthy
- [ ] Connection pool is not exhausted
- [ ] No slow queries
- [ ] Replication lag is acceptable (< 1s)

```bash
# Check database connections
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT count(*) FROM pg_stat_activity WHERE state = 'active';"

# Check replication lag (if applicable)
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT EXTRACT(EPOCH FROM (now() - pg_last_xact_replay_timestamp()));"
```

#### 3. Cache & Queue Health (3 minutes)

- [ ] Redis is responding
- [ ] Cache hit rate is normal (> 80%)
- [ ] RabbitMQ queues are processing
- [ ] No queue buildup

```bash
# Check Redis
redis-cli ping
redis-cli info stats | grep keyspace_hits

# Check RabbitMQ
curl -u admin:password http://localhost:15672/api/queues | jq '.[] | {name, messages}'
```

#### 4. API Functionality (5 minutes)

- [ ] Login works
- [ ] Job search returns results
- [ ] Job details page loads
- [ ] User profile loads
- [ ] API response times are normal (< 200ms p95)

```bash
# Test key endpoints
curl -X POST https://api.ghost.example.com/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"test123"}'

# Get auth token and test API
TOKEN="<jwt-token>"
curl -H "Authorization: Bearer $TOKEN" \
  https://api.ghost.example.com/jobs?limit=10
```

#### 5. Monitoring & Alerts (3 minutes)

- [ ] Grafana dashboards showing data
- [ ] No new alerts firing
- [ ] Prometheus scraping all targets
- [ ] Log aggregation working

```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {job, health}'

# Check active alerts
curl http://localhost:9090/api/v1/alerts | jq '.data.alerts[] | {alertname, state}'
```

#### 6. Performance Metrics (5 minutes)

- [ ] CPU usage < 70%
- [ ] Memory usage < 80%
- [ ] Disk usage < 85%
- [ ] API latency p95 < 200ms
- [ ] Error rate < 0.5%

```bash
# Check resource usage
kubectl top pods -n ghost

# Check metrics in Prometheus
curl 'http://localhost:9090/api/v1/query?query=rate(http_requests_total[5m])'
curl 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))'
```

### Smoke Tests

```bash
# Run automated smoke tests
cd tests/smoke
npm test

# Or use curl-based tests
bash smoke-test.sh production
```

### Post-Deployment Actions

- [ ] Re-enable silenced alerts
- [ ] Disable maintenance mode
- [ ] Update deployment log
- [ ] Notify stakeholders of completion
- [ ] Schedule post-deployment review
- [ ] Update documentation if needed
- [ ] Close deployment ticket

---

## Troubleshooting

### Common Issues

#### Issue: Pod Stuck in Pending State

```bash
# Check pod events
kubectl describe pod <pod-name> -n ghost

# Common causes:
# 1. Insufficient resources
kubectl describe nodes | grep -A 5 "Allocated resources"

# 2. Image pull errors
kubectl get events -n ghost | grep "Failed to pull image"

# 3. PVC not bound
kubectl get pvc -n ghost
```

**Resolution:**
- Scale down other deployments
- Use smaller resource requests
- Fix image name/tag
- Check storage class configuration

#### Issue: Database Connection Failures

```bash
# Check database pod
kubectl get pod postgres-0 -n ghost
kubectl logs postgres-0 -n ghost

# Check service
kubectl get svc postgres -n ghost
kubectl describe svc postgres -n ghost

# Test connection from app pod
kubectl exec -it <ghost-pod> -n ghost -- bash
nc -zv postgres 5432
psql -h postgres -U ghost -d ghost -c "SELECT 1"
```

**Resolution:**
- Verify database credentials in secret
- Check network policies
- Verify service endpoints
- Check connection pool settings

#### Issue: High Memory Usage / OOMKilled

```bash
# Check pod resource limits
kubectl get pod <pod-name> -n ghost -o yaml | grep -A 5 resources

# Check memory usage
kubectl top pod <pod-name> -n ghost

# Check for memory leaks
kubectl exec -it <pod-name> -n ghost -- node --expose-gc --max-old-space-size=512
```

**Resolution:**
- Increase memory limits
- Enable garbage collection
- Check for memory leaks in code
- Restart pods periodically

#### Issue: Slow API Response Times

```bash
# Check database slow queries
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT pid, now() - query_start as duration, query 
   FROM pg_stat_activity 
   WHERE state = 'active' 
   ORDER BY duration DESC;"

# Check Redis latency
redis-cli --latency

# Check application logs
kubectl logs -l app=ghost-api -n ghost | grep "slow query"
```

**Resolution:**
- Add database indexes
- Optimize queries
- Increase Redis memory
- Scale application pods

#### Issue: Image Pull Errors

```bash
# Check image pull secret
kubectl get secret regcred -n ghost -o yaml

# Test registry access
docker login registry.example.com
docker pull ghost/api:v1.2.3

# Update image pull secret
kubectl create secret docker-registry regcred \
  --docker-server=registry.example.com \
  --docker-username=user \
  --docker-password=pass \
  --dry-run=client -o yaml | kubectl apply -f -
```

**Resolution:**
- Verify registry credentials
- Check network connectivity
- Verify image exists in registry
- Update image pull secret

---

## Emergency Contacts

### On-Call Rotation

| Role | Primary | Secondary | Phone | Slack |
|------|---------|-----------|-------|-------|
| Platform Engineer | John Doe | Jane Smith | +1-555-0100 | @john |
| DevOps Lead | Alice Johnson | Bob Wilson | +1-555-0101 | @alice |
| Database Admin | Charlie Brown | Diana Prince | +1-555-0102 | @charlie |
| Security Engineer | Eve Martinez | Frank Castle | +1-555-0103 | @eve |

### Escalation Path

1. **L1 - On-Call Engineer** (Response: 15 minutes)
2. **L2 - Platform Lead** (Response: 30 minutes)
3. **L3 - Engineering Manager** (Response: 1 hour)
4. **L4 - CTO** (Critical incidents only)

### Communication Channels

- **Incident Channel:** `#incidents` (Slack)
- **Platform Team:** `#platform-engineering` (Slack)
- **Status Page:** https://status.ghost.example.com
- **PagerDuty:** https://ghost.pagerduty.com

### External Contacts

- **Cloud Provider Support:** Available via console
- **Monitoring Vendor:** support@grafana.com
- **Security Vendor:** support@snyk.io

---

## Appendix

### Deployment Checklist (Printable)

```
DEPLOYMENT CHECKLIST - Ghost Platform
Date: _______________  Environment: _______________
Engineer: _______________  Approver: _______________

PRE-DEPLOYMENT
[ ] Change ticket created and approved
[ ] Deployment window scheduled
[ ] Stakeholders notified
[ ] Backup completed and verified
[ ] Test environment validation passed
[ ] Rollback plan prepared
[ ] Monitoring dashboards accessible

DEPLOYMENT
[ ] Maintenance mode enabled (if applicable)
[ ] Non-critical alerts silenced
[ ] Deployment executed
[ ] Health checks passed
[ ] Smoke tests passed
[ ] Monitoring verified

POST-DEPLOYMENT
[ ] Maintenance mode disabled
[ ] Alerts re-enabled
[ ] Stakeholders notified
[ ] Documentation updated
[ ] Deployment log updated
[ ] Post-mortem scheduled (if issues)

SIGN-OFF
Deployed by: _______________  Time: _______________
Verified by: _______________  Time: _______________
```

### Useful Commands Reference

```bash
# Kubernetes quick commands
alias k='kubectl'
alias kgp='kubectl get pods -n ghost'
alias kgd='kubectl get deployments -n ghost'
alias klf='kubectl logs -f -n ghost'
alias kdesc='kubectl describe -n ghost'

# Docker Compose quick commands
alias dc='docker compose'
alias dcp='docker compose ps'
alias dcl='docker compose logs'
alias dce='docker compose exec'

# Monitoring quick commands
alias prom='curl -s http://localhost:9090/api/v1'
alias graf='open http://localhost:3000'
```

### Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-03 | Platform Team | Initial version |

---

**Document Classification:** Internal Use Only  
**Review Schedule:** Quarterly  
**Next Review Date:** 2026-05-03
