# Ghost Worker - Build and Deployment Guide

## Overview

Ghost Worker is a distributed background service that pulls scraping jobs from Redis queues and executes them using the Ghost browser automation kernel. It's designed to run at scale in Kubernetes, handling 100+ concurrent workers across multiple nodes.

## Architecture

```
┌──────────────┐         ┌──────────────┐
│  Web API     │────────▶│ Redis Queue  │
│  (Enqueues)  │         │ (ghost:jobs) │
└──────────────┘         └──────┬───────┘
                                │
                 ┌──────────────┼──────────────┐
                 │              │              │
          ┌──────▼─────┐  ┌─────▼──────┐  ┌───▼────────┐
          │ Worker 1   │  │ Worker 2   │  │ Worker N   │
          │ (5 jobs)   │  │ (5 jobs)   │  │ (5 jobs)   │
          └────────────┘  └────────────┘  └────────────┘
                 │              │              │
          ┌──────▼──────────────▼──────────────▼──────┐
          │         Redis (Results Storage)           │
          │   job:results:{id}, job:status:{id}       │
          └───────────────────────────────────────────┘
```

## Local Development

### Prerequisites

- .NET 9 SDK
- Docker (optional, for containerization)
- Redis server (local or remote)

### Build

```bash
cd src/Ghost.Worker
dotnet build
```

### Run Locally

```bash
# Start Redis (if not running)
docker run -d -p 6379:6379 redis:7-alpine

# Set environment variables
export REDIS_HOST=localhost
export REDIS_PORT=6379
export MAX_CONCURRENT_JOBS=5

# Run worker
dotnet run
```

### Test with Manual Job Enqueue

```bash
# Connect to Redis CLI
redis-cli

# Enqueue a test job
LPUSH ghost:jobs:queue '{"JobId":"test-001","Platform":"linkedin","SearchQuery":"software engineer","SearchOptions":{"Location":"Remote","MaxResults":10}}'

# Check job status
GET job:status:test-001

# Get results
GET job:results:test-001
```

## Docker Build

### Build Image

```bash
# From repository root
docker build -f src/Ghost.Worker/Dockerfile -t ghost-worker:latest .
```

### Run Container

```bash
docker run -d \
  --name ghost-worker \
  -e REDIS_HOST=redis-server \
  -e REDIS_PORT=6379 \
  -e MAX_CONCURRENT_JOBS=5 \
  ghost-worker:latest
```

### Docker Compose (Development)

```yaml
version: '3.8'
services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
  
  ghost-worker:
    build:
      context: .
      dockerfile: src/Ghost.Worker/Dockerfile
    environment:
      REDIS_HOST: redis
      REDIS_PORT: 6379
      MAX_CONCURRENT_JOBS: 5
    depends_on:
      - redis
```

## Kubernetes Deployment

Workers are deployed to Kubernetes using the manifests in `infrastructure/k8s/`.

### Deploy

```bash
# Apply deployment (includes Redis and workers)
kubectl apply -f infrastructure/k8s/ghost-worker-deployment.yaml

# Check deployment status
kubectl get pods -n ghost
kubectl logs -n ghost -l component=worker --tail=100 -f
```

### Environment Variables

Configure workers via ConfigMap (`ghost-worker-config`):

| Variable                       | Description                      | Default          |
|--------------------------------|----------------------------------|------------------|
| `REDIS_HOST`                   | Redis server hostname            | `localhost`      |
| `REDIS_PORT`                   | Redis server port                | `6379`           |
| `REDIS_PASSWORD`               | Redis password (from secret)     | -                |
| `REDIS_QUEUE_KEY`              | Queue key name                   | `ghost:jobs:queue` |
| `MAX_CONCURRENT_JOBS`          | Max parallel jobs per worker     | `5`              |
| `POLL_INTERVAL_MS`             | Queue poll interval (ms)         | `1000`           |
| `RESULTS_EXPIRATION_HOURS`     | Result retention time            | `24`             |
| `WORKER_ID`                    | Worker identifier                | Pod name         |
| `NODE_NAME`                    | Kubernetes node name             | Node name        |

### Scaling

Scale workers manually:

```bash
kubectl scale deployment ghost-worker -n ghost --replicas=10
```

Or enable auto-scaling with HPA (see Task 4.4):

```bash
kubectl apply -f infrastructure/k8s/hpa.yaml
```

## Configuration

### Worker Configuration

Edit `WorkerConfiguration` class or set via environment variables:

```csharp
var config = new WorkerConfiguration
{
    WorkerId = "worker-001",
    NodeName = "node-1",
    RedisConnectionString = "localhost:6379,password=secret",
    RedisQueueKey = "ghost:jobs:queue",
    MaxConcurrentJobs = 5,
    PollIntervalMs = 1000,
    ResultsExpirationHours = 24
};
```

### Concurrency Control

Each worker processes multiple jobs concurrently using a `SemaphoreSlim`:

- **MaxConcurrentJobs = 5**: Worker handles 5 jobs simultaneously
- **10 workers × 5 jobs = 50 concurrent jobs**
- **100 workers × 5 jobs = 500 concurrent jobs**

Adjust based on:
- Available CPU/memory per pod
- Playwright browser resource usage (~200-500MB per session)
- Network bandwidth

### Redis Queue Pattern

Workers use Redis `RPOP` (right pop) to pull jobs from the queue:

```
Queue: ghost:jobs:queue
[job1, job2, job3, job4, ...] → Worker pops from right
```

Jobs are JSON-serialized `JobRequest` objects:

```json
{
  "JobId": "uuid-here",
  "Platform": "linkedin",
  "SearchQuery": "software engineer",
  "SearchOptions": {
    "Location": "Remote",
    "MaxResults": 50
  }
}
```

### Results Storage

Results are stored in Redis with expiration:

- **Key**: `job:results:{jobId}`
- **Value**: JSON array of `JobPosting` objects
- **TTL**: 24 hours (configurable)

Status tracking:

- **Key**: `job:status:{jobId}`
- **Value**: JSON object with status, timestamp, error message
- **TTL**: 24 hours

## Monitoring

### Health Checks

Workers include liveness/readiness probes (Kubernetes):

- **Liveness**: Process running check
- **Readiness**: Can accept jobs (queue connectivity)

### Metrics

Workers expose Prometheus metrics (port 8080):

- `ghost_worker_jobs_processed_total` - Total jobs processed
- `ghost_worker_jobs_failed_total` - Total failed jobs
- `ghost_worker_active_jobs` - Current active jobs
- `ghost_worker_queue_depth` - Redis queue depth

Access metrics:

```bash
kubectl port-forward -n ghost svc/ghost-worker-service 8080:8080
curl http://localhost:8080/metrics
```

### Logging

Workers use structured logging (JSON format in production):

```json
{
  "timestamp": "2026-02-08T07:00:00Z",
  "level": "Information",
  "message": "Completed job test-001 in 5432ms, found 25 results",
  "workerId": "worker-001",
  "jobId": "test-001"
}
```

View logs:

```bash
# All workers
kubectl logs -n ghost -l component=worker --tail=100 -f

# Specific worker
kubectl logs -n ghost ghost-worker-abc123-xyz --tail=100 -f
```

## Troubleshooting

### Worker Not Starting

**Symptom**: Pods in `CrashLoopBackOff`

**Solutions**:
1. Check logs: `kubectl logs -n ghost <pod-name> --previous`
2. Verify Redis connectivity: `telnet redis-service 6379`
3. Check environment variables: `kubectl describe pod <pod-name> -n ghost`
4. Ensure Playwright browsers installed (image build issue)

### Jobs Not Processing

**Symptom**: Jobs enqueued but not picked up

**Solutions**:
1. Check queue depth: `redis-cli LLEN ghost:jobs:queue`
2. Verify workers running: `kubectl get pods -n ghost`
3. Check worker logs for errors
4. Verify Redis connection string in ConfigMap

### High Memory Usage

**Symptom**: Workers OOMKilled

**Solutions**:
1. Reduce `MAX_CONCURRENT_JOBS` (default 5 → 3)
2. Increase memory limits in deployment YAML
3. Profile browser sessions for memory leaks
4. Enable browser cleanup after each job

### Slow Job Processing

**Symptom**: Jobs take longer than expected

**Solutions**:
1. Check node CPU/memory availability
2. Review target site response times
3. Enable IPv6 proxy rotation to avoid rate limiting
4. Add behavioral delays for stealth

## Performance Tuning

### Optimal Concurrency

Test different values for `MAX_CONCURRENT_JOBS`:

| Concurrent Jobs | Memory per Pod | CPU per Pod | Throughput |
|----------------|----------------|-------------|------------|
| 3              | ~1.5 GB        | ~1.5 cores  | Baseline   |
| 5              | ~2.5 GB        | ~2.5 cores  | +50%       |
| 10             | ~5 GB          | ~4 cores    | +100%      |

### Resource Requests/Limits

Configure pod resources in deployment YAML:

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "500m"
  limits:
    memory: "2Gi"
    cpu: "2000m"
```

### Redis Optimization

For high throughput (100+ workers):

1. **Use Redis Cluster** for horizontal scaling
2. **Enable persistence** (AOF or RDB) for durability
3. **Tune maxmemory** based on queue size
4. **Use pipelining** for batch operations

## Security

### Secrets Management

Use Kubernetes Secrets for sensitive data:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ghost-worker-secrets
type: Opaque
stringData:
  REDIS_PASSWORD: "your-secure-password"
```

### Network Policies

Restrict worker network access:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: ghost-worker-policy
spec:
  podSelector:
    matchLabels:
      component: worker
  ingress:
  - from:
    - podSelector:
        matchLabels:
          component: metrics
  egress:
  - to:
    - podSelector:
        matchLabels:
          component: redis
  - to: # Allow external HTTPS (for scraping)
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 443
```

### Container Security

- Run as non-root user (if Playwright supports)
- Use read-only root filesystem where possible
- Drop unnecessary Linux capabilities

## Next Steps

1. **Deploy workers**: `kubectl apply -f infrastructure/k8s/ghost-worker-deployment.yaml`
2. **Setup auto-scaling**: See Task 4.4 (HPA configuration)
3. **Enable observability**: See Task 4.5 (Prometheus, Grafana, Jaeger)
4. **Configure IPv6 rotation**: See `infrastructure/k8s/docs/ipv6-proxy-setup.md`

For issues or feature requests, open a GitHub issue or contribute a PR.
