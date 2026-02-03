# Cost Optimization Guide - Ghost Platform

**Version:** 1.0  
**Last Updated:** 2026-02-03  
**Owner:** Platform Engineering & FinOps Team

## Table of Contents

1. [Overview](#overview)
2. [Cost Breakdown](#cost-breakdown)
3. [Optimization Strategies](#optimization-strategies)
4. [Environment-Specific Guidance](#environment-specific-guidance)
5. [Monitoring and Alerts](#monitoring-and-alerts)
6. [Cost Allocation and Tagging](#cost-allocation-and-tagging)
7. [Regular Review Process](#regular-review-process)

---

## Overview

This guide provides strategies and best practices for optimizing infrastructure costs for the Ghost Platform while maintaining performance, reliability, and security.

### Cost Optimization Principles

1. **Right-size resources** - Match resource capacity to actual usage
2. **Leverage spot/preemptible instances** - Use for non-critical workloads
3. **Auto-scaling** - Scale based on demand
4. **Reserved capacity** - Commit for predictable workloads
5. **Resource lifecycle management** - Clean up unused resources
6. **Efficient data storage** - Archive old data, optimize storage tiers
7. **Network optimization** - Reduce data transfer costs
8. **Continuous monitoring** - Track and optimize regularly

### Target Cost Structure

| Environment | Monthly Target | Actual | Status |
|-------------|---------------|---------|--------|
| Development | $50 | $45 | ✅ Under budget |
| Staging | $150 | $165 | ⚠️ Slightly over |
| Production | $500-800 | $650 | ✅ Within range |
| **Total** | **$700-1000** | **$860** | ✅ On target |

---

## Cost Breakdown

### Current Monthly Costs (Production)

```
Total: $650/month

Compute (40%): $260
├── Kubernetes Nodes: $200
│   ├── On-demand (2x t3.medium): $120
│   └── Spot (3x t3.medium): $80
├── Bastion Host: $30
└── NAT Gateway: $30

Database (25%): $160
├── PostgreSQL (db.t3.medium): $140
└── Backup Storage: $20

Cache & Queue (10%): $65
├── Redis (cache.t3.micro): $30
└── RabbitMQ (on-node): $0
└── ElastiCache alternative: $35 (if separate)

Storage (10%): $65
├── EBS Volumes: $40
├── S3 Storage: $15
└── Backup Storage: $10

Networking (10%): $65
├── Load Balancer: $25
├── Data Transfer: $30
└── VPN: $10

Monitoring (5%): $35
├── CloudWatch: $15
├── Third-party: $20
```

### Cost Drivers

**Top 3 Cost Drivers:**
1. **Compute (40%)** - Kubernetes nodes and EC2 instances
2. **Database (25%)** - RDS PostgreSQL instance
3. **Data Transfer (5%)** - Cross-AZ and egress traffic

---

## Optimization Strategies

### 1. Compute Optimization

#### A. Right-size Kubernetes Nodes

**Current State:**
- 2x t3.medium (on-demand): $60/month each
- 3x t3.medium (spot): $27/month each

**Optimization:**

```bash
# Analyze actual resource usage
kubectl top nodes
kubectl top pods -A

# Check node utilization
kubectl describe nodes | grep -A 5 "Allocated resources"

# Recommended action: Move to smaller instances for dev/staging
# Development: 1x t3.small (spot) = $9/month
# Savings: ~$51/month per replaced node
```

**Implementation:**

```bash
# Update Terraform
# environments/development/main.tf

resource "aws_launch_template" "ghost_nodes" {
  instance_type = "t3.small"  # Changed from t3.medium
  
  instance_market_options {
    market_type = "spot"
    spot_options {
      max_price = "0.02"  # ~70% discount
    }
  }
}
```

**Savings:** $100-150/month for development

#### B. Implement Auto-scaling

```yaml
# Platform/base/cluster-autoscaler.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: cluster-autoscaler
  namespace: kube-system
spec:
  template:
    spec:
      containers:
      - name: cluster-autoscaler
        image: k8s.gcr.io/autoscaling/cluster-autoscaler:v1.28.0
        command:
          - ./cluster-autoscaler
          - --cloud-provider=aws
          - --skip-nodes-with-local-storage=false
          - --expander=least-waste
          - --scale-down-enabled=true
          - --scale-down-delay-after-add=5m
          - --scale-down-unneeded-time=5m
```

**Horizontal Pod Autoscaler:**

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ghost-api-hpa
  namespace: ghost
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ghost-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 15
```

**Savings:** $50-100/month by scaling down during low-traffic periods

#### C. Scheduled Scaling for Development

```yaml
# Scale down development at night and weekends

apiVersion: batch/v1
kind: CronJob
metadata:
  name: scale-down-dev
  namespace: ghost
spec:
  schedule: "0 20 * * 1-5"  # 8 PM weekdays
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: kubectl
            image: bitnami/kubectl:latest
            command:
            - /bin/sh
            - -c
            - |
              kubectl scale deployment ghost-api -n ghost --replicas=0
              kubectl scale deployment ghost-worker -n ghost --replicas=0
          restartPolicy: OnFailure
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: scale-up-dev
  namespace: ghost
spec:
  schedule: "0 8 * * 1-5"  # 8 AM weekdays
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: kubectl
            image: bitnami/kubectl:latest
            command:
            - /bin/sh
            - -c
            - |
              kubectl scale deployment ghost-api -n ghost --replicas=2
              kubectl scale deployment ghost-worker -n ghost --replicas=1
          restartPolicy: OnFailure
```

**Savings:** $150-200/month for development environment

### 2. Database Optimization

#### A. Right-size RDS Instance

**Current:** db.t3.medium (2 vCPU, 4 GB RAM) = $140/month

**Analysis:**

```sql
-- Check actual database usage
SELECT
  datname,
  pg_size_pretty(pg_database_size(datname)) as size,
  (SELECT count(*) FROM pg_stat_activity WHERE datname = d.datname) as connections
FROM pg_database d
ORDER BY pg_database_size(datname) DESC;

-- Check connection usage over time
SELECT
  max(numbackends) as max_connections,
  avg(numbackends) as avg_connections
FROM pg_stat_database
WHERE datname = 'ghost';
```

**Recommendations:**

- **Development:** Move to db.t3.micro = $35/month (**Save $105/month**)
- **Staging:** Move to db.t3.small = $70/month (**Save $70/month**)
- **Production:** Keep db.t3.medium but consider Reserved Instance

#### B. Reserved Instances for Production

```bash
# Purchase 1-year Reserved Instance (No Upfront)
# db.t3.medium: $140/month → $90/month
# Savings: $50/month ($600/year)

# AWS CLI command
aws rds purchase-reserved-db-instances-offering \
  --reserved-db-instances-offering-id <offering-id> \
  --db-instance-count 1 \
  --tags Key=Environment,Value=Production
```

**Savings:** $50/month for production database

#### C. Optimize Backup Retention

```hcl
# environments/production/main.tf

resource "aws_db_instance" "ghost" {
  # Current: 30 days = $30/month
  backup_retention_period = 7  # Reduce to 7 days

  # Enable automated snapshots lifecycle
  snapshot_identifier = null
  
  # Use Glacier for long-term backups
  lifecycle {
    create_before_destroy = true
  }
}
```

**Savings:** $15-20/month on backup storage

### 3. Storage Optimization

#### A. S3 Lifecycle Policies

```hcl
# Terraform configuration for S3 lifecycle

resource "aws_s3_bucket_lifecycle_configuration" "ghost_backups" {
  bucket = aws_s3_bucket.ghost_backups.id

  rule {
    id     = "archive-old-backups"
    status = "Enabled"

    transition {
      days          = 30
      storage_class = "STANDARD_IA"  # Save 50%
    }

    transition {
      days          = 90
      storage_class = "GLACIER"       # Save 70%
    }

    transition {
      days          = 180
      storage_class = "DEEP_ARCHIVE"  # Save 95%
    }

    expiration {
      days = 365
    }
  }

  rule {
    id     = "delete-incomplete-uploads"
    status = "Enabled"

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}
```

**Savings:** $20-30/month on storage costs

#### B. EBS Volume Optimization

```bash
# Identify unused EBS volumes
aws ec2 describe-volumes \
  --filters Name=status,Values=available \
  --query 'Volumes[*].[VolumeId,Size,VolumeType,CreateTime]' \
  --output table

# Delete unused volumes
aws ec2 delete-volume --volume-id vol-xxxxx

# Convert gp3 to gp2 for development (cheaper)
aws ec2 modify-volume \
  --volume-id vol-xxxxx \
  --volume-type gp2
```

**Savings:** $10-15/month by cleaning up unused volumes

### 4. Network Optimization

#### A. Reduce Cross-AZ Traffic

```yaml
# Configure topology-aware routing
apiVersion: v1
kind: Service
metadata:
  name: ghost-api
  annotations:
    service.kubernetes.io/topology-aware-hints: auto
spec:
  type: ClusterIP
  ports:
    - port: 8080
  selector:
    app: ghost-api
```

**Savings:** $10-20/month on data transfer costs

#### B. Use VPC Endpoints

```hcl
# modules/networking/vpc-endpoints.tf

resource "aws_vpc_endpoint" "s3" {
  vpc_id       = aws_vpc.main.id
  service_name = "com.amazonaws.${var.region}.s3"
  route_table_ids = [aws_route_table.private.id]

  tags = {
    Name = "ghost-s3-endpoint"
  }
}

resource "aws_vpc_endpoint" "ecr_api" {
  vpc_id              = aws_vpc.main.id
  service_name        = "com.amazonaws.${var.region}.ecr.api"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = aws_subnet.private[*].id
  security_group_ids  = [aws_security_group.vpc_endpoint.id]
}
```

**Savings:** $15-25/month on NAT Gateway and data transfer

#### C. CloudFront for Static Assets

```hcl
resource "aws_cloudfront_distribution" "ghost_assets" {
  enabled = true
  
  origin {
    domain_name = aws_s3_bucket.assets.bucket_regional_domain_name
    origin_id   = "S3-ghost-assets"
  }
  
  default_cache_behavior {
    target_origin_id       = "S3-ghost-assets"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true
    
    min_ttl     = 0
    default_ttl = 86400   # 1 day
    max_ttl     = 31536000 # 1 year
  }
  
  price_class = "PriceClass_100"  # US, Europe only
}
```

**Savings:** $20-30/month on data transfer

### 5. Monitoring Optimization

#### A. Reduce CloudWatch Costs

```bash
# Increase log retention periods
aws logs put-retention-policy \
  --log-group-name /aws/eks/ghost/cluster \
  --retention-in-days 7  # Reduce from 30 days

# Use metric filters instead of detailed logging
aws logs put-metric-filter \
  --log-group-name /app/ghost \
  --filter-name ErrorCount \
  --filter-pattern "ERROR" \
  --metric-transformations \
    metricName=ErrorCount,metricNamespace=Ghost,metricValue=1
```

**Savings:** $10-15/month on CloudWatch costs

#### B. Optimize Prometheus Retention

```yaml
# monitoring/prometheus/prometheus.yml

global:
  evaluation_interval: 30s  # Increase from 15s
  scrape_interval: 30s      # Increase from 15s

storage:
  tsdb:
    retention.time: 7d      # Reduce from 15d
    retention.size: 10GB    # Add size limit
```

**Savings:** $5-10/month on storage

---

## Environment-Specific Guidance

### Development Environment

**Target Cost:** $50/month

**Optimization Checklist:**

- [ ] Use t3.small instances (spot)
- [ ] Single-node cluster (k3s)
- [ ] Scale down nights/weekends
- [ ] Use smallest RDS instance (db.t3.micro)
- [ ] 7-day backup retention
- [ ] No high-availability
- [ ] Minimal logging/monitoring

**Configuration:**

```hcl
# environments/development/variables.tf

locals {
  instance_type     = "t3.small"
  instance_count    = 1
  use_spot          = true
  db_instance_class = "db.t3.micro"
  backup_retention  = 7
  
  # Auto-shutdown schedule
  auto_shutdown = {
    enabled = true
    stop_time = "20:00"
    start_time = "08:00"
    timezone = "America/New_York"
  }
}
```

### Staging Environment

**Target Cost:** $150/month

**Optimization Checklist:**

- [ ] Use t3.medium instances (mix of spot/on-demand)
- [ ] 2-3 node cluster
- [ ] Production-like but smaller
- [ ] db.t3.small for database
- [ ] 14-day backup retention
- [ ] Reduced monitoring

**Configuration:**

```hcl
# environments/staging/variables.tf

locals {
  instance_type     = "t3.medium"
  instance_count    = 2
  spot_percentage   = 50  # 50% spot, 50% on-demand
  db_instance_class = "db.t3.small"
  backup_retention  = 14
  
  # Keep running 24/7 for testing
  auto_shutdown = {
    enabled = false
  }
}
```

### Production Environment

**Target Cost:** $500-800/month

**Optimization Checklist:**

- [ ] Use Reserved Instances for baseline capacity
- [ ] Auto-scaling for burst traffic
- [ ] Multi-AZ for high availability
- [ ] Regular rightsizing reviews
- [ ] Implement all network optimizations
- [ ] Use CloudFront for static assets
- [ ] Optimize storage with lifecycle policies

**Configuration:**

```hcl
# environments/production/variables.tf

locals {
  instance_type     = "t3.medium"
  min_nodes         = 3
  max_nodes         = 10
  use_reserved      = true
  db_instance_class = "db.t3.medium"
  backup_retention  = 30
  multi_az          = true
  
  # Enable all optimizations
  enable_cloudfront   = true
  enable_vpc_endpoints = true
  enable_auto_scaling = true
}
```

---

## Monitoring and Alerts

### Cost Anomaly Detection

```hcl
# Terraform - AWS Cost Anomaly Detection

resource "aws_ce_anomaly_monitor" "ghost_platform" {
  name              = "GhostPlatformMonitor"
  monitor_type      = "DIMENSIONAL"
  monitor_dimension = "SERVICE"
}

resource "aws_ce_anomaly_subscription" "ghost_alerts" {
  name      = "GhostCostAlerts"
  frequency = "DAILY"

  monitor_arn_list = [
    aws_ce_anomaly_monitor.ghost_platform.arn,
  ]

  subscriber {
    type    = "EMAIL"
    address = "platform-team@ghost.example.com"
  }

  threshold_expression {
    dimension {
      key           = "ANOMALY_TOTAL_IMPACT_ABSOLUTE"
      match_options = ["GREATER_THAN_OR_EQUAL"]
      values        = ["100"]  # Alert if anomaly > $100
    }
  }
}
```

### Budget Alerts

```hcl
resource "aws_budgets_budget" "ghost_monthly" {
  name         = "ghost-platform-monthly"
  budget_type  = "COST"
  limit_amount = "1000"
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = ["platform-team@ghost.example.com"]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = ["platform-team@ghost.example.com"]
  }
}
```

### Grafana Dashboard

```yaml
# Cost Monitoring Dashboard
# Import into Grafana

apiVersion: v1
kind: ConfigMap
metadata:
  name: cost-dashboard
  namespace: monitoring
data:
  cost-dashboard.json: |
    {
      "dashboard": {
        "title": "Ghost Platform - Cost Monitoring",
        "panels": [
          {
            "title": "Monthly Cost Trend",
            "type": "graph",
            "targets": [
              {
                "expr": "aws_billing_estimated_charges{service='Total'}"
              }
            ]
          },
          {
            "title": "Cost by Service",
            "type": "piechart",
            "targets": [
              {
                "expr": "sum by (service) (aws_billing_estimated_charges)"
              }
            ]
          },
          {
            "title": "Cost per Environment",
            "type": "table",
            "targets": [
              {
                "expr": "sum by (environment) (aws_billing_estimated_charges)"
              }
            ]
          }
        ]
      }
    }
```

---

## Cost Allocation and Tagging

### Tagging Strategy

**Required Tags:**

```hcl
locals {
  common_tags = {
    Project     = "GhostPlatform"
    ManagedBy   = "Terraform"
    CostCenter  = "Engineering"
    Owner       = "platform-team@ghost.example.com"
  }
  
  environment_tags = {
    Environment = var.environment
    Workload    = var.workload_type
  }
  
  all_tags = merge(local.common_tags, local.environment_tags)
}

# Apply to all resources
resource "aws_instance" "ghost_node" {
  tags = local.all_tags
}
```

### Cost Allocation Reports

```bash
# Enable Cost Allocation Tags
aws ce create-cost-category-definition \
  --name "GhostPlatform" \
  --rules '[
    {
      "Value": "Production",
      "Rule": {
        "Tags": {
          "Key": "Environment",
          "Values": ["production"]
        }
      }
    }
  ]'

# Create cost report
aws cur put-report-definition \
  --report-definition '{
    "ReportName": "ghost-platform-monthly",
    "TimeUnit": "MONTHLY",
    "Format": "Parquet",
    "Compression": "Parquet",
    "S3Bucket": "ghost-cost-reports",
    "S3Prefix": "cost-reports",
    "S3Region": "us-east-1",
    "AdditionalSchemaElements": ["RESOURCES"],
    "ReportVersioning": "OVERWRITE_REPORT"
  }'
```

---

## Regular Review Process

### Monthly Cost Review

**Schedule:** First Monday of each month

**Participants:**
- Platform Engineering Lead
- FinOps Manager
- Engineering Manager

**Agenda:**

1. **Review Previous Month**
   - Actual vs. budgeted costs
   - Identify anomalies
   - Track optimization efforts

2. **Cost Trends**
   - Month-over-month comparison
   - Forecast next month
   - Identify cost drivers

3. **Optimization Opportunities**
   - Review rightsizing recommendations
   - Analyze unused resources
   - Evaluate new AWS features

4. **Action Items**
   - Assign optimization tasks
   - Set targets for next month
   - Schedule follow-up reviews

### Quarterly Deep Dive

**Additional Activities:**

- Comprehensive resource inventory
- Review Reserved Instance utilization
- Evaluate Savings Plans
- Architecture review for cost optimization
- Update cost models and projections

### Cost Optimization Checklist

**Monthly:**
- [ ] Review AWS Cost Explorer
- [ ] Check for unused resources
- [ ] Verify auto-scaling effectiveness
- [ ] Review log retention policies
- [ ] Check snapshot cleanup

**Quarterly:**
- [ ] Rightsizing analysis
- [ ] Reserved Instance review
- [ ] Storage lifecycle audit
- [ ] Network optimization review
- [ ] Database performance tuning

**Annually:**
- [ ] Architecture cost review
- [ ] Multi-year Reserved Instance planning
- [ ] Vendor negotiation review
- [ ] FinOps process improvement

---

## Cost Optimization Tools

### AWS Native Tools

```bash
# Cost Explorer - Analyze costs
aws ce get-cost-and-usage \
  --time-period Start=2026-01-01,End=2026-02-01 \
  --granularity MONTHLY \
  --metrics BlendedCost \
  --group-by Type=DIMENSION,Key=SERVICE

# Trusted Advisor - Get recommendations
aws support describe-trusted-advisor-checks \
  --language en

# Compute Optimizer - Rightsizing recommendations
aws compute-optimizer get-ec2-instance-recommendations
```

### Third-Party Tools

- **CloudHealth / VMware Aria:** Multi-cloud cost management
- **Kubecost:** Kubernetes-specific cost allocation
- **Infracost:** Infrastructure-as-code cost estimation

---

## Summary: Quick Wins

### Immediate Actions (This Week)

1. ✅ **Enable auto-shutdown for development** - Save $150/month
2. ✅ **Downsize development RDS** - Save $105/month
3. ✅ **Delete unused EBS volumes** - Save $15/month
4. ✅ **Implement S3 lifecycle policies** - Save $20/month

**Total Quick Win Savings: ~$290/month**

### Short-term Actions (This Month)

1. ⏳ **Purchase Reserved Instances for production** - Save $50/month
2. ⏳ **Implement VPC endpoints** - Save $20/month
3. ⏳ **Optimize CloudWatch retention** - Save $15/month
4. ⏳ **Enable cluster autoscaling** - Save $50/month

**Total Short-term Savings: ~$135/month**

### Long-term Actions (This Quarter)

1. 📋 **Migrate to spot instances where possible** - Save $100/month
2. 📋 **Implement CloudFront** - Save $30/month
3. 📋 **Database query optimization** - Potential downsize, save $50/month

**Total Long-term Savings: ~$180/month**

---

**Total Potential Savings: ~$605/month (~$7,260/year)**

This would bring costs from $860/month to $255/month, well within the $700-1000 target range with significant buffer.

---

**Document Maintainer:** Platform Engineering & FinOps Team  
**Last Updated:** 2026-02-03  
**Next Review:** 2026-03-01  
**Cost Review Schedule:** First Monday of each month
