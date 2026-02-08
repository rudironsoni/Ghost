# Auto-Scaling Setup for Ghost Workers

This directory contains configurations for automatically scaling Ghost workers based on workload (Redis queue depth, CPU, memory).

## Overview

Ghost supports two autoscaling approaches:

1. **Kubernetes HPA (Horizontal Pod Autoscaler)** - Standard k8s autoscaling
2. **KEDA (Kubernetes Event-Driven Autoscaling)** - Advanced queue-based autoscaling

**Recommendation**: Use KEDA for production deployments. It scales based on Redis queue depth, providing more accurate scaling than CPU/memory-based HPA.

## Prerequisites

### For HPA (Standard Kubernetes)

```bash
# Install Metrics Server (required for CPU/memory-based scaling)
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Verify metrics server is running
kubectl get deployment metrics-server -n kube-system
```

### For KEDA (Recommended)

```bash
# Install KEDA
kubectl apply --server-side -f https://github.com/kedacore/keda/releases/download/v2.15.0/keda-2.15.0.yaml

# Verify KEDA is running
kubectl get pods -n keda
```

## Deployment

### Option 1: KEDA (Recommended)

Deploy KEDA-based autoscaler:

```bash
cd infrastructure/k8s
kubectl apply -f keda-scaler.yaml
```

This will:
- Scale workers based on Redis queue length (`ghost:jobs:queue`)
- Target: 100 jobs per worker
- Min: 3 workers, Max: 100 workers
- Activation threshold: 10 jobs (starts scaling)

Verify KEDA scaler:

```bash
kubectl get scaledobject -n ghost
kubectl describe scaledobject ghost-worker-scaler -n ghost
```

### Option 2: Standard HPA

Deploy Kubernetes HPA:

```bash
kubectl apply -f hpa.yaml
```

This scales based on:
- CPU utilization (target: 70%)
- Memory utilization (target: 80%)
- (Optional) Custom metrics via Prometheus Adapter

Verify HPA:

```bash
kubectl get hpa -n ghost
kubectl describe hpa ghost-worker-hpa -n ghost
```

## Configuration

### KEDA Scaling Parameters

Edit `keda-scaler.yaml` to adjust:

```yaml
spec:
  minReplicaCount: 3        # Minimum workers (always running)
  maxReplicaCount: 100      # Maximum workers
  pollingInterval: 15       # Check queue every 15 seconds
  cooldownPeriod: 300       # Wait 5 min before scaling down
  triggers:
  - type: redis
    metadata:
      listLength: "100"     # Target jobs per worker
      activationListLength: "10"  # Start scaling at 10 jobs
```

**Scaling logic**:
- Queue has 500 jobs, 3 workers → Scale to 5 workers (500 / 100 = 5)
- Queue has 10,000 jobs → Scale to 100 workers (10,000 / 100 = 100, capped)
- Queue has 5 jobs → Stay at 3 workers (below activation threshold)

### HPA Scaling Parameters

Edit `hpa.yaml` to adjust:

```yaml
spec:
  minReplicas: 3
  maxReplicas: 100
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        averageUtilization: 70  # Scale when avg CPU > 70%
  - type: Resource
    resource:
      name: memory
      target:
        averageUtilization: 80  # Scale when avg memory > 80%
```

## Monitoring

### KEDA Metrics

Check current scaling status:

```bash
# Current replicas
kubectl get deployment ghost-worker -n ghost

# KEDA scaler status
kubectl get scaledobject ghost-worker-scaler -n ghost -o yaml

# KEDA logs
kubectl logs -n keda -l app=keda-operator -f
```

### HPA Metrics

```bash
# HPA status
kubectl get hpa ghost-worker-hpa -n ghost

# Detailed HPA info
kubectl describe hpa ghost-worker-hpa -n ghost

# Current metrics
kubectl top pods -n ghost -l component=worker
```

### Redis Queue Depth

Monitor the job queue:

```bash
# Connect to Redis
kubectl exec -it -n ghost deployment/redis -- redis-cli -a $(kubectl get secret ghost-worker-secrets -n ghost -o jsonpath='{.data.REDIS_PASSWORD}' | base64 -d)

# Check queue length
LLEN ghost:jobs:queue

# View pending jobs (first 10)
LRANGE ghost:jobs:queue 0 9
```

## Scaling Behavior

### Scale-Up

**Fast and aggressive** to handle load spikes:
- No stabilization window (immediate scale-up)
- Can double replicas in 60 seconds (100% increase)
- Or add 10 pods at a time

Example: Queue grows from 100 → 5,000 jobs
- Current: 3 workers
- Target: 50 workers (5,000 / 100)
- Iteration 1 (0s): 3 → 6 workers (+100%)
- Iteration 2 (60s): 6 → 12 workers (+100%)
- Iteration 3 (120s): 12 → 22 workers (+10 pods)
- Iteration 4 (180s): 22 → 32 workers (+10 pods)
- ...continues until reaching 50

### Scale-Down

**Slow and conservative** to avoid thrashing:
- 5-minute stabilization window
- Max 50% reduction per minute (or 5 pods, whichever is less)

Example: Queue drops from 5,000 → 100 jobs
- Current: 50 workers
- Target: 3 workers (100 / 100 = 1, but min is 3)
- Wait 5 minutes (ensure queue stays low)
- Iteration 1 (300s): 50 → 45 workers (-5 pods, limited by policy)
- Iteration 2 (360s): 45 → 40 workers (-5 pods)
- ...continues until reaching 3

## Troubleshooting

### Workers Not Scaling Up

**Symptom**: Queue growing but replicas stay at minReplicas

**Solutions**:
1. Check KEDA/HPA status:
   ```bash
   kubectl describe scaledobject ghost-worker-scaler -n ghost
   kubectl get events -n ghost --sort-by='.lastTimestamp'
   ```
2. Verify Redis connectivity from KEDA:
   ```bash
   kubectl logs -n keda -l app=keda-operator | grep ghost
   ```
3. Check resource quotas/limits:
   ```bash
   kubectl describe resourcequota -n ghost
   kubectl describe limitrange -n ghost
   ```
4. Verify node capacity:
   ```bash
   kubectl describe nodes | grep -A 5 "Allocated resources"
   ```

### Workers Not Scaling Down

**Symptom**: Queue empty but replicas stay high

**Solutions**:
1. Check cooldown period (default 5 minutes)
2. Verify queue is actually empty:
   ```bash
   kubectl exec -n ghost deployment/redis -- redis-cli LLEN ghost:jobs:queue
   ```
3. Check for active jobs preventing scale-down:
   ```bash
   kubectl top pods -n ghost -l component=worker
   ```

### Rapid Scaling (Thrashing)

**Symptom**: Workers constantly scaling up/down

**Solutions**:
1. Increase `listLength` target (less sensitive):
   ```yaml
   listLength: "200"  # Was 100
   ```
2. Increase cooldown period:
   ```yaml
   cooldownPeriod: 600  # 10 minutes instead of 5
   ```
3. Adjust scale-down policy (slower):
   ```yaml
   stabilizationWindowSeconds: 600  # 10 minutes
   ```

### KEDA Not Installed/Working

**Symptom**: `kubectl get scaledobject` returns error

**Solution**: Install KEDA first:
```bash
kubectl apply --server-side -f https://github.com/kedacore/keda/releases/download/v2.15.0/keda-2.15.0.yaml
kubectl wait --for=condition=ready pod -l app=keda-operator -n keda --timeout=300s
```

### Metrics Not Available (HPA)

**Symptom**: `kubectl get hpa` shows `<unknown>/70%` for CPU

**Solution**: Install Metrics Server:
```bash
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
# For k3s, may need --kubelet-insecure-tls flag
```

## Performance Tuning

### High-Throughput Workloads (1000+ jobs/sec)

```yaml
# keda-scaler.yaml
spec:
  pollingInterval: 5       # Check queue every 5 seconds (faster)
  minReplicaCount: 10      # Higher baseline
  maxReplicaCount: 200     # Allow more workers
  triggers:
  - type: redis
    metadata:
      listLength: "50"     # More aggressive (50 jobs per worker)
      activationListLength: "5"
```

### Cost-Optimized (Batch processing)

```yaml
# keda-scaler.yaml
spec:
  pollingInterval: 60      # Check less frequently
  minReplicaCount: 1       # Minimal baseline
  maxReplicaCount: 50      # Limit max workers
  cooldownPeriod: 600      # Scale down after 10 min idle
  triggers:
  - type: redis
    metadata:
      listLength: "200"    # Pack more jobs per worker
```

### Latency-Sensitive (Real-time)

```yaml
# keda-scaler.yaml
spec:
  pollingInterval: 10
  minReplicaCount: 10      # Always have workers ready
  maxReplicaCount: 100
  triggers:
  - type: redis
    metadata:
      listLength: "10"     # Very aggressive (few jobs per worker)
      activationListLength: "1"  # Start scaling at 1 job
```

## Cost Analysis

Assuming $0.03/hour per worker pod (Hetzner/DigitalOcean pricing):

| Scenario | Min Workers | Avg Workers | Max Workers | Cost/Month |
|----------|-------------|-------------|-------------|------------|
| Idle     | 3           | 3           | -           | $65        |
| Low      | 3           | 10          | 20          | $216       |
| Medium   | 3           | 30          | 50          | $648       |
| High     | 3           | 60          | 100         | $1,296     |
| Peak     | 3           | 100         | 100         | $2,160     |

Compare to:
- AWS Fargate (100 tasks): $3,000+/month
- Managed Kubernetes: $200/month cluster + compute
- Traditional VMs (100 servers): $5,000+/month

**Ghost with KEDA achieves 50-70% cost savings** by scaling down during idle periods.

## Next Steps

1. Deploy KEDA: `kubectl apply -f keda-scaler.yaml`
2. Monitor scaling: `watch kubectl get scaledobject,deployment -n ghost`
3. Load test: Enqueue 10,000 jobs and observe scaling behavior
4. Tune parameters based on your workload patterns
5. Setup alerting (Task 4.5) for scaling issues

For questions or issues, see main project documentation.
