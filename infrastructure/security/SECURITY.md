# Security Infrastructure Documentation

## Overview

This document describes the enterprise-grade security infrastructure deployed for the Ghost platform. The security stack includes secrets management, policy enforcement, vulnerability scanning, and runtime security monitoring.

## Table of Contents

- [Architecture](#architecture)
- [Components](#components)
  - [HashiCorp Vault](#hashicorp-vault)
  - [OPA Gatekeeper](#opa-gatekeeper)
  - [Trivy Operator](#trivy-operator)
  - [Falco](#falco)
- [Deployment](#deployment)
- [Configuration](#configuration)
- [Monitoring](#monitoring)
- [Incident Response](#incident-response)
- [Compliance](#compliance)
- [Best Practices](#best-practices)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Ghost Security Stack                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Vault HA   │  │ OPA          │  │   Trivy      │      │
│  │   Cluster    │  │ Gatekeeper   │  │   Operator   │      │
│  │              │  │              │  │              │      │
│  │ • KV v2      │  │ • Admission  │  │ • Vuln Scan  │      │
│  │ • Dynamic    │  │   Control    │  │ • Config     │      │
│  │   Secrets    │  │ • Policy     │  │   Audit      │      │
│  │ • Transit    │  │   Engine     │  │ • Compliance │      │
│  │ • PKI        │  │              │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │    Falco     │  │   Network    │  │   TLS/PKI    │      │
│  │              │  │   Policies   │  │              │      │
│  │ • Runtime    │  │              │  │ • Cert       │      │
│  │   Security   │  │ • Ingress/   │  │   Manager    │      │
│  │ • Threat     │  │   Egress     │  │ • mTLS       │      │
│  │   Detection  │  │ • Service    │  │ • Rotation   │      │
│  │ • Audit      │  │   Mesh       │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Components

### HashiCorp Vault

**Purpose**: Centralized secrets management and encryption as a service.

**Location**: `/infrastructure/security/vault/`

**Features**:
- High Availability (HA) cluster with Raft storage
- Auto-unseal using AWS KMS
- Kubernetes authentication
- AppRole authentication for CI/CD
- Dynamic database credentials
- Transit encryption for PII
- PKI for internal certificates

**Deployment**:
```bash
# Deploy Vault using Helm
helm repo add hashicorp https://helm.releases.hashicorp.com
helm install vault hashicorp/vault \
  --namespace vault \
  --create-namespace \
  -f vault/vault-values.yaml

# Initialize and configure Vault
cd vault
./vault-setup.sh
```

**Key Files**:
- `vault-values.yaml`: Helm chart configuration
- `vault-policy-ghost.hcl`: Application-specific policies
- `vault-setup.sh`: Initialization script

**Authentication**:

1. **Kubernetes Auth** (for pods):
```bash
vault write auth/kubernetes/role/ghost \
  bound_service_account_names=ghost \
  bound_service_account_namespaces=ghost-production \
  policies=ghost-app \
  ttl=24h
```

2. **AppRole Auth** (for CI/CD):
```bash
vault write auth/approle/role/ghost \
  token_policies="ghost-app" \
  token_ttl=1h
```

**Secret Engines**:

- **KV v2**: Application secrets at `secret/ghost/*`
- **Database**: Dynamic credentials for MySQL
- **Transit**: Encryption/decryption for PII
- **PKI**: Internal certificate issuance

**Access Patterns**:
```bash
# Read secret from KV v2
vault kv get secret/ghost/database

# Get dynamic database credentials
vault read database/creds/ghost-app

# Encrypt data with Transit
vault write transit/encrypt/ghost-pii plaintext=$(base64 <<< "sensitive-data")

# Issue certificate from PKI
vault write pki/issue/ghost-internal common_name="api.ghost.internal"
```

**Monitoring**:
- Prometheus metrics at `:8200/v1/sys/metrics`
- Audit logs at `/vault/audit/vault-audit.log`
- Telemetry exported to Prometheus

**Security Considerations**:
- Root token should be revoked after initial setup
- Unseal keys stored in separate secure locations
- Regular key rotation (90 days)
- Audit logging enabled for all operations
- TLS enabled for all communication

### OPA Gatekeeper

**Purpose**: Policy-as-code enforcement using admission control.

**Location**: `/infrastructure/security/policies/`

**Features**:
- Admission webhook for policy enforcement
- Custom constraint templates
- Audit mode for testing
- Dry-run capability
- Policy violation reporting

**Deployment**:
```bash
# Install Gatekeeper
helm repo add gatekeeper https://open-policy-agent.github.io/gatekeeper/charts
helm install gatekeeper gatekeeper/gatekeeper \
  --namespace gatekeeper-system \
  --create-namespace

# Deploy constraint templates
kubectl apply -f policies/constraint-templates.yaml

# Deploy constraints
kubectl apply -f policies/opa-gatekeeper-constraints.yaml
kubectl apply -f policies/privileged-containers.yaml
kubectl apply -f policies/allowed-repos.yaml
kubectl apply -f policies/required-labels.yaml
kubectl apply -f policies/resource-limits.yaml
```

**Policies Enforced**:

1. **Security Policies**:
   - No privileged containers
   - No host namespaces (PID, IPC, Network)
   - Read-only root filesystem
   - No privilege escalation
   - Run as non-root user
   - Drop all capabilities
   - No host path volumes
   - Seccomp profile required

2. **Operational Policies**:
   - Required labels (environment, team, app)
   - Resource requests and limits
   - Allowed container registries
   - No :latest tags
   - Resource limit/request ratio

3. **Compliance Policies**:
   - PCI-DSS controls
   - SOC 2 requirements
   - CIS Kubernetes Benchmark

**Constraint Examples**:

```yaml
# Block privileged containers
apiVersion: constraints.gatekeeper.sh/v1beta1
kind: K8sPSPPrivilegedContainer
metadata:
  name: block-privileged-containers
spec:
  enforcementAction: deny
  match:
    kinds:
      - apiGroups: [""]
        kinds: ["Pod"]
```

**Testing Policies**:
```bash
# Test with dry-run
kubectl apply -f test-pod.yaml --dry-run=server

# View violations
kubectl get constraints

# Describe specific constraint
kubectl describe k8spsprivilegedcontainer block-privileged-containers
```

**Monitoring**:
- Gatekeeper metrics exposed on port 8888
- Policy violations logged and tracked
- Prometheus ServiceMonitor available

### Trivy Operator

**Purpose**: Kubernetes-native security scanning for vulnerabilities and misconfigurations.

**Location**: `/infrastructure/security/scanning/trivy-operator-values.yaml`

**Features**:
- Continuous vulnerability scanning
- Configuration audit (CIS benchmarks)
- Exposed secret detection
- RBAC assessment
- Infrastructure assessment
- Compliance reporting (NSA, CIS, PCI-DSS, SOC2)

**Deployment**:
```bash
# Install Trivy Operator
helm repo add aqua https://aquasecurity.github.io/helm-charts/
helm install trivy-operator aqua/trivy-operator \
  --namespace trivy-system \
  --create-namespace \
  -f scanning/trivy-operator-values.yaml
```

**Scan Types**:

1. **Vulnerability Reports**:
   - Image scanning for CVEs
   - Severity-based reporting (CRITICAL, HIGH, MEDIUM, LOW)
   - Schedule: Every 6 hours for production

2. **Configuration Audits**:
   - Kubernetes misconfigurations
   - Pod Security Standards violations
   - Schedule: Every 6 hours

3. **Exposed Secrets**:
   - API keys, tokens, passwords in images
   - Immediate alerting for findings

4. **RBAC Assessment**:
   - Overly permissive roles
   - Unused service accounts
   - Schedule: Daily

5. **Infrastructure Assessment**:
   - Node security posture
   - Host vulnerabilities
   - Schedule: Weekly

6. **Compliance Reports**:
   - NSA Kubernetes Hardening Guide
   - CIS Kubernetes Benchmark
   - PCI-DSS requirements
   - SOC 2 controls

**Viewing Reports**:
```bash
# List vulnerability reports
kubectl get vulnerabilityreports -n ghost-production

# View specific report
kubectl get vulnerabilityreport <name> -o yaml

# List configuration audit reports
kubectl get configauditreports -n ghost-production

# View compliance reports
kubectl get clustercompliancereports
```

**Severity Handling**:
- **CRITICAL**: Block deployment, immediate remediation required
- **HIGH**: Allow with approval, remediation within 7 days
- **MEDIUM**: Allow deployment, remediation within 30 days
- **LOW**: Allow deployment, remediation within 90 days

**Integration**:
- Slack notifications for CRITICAL/HIGH findings
- Custom webhook to security dashboard
- CloudWatch Logs for audit trail
- S3 storage for report archives

### Falco

**Purpose**: Runtime security and threat detection using eBPF.

**Location**: `/infrastructure/security/scanning/falco-values.yaml`

**Features**:
- Runtime threat detection
- System call monitoring
- Kubernetes audit events
- Custom rules for Ghost application
- Real-time alerting
- Forensic data collection

**Deployment**:
```bash
# Install Falco
helm repo add falcosecurity https://falcosecurity.github.io/charts
helm install falco falcosecurity/falco \
  --namespace falco \
  --create-namespace \
  -f scanning/falco-values.yaml
```

**Detection Rules**:

1. **File System Activity**:
   - Write below /bin, /sbin, /usr/bin
   - Modify sensitive files (/etc/passwd, /etc/shadow)
   - Change binary directories

2. **Process Activity**:
   - Shell spawned in container
   - Privileged container launched
   - User management commands
   - Package manager executed

3. **Network Activity**:
   - Outbound connection to unexpected port
   - Connection to C&C server
   - Port scanning detected

4. **Kubernetes Activity**:
   - ConfigMap/Secret accessed
   - Anonymous requests
   - Pod created in kube-system
   - Service account token accessed

5. **Application-Specific**:
   - Ghost content upload anomalies
   - Database dump attempts
   - Unauthorized API access

**Alert Channels**:
- Slack (#ghost-security-alerts)
- PagerDuty (critical alerts)
- Elasticsearch (indexing)
- AWS Security Hub
- Custom webhook

**Viewing Alerts**:
```bash
# View Falco logs
kubectl logs -n falco -l app.kubernetes.io/name=falco

# Access Falco Sidekick UI
kubectl port-forward -n falco svc/falco-sidekick-ui 2802:2802

# Query Elasticsearch
curl -X GET "https://elasticsearch.ghost.internal:9200/falco-alerts/_search?pretty"
```

**Custom Rules**:
```yaml
# Example custom rule for Ghost
- rule: Unauthorized Ghost Content Access
  desc: Detect unauthorized access to Ghost content directory
  condition: >
    open_read and
    container.image.repository = "ghost" and
    fd.name startswith "/var/lib/ghost/content/" and
    not proc.name in (node, nginx, ghost)
  output: >
    Unauthorized access to Ghost content
    (user=%user.name command=%proc.cmdline file=%fd.name)
  priority: WARNING
  tags: [ghost, unauthorized_access]
```

## Deployment

### Prerequisites

- Kubernetes cluster (v1.25+)
- Helm 3.x
- kubectl configured
- cert-manager installed
- Prometheus Operator installed

### Deployment Order

1. **Deploy Vault**:
```bash
cd vault
helm install vault hashicorp/vault \
  --namespace vault \
  --create-namespace \
  -f vault-values.yaml

# Wait for pods to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=vault -n vault --timeout=300s

# Initialize Vault
./vault-setup.sh
```

2. **Deploy OPA Gatekeeper**:
```bash
cd policies
helm install gatekeeper gatekeeper/gatekeeper \
  --namespace gatekeeper-system \
  --create-namespace

# Deploy templates and constraints
kubectl apply -f constraint-templates.yaml
kubectl apply -f opa-gatekeeper-constraints.yaml
kubectl apply -f privileged-containers.yaml
kubectl apply -f allowed-repos.yaml
kubectl apply -f required-labels.yaml
kubectl apply -f resource-limits.yaml
```

3. **Deploy Trivy Operator**:
```bash
cd scanning
helm install trivy-operator aqua/trivy-operator \
  --namespace trivy-system \
  --create-namespace \
  -f trivy-operator-values.yaml
```

4. **Deploy Falco**:
```bash
cd scanning
helm install falco falcosecurity/falco \
  --namespace falco \
  --create-namespace \
  -f falco-values.yaml
```

### Verification

```bash
# Check all security components
kubectl get pods -n vault
kubectl get pods -n gatekeeper-system
kubectl get pods -n trivy-system
kubectl get pods -n falco

# Verify Gatekeeper constraints
kubectl get constraints

# Check Trivy scans
kubectl get vulnerabilityreports -A

# View Falco alerts
kubectl logs -n falco -l app.kubernetes.io/name=falco --tail=50
```

## Configuration

### Vault Configuration

**Configure Database Secrets Engine**:
```bash
vault write database/config/ghost-mysql \
  plugin_name=mysql-database-plugin \
  connection_url="{{username}}:{{password}}@tcp(mysql.ghost.svc:3306)/" \
  allowed_roles="ghost-app,ghost-readonly" \
  username="vault-admin" \
  password="${MYSQL_PASSWORD}"

vault write database/roles/ghost-app \
  db_name=ghost-mysql \
  creation_statements="CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON ghost.* TO '{{name}}'@'%';" \
  default_ttl="1h" \
  max_ttl="24h"
```

**Configure Transit Encryption**:
```bash
vault write -f transit/keys/ghost-pii \
  type=aes256-gcm96 \
  deletion_allowed=false

vault write -f transit/keys/ghost-content \
  type=aes256-gcm96 \
  deletion_allowed=false
```

**Configure PKI**:
```bash
vault secrets enable pki
vault secrets tune -max-lease-ttl=87600h pki

vault write pki/root/generate/internal \
  common_name="Ghost Internal CA" \
  ttl=87600h

vault write pki/roles/ghost-internal \
  allowed_domains="ghost.internal" \
  allow_subdomains=true \
  max_ttl="720h"
```

### Gatekeeper Configuration

**Adjust Enforcement**:
```bash
# Change to warn mode for testing
kubectl patch k8spsprivilegedcontainer block-privileged-containers \
  --type='json' \
  -p='[{"op": "replace", "path": "/spec/enforcementAction", "value":"warn"}]'

# Change back to deny mode
kubectl patch k8spsprivilegedcontainer block-privileged-containers \
  --type='json' \
  -p='[{"op": "replace", "path": "/spec/enforcementAction", "value":"deny"}]'
```

**Add Namespace Exclusions**:
```yaml
spec:
  match:
    excludedNamespaces:
      - kube-system
      - monitoring
      - my-special-namespace
```

### Trivy Configuration

**Adjust Scan Schedules**:
```yaml
vulnerabilityReports:
  schedule: "0 */12 * * *"  # Every 12 hours

configAuditReports:
  schedule: "0 0 * * *"  # Daily

infraAssessmentReports:
  schedule: "0 2 * * 0"  # Weekly Sunday 2 AM
```

**Configure Severity Thresholds**:
```yaml
policy:
  severityThreshold: CRITICAL
  maxAllowedBySeverity:
    CRITICAL: 0
    HIGH: 5
    MEDIUM: 20
```

### Falco Configuration

**Adjust Alert Thresholds**:
```yaml
falco:
  priority: WARNING  # DEBUG, INFO, NOTICE, WARNING, ERROR, CRITICAL, ALERT, EMERGENCY
  
  outputs:
    rate: 1
    maxBurst: 1000
```

**Configure Alert Destinations**:
```yaml
falcosidekick:
  config:
    slack:
      webhookurl: "https://hooks.slack.com/services/YOUR/WEBHOOK"
      minimumpriority: "warning"
    
    pagerduty:
      routingkey: "YOUR_ROUTING_KEY"
      minimumpriority: "critical"
```

## Monitoring

### Metrics Collection

All security components export Prometheus metrics:

**Vault Metrics**:
- Endpoint: `https://vault.vault.svc:8200/v1/sys/metrics`
- Metrics: Token operations, secret reads/writes, seal status

**Gatekeeper Metrics**:
- Endpoint: `http://gatekeeper-controller-manager.gatekeeper-system.svc:8888/metrics`
- Metrics: Constraint violations, audit results, webhook latency

**Trivy Metrics**:
- Endpoint: `http://trivy-operator.trivy-system.svc:8080/metrics`
- Metrics: Vulnerability counts, scan duration, report status

**Falco Metrics**:
- Endpoint: `http://falco.falco.svc:8765/metrics`
- Metrics: Event counts by severity, dropped events, rules triggered

### Grafana Dashboards

Import pre-built dashboards:

1. **Vault Dashboard**: ID 12904
2. **Gatekeeper Dashboard**: ID 15141
3. **Trivy Dashboard**: Custom (available in repository)
4. **Falco Dashboard**: ID 11914

Access Grafana:
```bash
kubectl port-forward -n monitoring svc/grafana 3000:80
```

### Alerting Rules

**Prometheus Alert Examples**:
```yaml
groups:
  - name: security-alerts
    rules:
      - alert: VaultSealed
        expr: vault_core_unsealed == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Vault is sealed"
      
      - alert: CriticalVulnerability
        expr: trivy_vulnerability_count{severity="CRITICAL"} > 0
        for: 0m
        labels:
          severity: critical
        annotations:
          summary: "Critical vulnerability detected"
      
      - alert: FalcoSecurityEvent
        expr: rate(falco_events{priority="Critical"}[5m]) > 0
        for: 0m
        labels:
          severity: critical
        annotations:
          summary: "Falco detected critical security event"
```

### Log Aggregation

**Configure Log Forwarding**:

1. **Vault Audit Logs** → CloudWatch Logs
2. **Falco Alerts** → Elasticsearch
3. **Gatekeeper Violations** → Loki
4. **Trivy Reports** → S3

**Query Examples**:
```bash
# Query Loki for Gatekeeper violations
logcli query '{namespace="gatekeeper-system", level="error"}'

# Query Elasticsearch for Falco events
curl -X GET "https://elasticsearch:9200/falco-alerts/_search?q=priority:Critical"

# View Vault audit logs in CloudWatch
aws logs tail /aws/vault/ghost --follow
```

## Incident Response

### Security Event Workflow

```
┌─────────────────┐
│  Event Occurs   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Detection     │ ◄── Falco, Trivy, Gatekeeper
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Alerting      │ ◄── Slack, PagerDuty, Email
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Triage        │ ◄── Security Team
└────────┬────────┘
         │
         ├─ Low/Medium → Create Ticket
         │
         └─ High/Critical → Immediate Response
                           ┌─────────────────┐
                           │  Containment    │
                           └────────┬────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │  Investigation  │
                           └────────┬────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │  Remediation    │
                           └────────┬────────┘
                                    │
                                    ▼
                           ┌─────────────────┐
                           │  Post-Mortem    │
                           └─────────────────┘
```

### Incident Response Playbooks

#### 1. Critical Vulnerability Detected

**Alert**: Trivy finds CRITICAL vulnerability in production image

**Actions**:
1. Identify affected deployments
2. Check if vulnerability is exploitable
3. Review available patches
4. Plan deployment window
5. Deploy patched version
6. Verify vulnerability resolved

**Commands**:
```bash
# Find affected deployments
kubectl get vulnerabilityreports -A -o json | \
  jq '.items[] | select(.report.summary.criticalCount > 0)'

# Check deployment image
kubectl get deployment -n ghost-production ghost-webapi -o jsonpath='{.spec.template.spec.containers[0].image}'

# Update deployment
kubectl set image deployment/ghost-webapi -n ghost-production \
  ghost-webapi=ghost:v1.2.3-patched

# Verify
kubectl rollout status deployment/ghost-webapi -n ghost-production
```

#### 2. Falco Security Alert

**Alert**: Falco detects shell spawned in container

**Actions**:
1. Identify affected pod
2. Capture forensic data
3. Isolate pod (network policy)
4. Analyze logs
5. Determine if legitimate or attack
6. Remediate and restore

**Commands**:
```bash
# Get pod details from alert
export POD_NAME="ghost-webapi-abc123"
export NAMESPACE="ghost-production"

# Capture pod logs
kubectl logs $POD_NAME -n $NAMESPACE --all-containers > incident-logs.txt

# Get pod events
kubectl get events -n $NAMESPACE --field-selector involvedObject.name=$POD_NAME

# Isolate pod (apply deny-all network policy)
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: isolate-$POD_NAME
  namespace: $NAMESPACE
spec:
  podSelector:
    matchLabels:
      pod: $POD_NAME
  policyTypes:
  - Ingress
  - Egress
EOF

# Terminate pod
kubectl delete pod $POD_NAME -n $NAMESPACE
```

#### 3. Policy Violation Detected

**Alert**: Gatekeeper blocks privileged container

**Actions**:
1. Review violation details
2. Contact requestor
3. Assess if exception needed
4. If legitimate, create exception
5. If not, educate team

**Commands**:
```bash
# View violation
kubectl describe k8spsprivilegedcontainer block-privileged-containers

# Get violation details
kubectl get k8spsprivilegedcontainer block-privileged-containers \
  -o jsonpath='{.status.violations}' | jq

# If exception needed, add to excludedNamespaces
kubectl edit k8spsprivilegedcontainer block-privileged-containers
```

#### 4. Vault Unsealed

**Alert**: Vault is sealed and inaccessible

**Actions**:
1. Check Vault pod status
2. Retrieve unseal keys from secure storage
3. Unseal Vault cluster
4. Verify services restore

**Commands**:
```bash
# Check Vault status
kubectl exec -n vault vault-0 -- vault status

# Unseal each Vault pod (need 3 of 5 keys)
for i in 0 1 2; do
  kubectl exec -n vault vault-$i -- vault operator unseal $UNSEAL_KEY_1
  kubectl exec -n vault vault-$i -- vault operator unseal $UNSEAL_KEY_2
  kubectl exec -n vault vault-$i -- vault operator unseal $UNSEAL_KEY_3
done

# Verify
kubectl exec -n vault vault-0 -- vault status
```

### Post-Incident Activities

1. **Document Incident**:
   - Timeline of events
   - Actions taken
   - Root cause analysis
   - Impact assessment

2. **Update Runbooks**:
   - Add new detection rules
   - Update response procedures
   - Document lessons learned

3. **Improve Detection**:
   - Add new Falco rules
   - Adjust Gatekeeper policies
   - Update Trivy exceptions

4. **Team Communication**:
   - Post-mortem meeting
   - Share findings
   - Update documentation

## Compliance

### Supported Standards

1. **CIS Kubernetes Benchmark v1.7**
2. **NSA Kubernetes Hardening Guide**
3. **PCI-DSS 3.2.1**
4. **SOC 2 Type II**
5. **HIPAA** (when applicable)
6. **GDPR** (data protection)

### Compliance Reports

**Generate CIS Report**:
```bash
kubectl get clustercompliancereport cis -o yaml
```

**Generate NSA Report**:
```bash
kubectl get clustercompliancereport nsa -o yaml
```

**Export Reports**:
```bash
# Export to JSON
kubectl get clustercompliancereport cis -o json > cis-report.json

# Export to HTML (requires custom script)
./scripts/generate-compliance-html.sh cis-report.json > cis-report.html
```

### Audit Trail

All security-relevant events are logged:

1. **Vault Audit Log**: All secret access
2. **Kubernetes Audit Log**: All API calls
3. **Falco Events**: Runtime security events
4. **Gatekeeper Violations**: Policy violations

**Access Audit Logs**:
```bash
# Vault audit
kubectl exec -n vault vault-0 -- cat /vault/audit/vault-audit.log

# Kubernetes audit (varies by setup)
aws logs tail /aws/eks/ghost/cluster --follow

# Falco events
kubectl logs -n falco -l app.kubernetes.io/name=falco

# Gatekeeper violations
kubectl get constraints -o yaml | grep -A 10 violations
```

## Best Practices

### Secrets Management

1. **Never commit secrets to Git**
   - Use Vault for all secrets
   - Use sealed-secrets or external-secrets for K8s secrets

2. **Rotate secrets regularly**
   - Database passwords: 90 days
   - API keys: 180 days
   - TLS certificates: 90 days (automated)

3. **Use dynamic secrets when possible**
   - Database credentials from Vault
   - Cloud credentials from Vault AWS engine

4. **Encrypt sensitive data**
   - Use Vault Transit for PII
   - Encrypt data at rest
   - Use TLS for data in transit

### Policy Enforcement

1. **Start with warn mode**
   - Test policies before enforcement
   - Review violations
   - Adjust policies as needed

2. **Use namespace exclusions carefully**
   - Only exclude system namespaces
   - Document exceptions
   - Review exceptions quarterly

3. **Keep policies updated**
   - Update for new vulnerabilities
   - Align with industry standards
   - Review quarterly

### Vulnerability Management

1. **Scan images before deployment**
   - CI/CD integration
   - Block CRITICAL vulnerabilities
   - Report HIGH vulnerabilities

2. **Regular scanning**
   - Production: Every 6 hours
   - Staging: Daily
   - Development: Weekly

3. **Patch management**
   - CRITICAL: Immediate (< 24h)
   - HIGH: Within 7 days
   - MEDIUM: Within 30 days
   - LOW: Within 90 days

### Runtime Security

1. **Monitor continuously**
   - Review Falco alerts daily
   - Investigate anomalies
   - Update rules based on learnings

2. **Minimize attack surface**
   - Run as non-root
   - Use read-only filesystems
   - Drop all capabilities
   - No privileged containers

3. **Network segmentation**
   - Use NetworkPolicies
   - Implement service mesh
   - Restrict egress traffic

### Access Control

1. **Principle of least privilege**
   - Minimal RBAC permissions
   - Just-in-time access
   - Regular access reviews

2. **Multi-factor authentication**
   - MFA for Vault access
   - MFA for cluster access
   - MFA for cloud console

3. **Audit everything**
   - Enable audit logging
   - Centralize logs
   - Retain logs per compliance requirements

## Troubleshooting

### Common Issues

#### Vault

**Issue**: Vault is sealed
```bash
# Check status
kubectl exec -n vault vault-0 -- vault status

# Unseal
kubectl exec -n vault vault-0 -- vault operator unseal $KEY
```

**Issue**: Pods can't authenticate to Vault
```bash
# Check Kubernetes auth config
kubectl exec -n vault vault-0 -- vault read auth/kubernetes/config

# Verify service account
kubectl get sa -n ghost-production

# Check role binding
kubectl exec -n vault vault-0 -- vault read auth/kubernetes/role/ghost
```

#### Gatekeeper

**Issue**: Policies not enforcing
```bash
# Check Gatekeeper pods
kubectl get pods -n gatekeeper-system

# Check webhook
kubectl get validatingwebhookconfigurations

# View constraint status
kubectl get constraints
```

**Issue**: False positives
```bash
# Add namespace exclusion
kubectl edit constraint <constraint-name>

# Or change to warn mode temporarily
kubectl patch constraint <name> \
  --type='json' \
  -p='[{"op": "replace", "path": "/spec/enforcementAction", "value":"warn"}]'
```

#### Trivy

**Issue**: Scans not running
```bash
# Check operator pods
kubectl get pods -n trivy-system

# Check scan jobs
kubectl get jobs -n ghost-production

# View operator logs
kubectl logs -n trivy-system -l app.kubernetes.io/name=trivy-operator
```

**Issue**: Database update failures
```bash
# Check vulnerability database
kubectl get configmap -n trivy-system trivy-operator-trivy-config

# Manually trigger DB update
kubectl delete pod -n trivy-system -l app.kubernetes.io/name=trivy-operator
```

#### Falco

**Issue**: No alerts appearing
```bash
# Check Falco pods
kubectl get pods -n falco

# View Falco logs
kubectl logs -n falco -l app.kubernetes.io/name=falco

# Check sidekick
kubectl logs -n falco -l app.kubernetes.io/name=falco-sidekick

# Verify rules loaded
kubectl exec -n falco -it falco-xxx -- falco -L
```

**Issue**: Too many alerts (noise)
```bash
# Adjust minimum priority
kubectl edit configmap -n falco falco-config

# Add suppressions
kubectl edit configmap -n falco falco-custom-rules
```

## Maintenance

### Regular Tasks

**Daily**:
- [ ] Review Falco critical alerts
- [ ] Check Vault seal status
- [ ] Review new Trivy CRITICAL findings

**Weekly**:
- [ ] Review Gatekeeper violations
- [ ] Check Vault audit logs
- [ ] Review Trivy HIGH findings
- [ ] Update vulnerability exceptions

**Monthly**:
- [ ] Review and update policies
- [ ] Rotate API keys
- [ ] Review RBAC permissions
- [ ] Compliance report review

**Quarterly**:
- [ ] Security assessment
- [ ] Policy effectiveness review
- [ ] Update documentation
- [ ] Disaster recovery drill
- [ ] Rotate Vault unseal keys

### Backup Procedures

**Vault Backup**:
```bash
# Backup Raft storage
kubectl exec -n vault vault-0 -- vault operator raft snapshot save backup.snap

# Copy backup
kubectl cp vault/vault-0:backup.snap ./vault-backup-$(date +%Y%m%d).snap

# Upload to S3
aws s3 cp ./vault-backup-*.snap s3://ghost-backups/vault/
```

**Gatekeeper Backup**:
```bash
# Export all constraints
kubectl get constraints -A -o yaml > gatekeeper-constraints-backup.yaml

# Export templates
kubectl get constrainttemplates -o yaml > gatekeeper-templates-backup.yaml
```

## Support

### Contacts

- **Security Team**: security@ghost.internal
- **On-Call**: oncall@ghost.internal
- **Slack**: #ghost-security

### Documentation

- Vault: https://www.vaultproject.io/docs
- OPA Gatekeeper: https://open-policy-agent.github.io/gatekeeper/
- Trivy: https://aquasecurity.github.io/trivy/
- Falco: https://falco.org/docs/

### Getting Help

```bash
# Check component status
kubectl get all -n vault
kubectl get all -n gatekeeper-system
kubectl get all -n trivy-system
kubectl get all -n falco

# View logs
kubectl logs -n <namespace> <pod-name>

# Describe resources
kubectl describe <resource> <name> -n <namespace>
```

## Appendix

### Security Checklist

- [ ] Vault deployed in HA mode
- [ ] Vault auto-unseal configured
- [ ] Vault audit logging enabled
- [ ] Vault policies configured
- [ ] Gatekeeper installed and configured
- [ ] All constraint templates deployed
- [ ] Security policies enforced
- [ ] Trivy Operator installed
- [ ] Vulnerability scanning enabled
- [ ] Compliance reporting configured
- [ ] Falco installed with eBPF driver
- [ ] Falco rules customized for Ghost
- [ ] Alert channels configured
- [ ] Prometheus metrics enabled
- [ ] Grafana dashboards imported
- [ ] Log aggregation configured
- [ ] Incident response procedures documented
- [ ] Backup procedures tested
- [ ] Disaster recovery plan created

### Quick Reference

**Deploy All Security Components**:
```bash
./scripts/deploy-security.sh
```

**Check Security Status**:
```bash
./scripts/security-status.sh
```

**Generate Compliance Report**:
```bash
./scripts/compliance-report.sh
```

**Emergency Contacts**:
- Security Team: +1-555-SECURITY
- On-Call Engineer: +1-555-ONCALL

---

**Last Updated**: February 2026  
**Version**: 1.0  
**Owner**: Security Team  
**Review Frequency**: Quarterly
