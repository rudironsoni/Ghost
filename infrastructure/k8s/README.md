# Ghost Kubernetes (k3s) Deployment Guide

This directory contains all necessary files to deploy Ghost's distributed scraping architecture on a k3s Kubernetes cluster, capable of scaling from 3 to 100+ concurrent workers.

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                  k3s Master Node                     │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────┐ │
│  │ API Server   │  │   etcd       │  │ Scheduler │ │
│  └──────────────┘  └──────────────┘  └───────────┘ │
└─────────────────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
┌───────▼──────┐  ┌──────▼──────┐  ┌────▼────────┐
│ Worker Node 1│  │Worker Node 2│  │Worker Node N│
│              │  │             │  │   (up to 100)│
│ Ghost Worker │  │Ghost Worker │  │Ghost Worker │
│ Pods (3-5)   │  │Pods (3-5)   │  │Pods (3-5)   │
└──────────────┘  └─────────────┘  └─────────────┘
        │                │                │
        └────────────────┼────────────────┘
                         │
                  ┌──────▼──────┐
                  │Redis Queue  │
                  │(Job Queue)  │
                  └─────────────┘
```

## Quick Start (3-Node Cluster)

### Prerequisites

- 3+ servers (VMs or bare metal) running Ubuntu 20.04+ or similar
- Each server: 2+ CPU cores, 4GB+ RAM, 20GB+ disk
- Root access (or sudo privileges)
- IPv4 networking between nodes
- Optional: IPv6 /64 subnet for proxy rotation (see IPv6 setup)

### Step 1: Install Master Node

On your designated master node:

```bash
cd infrastructure/k8s
sudo ./k3s-master-install.sh
```

**Save the output!** You'll need the `NODE_TOKEN` and `MASTER_IP` for worker nodes.

Example output:
```
Master Node IP: 192.168.1.10
Node Token: K10abc123...xyz::server:abc123def456
```

### Step 2: Join Worker Nodes

On each worker node, export the token and URL from Step 1:

```bash
export K3S_TOKEN='K10abc123...xyz::server:abc123def456'
export K3S_URL='https://192.168.1.10:6443'
sudo ./k3s-worker-install.sh
```

Verify on master node:
```bash
kubectl get nodes
```

Expected output (3-node cluster):
```
NAME       STATUS   ROLES                  AGE   VERSION
master     Ready    control-plane,master   5m    v1.29.1+k3s2
worker-1   Ready    <none>                 2m    v1.29.1+k3s2
worker-2   Ready    <none>                 1m    v1.29.1+k3s2
```

### Step 3: Deploy Ghost Workers

**Configure secrets first:**

Edit `ghost-worker-deployment.yaml` and set a strong Redis password:
```yaml
stringData:
  REDIS_PASSWORD: "YOUR_SECURE_PASSWORD_HERE"
```

Deploy:
```bash
kubectl apply -f ghost-worker-deployment.yaml
```

Verify deployment:
```bash
kubectl get pods -n ghost
kubectl logs -n ghost -l app=ghost,component=worker --tail=50
```

### Step 4: Setup Auto-Scaling (Optional)

Deploy HorizontalPodAutoscaler:
```bash
kubectl apply -f hpa.yaml
```

This will automatically scale Ghost workers based on Redis queue depth (3-100 replicas).

## Configuration

### Worker Configuration

Edit `ghost-worker-deployment.yaml` ConfigMap to adjust:

- `MAX_CONCURRENT_SESSIONS`: Browser sessions per worker pod (default: 5)
- `SESSION_TIMEOUT_SECONDS`: Max session duration (default: 300)
- `LOG_LEVEL`: Logging verbosity (Debug, Information, Warning, Error)

### Resource Limits

Each worker pod is configured with:
- **Requests**: 500m CPU, 512Mi RAM
- **Limits**: 2 CPU cores, 2Gi RAM
- **Browser cache**: 5Gi ephemeral storage
- **Temp storage**: 1Gi ephemeral storage

Adjust `resources` section based on your workload.

### Redis Configuration

Single-instance Redis is included for simplicity. For production, consider:
- Redis Sentinel for HA
- Redis Cluster for horizontal scaling
- Persistent volumes for queue durability

Edit `redis` deployment in `ghost-worker-deployment.yaml` or deploy separate Redis infrastructure.

## Scaling Operations

### Manual Scaling

Scale workers manually:
```bash
kubectl scale deployment ghost-worker -n ghost --replicas=10
```

### Auto-Scaling

HPA automatically scales based on custom metrics (Redis queue depth):
- **Target**: 100 jobs per worker
- **Min replicas**: 3
- **Max replicas**: 100
- **Scale-up**: When queue > 100 jobs/worker
- **Scale-down**: When queue < 50 jobs/worker (5-minute cooldown)

Monitor scaling:
```bash
kubectl get hpa -n ghost
kubectl describe hpa ghost-worker-hpa -n ghost
```

### Adding More Nodes

To scale beyond initial worker nodes, repeat Step 2 on new servers. k3s automatically distributes pods across available nodes.

## Monitoring

### Health Checks

Check worker health:
```bash
kubectl get pods -n ghost
kubectl describe pod <pod-name> -n ghost
```

### Logs

Stream worker logs:
```bash
# All workers
kubectl logs -n ghost -l component=worker -f --tail=100

# Specific worker
kubectl logs -n ghost <pod-name> -f
```

### Metrics

Workers expose Prometheus metrics on port 8080:
```bash
kubectl port-forward -n ghost svc/ghost-worker-service 8080:8080
curl http://localhost:8080/metrics
```

For full observability stack (Prometheus, Grafana, Jaeger), see Task 4.5.

## IPv6 Proxy Rotation Setup

To leverage IPv6 proxy rotation (Task 4.2):

1. Ensure your VPS provider allocates a /64 IPv6 subnet
2. Configure IPv6 on all nodes:
   ```bash
   # Enable IPv6 forwarding
   echo "net.ipv6.conf.all.forwarding=1" >> /etc/sysctl.conf
   sysctl -p
   ```
3. Mount IPv6 subnet into worker pods (see IPv6Rotator documentation)
4. Workers will automatically rotate through millions of IPv6 addresses

Cost: ~$5/month per VPS with IPv6 vs. $500/month for commercial proxy services.

## Troubleshooting

### Workers Not Starting

Check pod events:
```bash
kubectl describe pod <pod-name> -n ghost
```

Common issues:
- Image not found: Build and push Docker image first (Task 4.3)
- Redis connection failed: Check Redis password in secrets
- Insufficient resources: Adjust resource requests/limits

### Workers Crashing (CrashLoopBackOff)

Check logs:
```bash
kubectl logs -n ghost <pod-name> --previous
```

Common causes:
- Missing environment variables
- Redis authentication failure
- Playwright browser installation issues

### Node Not Joining Cluster

Verify network connectivity between master and worker:
```bash
# On worker
ping <master-ip>
curl -k https://<master-ip>:6443
```

Check k3s agent logs:
```bash
sudo journalctl -u k3s-agent -f
```

### Performance Tuning

For high-concurrency workloads (50+ workers):

1. **Increase kernel limits** on all nodes:
   ```bash
   # /etc/sysctl.conf
   fs.file-max = 2097152
   fs.inotify.max_user_watches = 524288
   net.core.somaxconn = 65535
   net.ipv4.ip_local_port_range = 1024 65535
   ```

2. **Optimize Playwright**: Set `PLAYWRIGHT_BROWSERS_PATH` to shared volume
3. **Redis tuning**: Increase maxmemory based on queue size
4. **Node resources**: Ensure adequate CPU/RAM for target scale

## Uninstallation

### Remove Ghost Deployment

```bash
kubectl delete namespace ghost
```

### Uninstall k3s

Master node:
```bash
sudo /usr/local/bin/k3s-uninstall.sh
```

Worker nodes:
```bash
sudo /usr/local/bin/k3s-agent-uninstall.sh
```

## Cost Analysis

**3-Node Cluster (10 workers)**:
- 3x VPS: $15-30/month (Hetzner, DigitalOcean, Linode)
- IPv6 proxy: $0 (included with VPS /64)
- **Total**: ~$20-30/month

**100-Worker Cluster** (10 nodes, 10 workers each):
- 10x VPS: $50-100/month
- IPv6 proxy: $0
- **Total**: ~$60-100/month

Compare to:
- AWS Fargate (100 tasks): $1,000+/month
- Commercial proxies: $500-2,000/month
- Managed k8s: $200+/month

**Ghost achieves 10-20x cost savings** with self-hosted k3s + IPv6.

## Next Steps

1. **Task 4.2**: Implement IPv6 proxy rotation
2. **Task 4.3**: Build Ghost.Worker Docker image
3. **Task 4.4**: Deploy auto-scaling HPA
4. **Task 4.5**: Setup observability stack (OpenTelemetry, Prometheus, Grafana, Jaeger)

For detailed implementation, see respective task documentation.
