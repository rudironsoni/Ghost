# Ghost Observability Stack

Complete self-hosted observability for Ghost distributed scraping at $0 cost.

## Stack Overview

- **Prometheus**: Metrics collection and storage
- **Grafana**: Visualization and dashboards
- **Jaeger**: Distributed tracing
- **Redis Exporter**: Queue metrics
- **OpenTelemetry**: Instrumentation standard

All components run in-cluster, no external services required.

## Quick Start

### 1. Deploy Observability Stack

```bash
cd infrastructure/k8s/observability

# Deploy Prometheus
kubectl apply -f prometheus.yaml

# Deploy Grafana
kubectl apply -f grafana.yaml

# Deploy Jaeger
kubectl apply -f jaeger.yaml

# Verify all pods running
kubectl get pods -n ghost -l component=observability
```

### 2. Access UIs

```bash
# Grafana (http://localhost:3000)
kubectl port-forward -n ghost svc/grafana 3000:3000
# Login: admin / admin (change password on first login)

# Prometheus (http://localhost:9090)
kubectl port-forward -n ghost svc/prometheus 9090:9090

# Jaeger (http://localhost:16686)
kubectl port-forward -n ghost svc/jaeger-query 16686:16686
```

### 3. Import Dashboards

Grafana dashboards are available in `dashboards/` directory:

1. Open Grafana → Dashboards → Import
2. Upload `ghost-worker-dashboard.json`
3. Select Prometheus datasource
4. Click Import

## Metrics

### Worker Metrics

Ghost workers expose these metrics on `/metrics`:

| Metric | Type | Description |
|--------|------|-------------|
| `ghost_worker_jobs_processed_total` | Counter | Total jobs processed |
| `ghost_worker_jobs_failed_total` | Counter | Total jobs failed |
| `ghost_worker_active_jobs` | Gauge | Currently active jobs |
| `ghost_worker_job_duration_seconds` | Histogram | Job processing duration |
| `ghost_worker_queue_depth` | Gauge | Redis queue length |
| `ghost_worker_browser_sessions` | Gauge | Active Playwright sessions |

### Redis Metrics

Provided by Redis Exporter:

| Metric | Description |
|--------|-------------|
| `redis_list_length{key="ghost:jobs:queue"}` | Queue depth |
| `redis_connected_clients` | Connected clients |
| `redis_used_memory_bytes` | Memory usage |
| `redis_commands_processed_total` | Commands processed |

### System Metrics

Provided by Node Exporter (if installed):

- CPU usage per node
- Memory usage per node
- Disk I/O
- Network traffic

## Distributed Tracing

### OpenTelemetry Setup

Ghost uses OpenTelemetry for distributed tracing. Traces show the full journey of a scraping job:

```
Job Enqueued → Worker Picks Up → Browser Launch → Page Navigation → 
Content Extraction → Results Storage → Job Complete
```

### Viewing Traces

1. Open Jaeger UI: http://localhost:16686
2. Select Service: `ghost-worker`
3. Find Operations:
   - `ProcessJob`
   - `SearchJobs`
   - `GetJobDetails`
4. View trace timeline with spans

### Trace Context

Each trace includes:
- **Job ID**: Unique identifier
- **Platform**: LinkedIn, Indeed, etc.
- **Duration**: Total time
- **Spans**: Individual operations (browser launch, network requests, etc.)
- **Errors**: Stack traces if failed

## Dashboards

### Ghost Worker Dashboard

Comprehensive dashboard showing:

**Row 1: Overview**
- Total Jobs Processed (24h)
- Success Rate (%)
- Active Workers
- Queue Depth

**Row 2: Throughput**
- Jobs/sec (time series)
- Job Duration (p50, p95, p99)
- Queue Length (time series)

**Row 3: Resource Usage**
- CPU Usage per worker
- Memory Usage per worker
- Browser Sessions per worker

**Row 4: Errors**
- Failed Jobs by Platform
- Error Rate (time series)
- Top Error Messages (table)

**Row 5: Scaling**
- HPA Current vs Desired Replicas
- Scale Up/Down Events

### Creating Custom Dashboards

1. Open Grafana → Dashboards → New Dashboard
2. Add Panel → Select Prometheus datasource
3. Use PromQL queries:

```promql
# Job processing rate
rate(ghost_worker_jobs_processed_total[5m])

# Average job duration
histogram_quantile(0.95, rate(ghost_worker_job_duration_seconds_bucket[5m]))

# Queue depth
redis_list_length{key="ghost:jobs:queue"}

# Error rate
rate(ghost_worker_jobs_failed_total[5m]) / rate(ghost_worker_jobs_processed_total[5m])
```

## Alerting

### Prometheus Alerts

Alerts are defined in `prometheus-config` ConfigMap. Key alerts:

**Critical Alerts**
- `GhostWorkerDown`: No workers running
- `GhostWorkerHighFailureRate`: >10% jobs failing
- `GhostQueueBacklog`: Queue >10,000 jobs for >10 minutes

**Warning Alerts**
- `GhostWorkerHighMemory`: Worker using >80% memory
- `GhostWorkerSlowJobs`: p95 duration >5 minutes
- `GhostRedisDown`: Redis unavailable

### Alertmanager (Optional)

For notifications (Slack, email, PagerDuty), deploy Alertmanager:

```bash
kubectl apply -f alertmanager.yaml
```

Configure receivers in `alertmanager-config` ConfigMap.

## Performance Monitoring

### Key Metrics to Watch

1. **Queue Depth**: Should be <1000 under normal load
   - High depth → Need more workers (scale up)
   - Zero depth → Can scale down workers

2. **Job Duration**: p95 should be <60 seconds
   - High duration → Check target site latency or rate limiting
   - Consider adding retries or proxy rotation

3. **Success Rate**: Should be >95%
   - Low rate → Investigate error logs
   - Check for bot detection, rate limiting, or network issues

4. **Worker Resource Usage**: CPU <70%, Memory <80%
   - High usage → Reduce `MAX_CONCURRENT_JOBS` per worker
   - Or increase resource limits in deployment YAML

### Query Examples

**Jobs processed per platform (last hour)**
```promql
sum by (platform) (increase(ghost_worker_jobs_processed_total[1h]))
```

**Average queue wait time**
```promql
redis_list_length{key="ghost:jobs:queue"} / rate(ghost_worker_jobs_processed_total[5m])
```

**Top error messages**
```promql
topk(5, sum by (error_message) (increase(ghost_worker_jobs_failed_total[1h])))
```

**Worker efficiency (jobs per second per worker)**
```promql
rate(ghost_worker_jobs_processed_total[5m]) / count(ghost_worker_active_jobs)
```

## Troubleshooting

### No Metrics Appearing

**Issue**: Grafana shows "No data"

**Solutions**:
1. Check Prometheus scraping:
   ```bash
   kubectl port-forward -n ghost svc/prometheus 9090:9090
   # Visit http://localhost:9090/targets
   # Ensure ghost-workers targets are "UP"
   ```
2. Verify worker metrics endpoint:
   ```bash
   kubectl port-forward -n ghost <worker-pod> 8080:8080
   curl http://localhost:8080/metrics
   ```
3. Check Prometheus logs:
   ```bash
   kubectl logs -n ghost deployment/prometheus
   ```

### Traces Not Showing in Jaeger

**Issue**: Jaeger UI shows no services

**Solutions**:
1. Check Jaeger collector:
   ```bash
   kubectl logs -n ghost deployment/jaeger
   ```
2. Verify OpenTelemetry configuration in Ghost workers
3. Check if traces are being sent:
   ```bash
   kubectl logs -n ghost -l component=worker | grep "trace"
   ```

### High Cardinality Warning

**Issue**: Prometheus warns about high cardinality

**Solutions**:
- Limit label values (e.g., don't use job IDs as labels)
- Use recording rules to aggregate metrics
- Reduce metric retention (`--storage.tsdb.retention.time=15d`)

### Grafana Dashboard Not Loading

**Issue**: Dashboard shows errors or doesn't load

**Solutions**:
1. Check Grafana logs:
   ```bash
   kubectl logs -n ghost deployment/grafana
   ```
2. Verify datasource configuration:
   - Grafana → Configuration → Data Sources
   - Test Prometheus connection
3. Re-import dashboard JSON

## Storage and Retention

### Prometheus Storage

Default configuration:
- **Retention**: 30 days
- **Storage**: 20GB (emptyDir)
- **Scrape interval**: 15 seconds

For production with longer retention, use PersistentVolumes:

```yaml
volumes:
- name: data
  persistentVolumeClaim:
    claimName: prometheus-data
```

### Jaeger Storage

Default: Badger (local embedded database)
- **Retention**: ~7 days (depends on disk)
- **Storage**: 10GB (emptyDir)

For production, consider:
- Elasticsearch backend (longer retention)
- Cassandra backend (high scale)

## Cost Analysis

### Self-Hosted (Ghost Stack)

Per month with 3-node cluster:

| Component | Memory | CPU | Storage | Cost |
|-----------|--------|-----|---------|------|
| Prometheus | 2GB | 1 core | 20GB | $0 (shared node) |
| Grafana | 512MB | 0.5 core | 5GB | $0 (shared node) |
| Jaeger | 1GB | 0.5 core | 10GB | $0 (shared node) |
| Redis Exporter | 128MB | 0.2 core | - | $0 (shared node) |
| **Total** | **~4GB** | **2.2 cores** | **35GB** | **$0 extra** |

All observability runs on existing worker nodes (no additional VMs needed).

### Commercial Services

| Service | Cost/Month |
|---------|------------|
| Datadog APM (100 hosts) | $1,500+ |
| New Relic Pro (100 hosts) | $1,200+ |
| Grafana Cloud (Pro) | $300+ |
| Honeycomb (Team) | $500+ |

**Ghost self-hosted saves $1,200-1,500/month** vs. commercial APM.

## Security

### Default Credentials

**Change these in production!**

- Grafana: admin / admin
- Prometheus: No authentication (use network policies)
- Jaeger: No authentication (use network policies)

### Securing Services

1. **Enable authentication**:
   ```yaml
   # Grafana
   env:
   - name: GF_SECURITY_ADMIN_PASSWORD
     valueFrom:
       secretKeyRef:
         name: grafana-secrets
         key: admin-password
   ```

2. **Use NetworkPolicies** to restrict access:
   ```yaml
   apiVersion: networking.k8s.io/v1
   kind: NetworkPolicy
   metadata:
     name: grafana-ingress
   spec:
     podSelector:
       matchLabels:
         app: grafana
     ingress:
     - from:
       - podSelector:
           matchLabels:
             app: ingress-controller
   ```

3. **Enable TLS** with cert-manager + Let's Encrypt

## Advanced Configuration

### Recording Rules

Pre-aggregate expensive queries in Prometheus:

```yaml
# prometheus-config ConfigMap
rule_files:
- '/etc/prometheus/rules/*.yml'

# rules/ghost.yml
groups:
- name: ghost_rules
  interval: 30s
  rules:
  - record: job:ghost_worker_job_rate:5m
    expr: rate(ghost_worker_jobs_processed_total[5m])
  
  - record: job:ghost_worker_error_rate:5m
    expr: |
      rate(ghost_worker_jobs_failed_total[5m])
      / rate(ghost_worker_jobs_processed_total[5m])
```

### Custom Exporters

Add platform-specific metrics:

```csharp
// In Ghost.Worker
using Prometheus;

var jobsProcessed = Metrics.CreateCounter(
    "ghost_worker_jobs_processed_total",
    "Total number of jobs processed",
    new CounterConfiguration { LabelNames = new[] { "platform", "status" } }
);

jobsProcessed.WithLabels("linkedin", "success").Inc();
```

### Log Aggregation

For logs, add Loki (not included by default):

```bash
kubectl apply -f https://github.com/grafana/loki/releases/download/v3.2.1/loki-stack.yaml
```

Then query logs in Grafana with LogQL.

## Next Steps

1. Deploy observability stack: `kubectl apply -f observability/`
2. Access Grafana and import dashboards
3. Monitor job processing and scaling
4. Set up alerts for critical issues
5. Tune retention and resource limits based on your needs

For questions or issues, see main project documentation.
