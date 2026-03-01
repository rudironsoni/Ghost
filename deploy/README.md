# Ghost Canary Deployment Configuration

This directory contains Docker Compose configuration for canary deployments of the Ghost job scraper platform.

## Files

- **docker-compose.canary.yml**: Main Docker Compose configuration for canary deployment
- **nginx-canary.conf**: Nginx configuration for traffic splitting (to be created)
- **README.md**: This file

## Quick Start

### Prerequisites
- Docker and Docker Compose installed
- Ghost Scraper images built:
  - `ghost-scraper:stable`
  - `ghost-scraper:canary`

### Setup

1. **Create logs directory** (for log volume):
```bash
mkdir -p logs
```

2. **Create nginx configuration** (`nginx-canary.conf`):
```nginx
upstream stable {
    server ghost-stable:5000;
}

upstream canary {
    server ghost-canary:5001;
}

server {
    listen 80;
    server_name _;
    
    location /health {
        access_log off;
        return 200 "healthy\n";
        add_header Content-Type text/plain;
    }
    
    location / {
        # 90% to stable, 10% to canary
        # Use split_clients for traffic distribution
        split_clients $request_uri $backend {
            90% "stable";
            * "canary";
        }
        
        proxy_pass http://$backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Add canary header for canary traffic
        add_header X-Backend $backend;
    }
}
```

3. **Build Docker images**:
```bash
# Build stable image (production)
docker build -t ghost-scraper:stable -f Dockerfile.stable .

# Build canary image (pre-release)
docker build -t ghost-scraper:canary -f Dockerfile.canary .
```

4. **Start the deployment**:
```bash
docker-compose -f docker-compose.canary.yml up -d
```

5. **Verify services are healthy**:
```bash
docker-compose -f docker-compose.canary.yml ps
```

Expected output:
```
NAME              COMMAND                  SERVICE   STATUS      PORTS
ghost-canary      "node app.js"            ghost-canary     Up (healthy)   5001/tcp
ghost-nginx       "nginx -g daemon off;"   nginx     Up (healthy)   0.0.0.0:80->80/tcp, 0.0.0.0:443->443/tcp
ghost-stable      "node app.js"            ghost-stable     Up (healthy)   5000/tcp
```

## Configuration Details

### Services

#### ghost-stable
- **Image**: `ghost-scraper:stable`
- **Port**: 5000 (internal), mapped to 5000 (external)
- **Traffic**: 90% of incoming requests
- **Resources**: 2 CPU cores max, 1GB RAM max
- **Health Check**: `/health` endpoint every 30 seconds

#### ghost-canary
- **Image**: `ghost-scraper:canary`
- **Port**: 5001 (internal), mapped to 5001 (external)
- **Traffic**: 10% of incoming requests
- **Resources**: 1.5 CPU cores max, 768MB RAM max
- **Health Check**: `/health` endpoint every 30 seconds
- **Environment**: Includes `CANARY_VERSION=true` flag

#### nginx
- **Image**: `nginx:alpine`
- **Ports**: 80 (HTTP), 443 (HTTPS)
- **Role**: Reverse proxy with traffic splitting
- **Config**: Mounted from `nginx-canary.conf` (read-only)
- **Health Check**: `/health` endpoint every 30 seconds
- **Dependencies**: Requires both ghost services to be healthy

### Volumes

- **logs**: Shared volume for logs from all services
  - Mounted at `/var/log/ghost` in ghost services
  - Mounted at `/var/log/nginx` in nginx
  - Persists logs on host system

### Network

- **ghost-network**: Custom bridge network for inter-service communication
  - Subnet: 172.25.0.0/16
  - Allows services to communicate via container names (e.g., http://ghost-stable:5000)

## Environment Variables

### Common (both services)
```
ENVIRONMENT=production
PORT=5000 (or 5001 for canary)
LOG_LEVEL=info
NODE_ENV=production
MAX_WORKERS=4
REQUEST_TIMEOUT=30000
RETRY_ATTEMPTS=3
CACHE_TTL=3600
```

### Canary-specific
```
CANARY_VERSION=true
VERSION_TAG=canary
```

## Management Commands

### View logs
```bash
# All services
docker-compose -f docker-compose.canary.yml logs -f

# Specific service
docker-compose -f docker-compose.canary.yml logs -f ghost-stable
docker-compose -f docker-compose.canary.yml logs -f ghost-canary
docker-compose -f docker-compose.canary.yml logs -f nginx
```

### Stop deployment
```bash
docker-compose -f docker-compose.canary.yml stop
```

### Remove deployment
```bash
docker-compose -f docker-compose.canary.yml down
```

### Check service health
```bash
curl http://localhost/health
curl http://localhost:5000/health
curl http://localhost:5001/health
```

## Traffic Distribution

The nginx configuration uses `split_clients` to distribute traffic:
- **90% (243 out of 270 requests)**: Routed to `ghost-stable:5000`
- **10% (27 out of 270 requests)**: Routed to `ghost-canary:5001`

To adjust the ratio, modify `nginx-canary.conf`:
```nginx
split_clients $request_uri $backend {
    90% "stable";  # Change this percentage
    * "canary";    # Remaining traffic
}
```

## Monitoring Canary Deployment

### Metrics to monitor

1. **Error rates**: Compare error rates between stable and canary
2. **Response times**: Track latency differences
3. **Resource usage**: Monitor CPU and memory consumption
4. **Health checks**: Verify all services remain healthy
5. **Traffic distribution**: Confirm 90/10 split is being respected

### Log analysis

```bash
# Count requests by backend
docker-compose -f docker-compose.canary.yml logs nginx | grep "X-Backend" | sort | uniq -c

# View stable service errors
docker-compose -f docker-compose.canary.yml logs ghost-stable | grep ERROR

# View canary service errors
docker-compose -f docker-compose.canary.yml logs ghost-canary | grep ERROR
```

## Promoting Canary to Stable

Once the canary version is proven stable:

1. **Update the stable image**:
```bash
docker build -t ghost-scraper:stable -f Dockerfile.stable .
```

2. **Rebuild the deployment**:
```bash
docker-compose -f docker-compose.canary.yml up -d
```

3. **Monitor the rollout**:
```bash
docker-compose -f docker-compose.canary.yml ps
```

4. **Adjust traffic ratio** to 95/5, then 99/1 before full cutover if desired

## Troubleshooting

### Services not becoming healthy
- Check logs: `docker-compose logs -f`
- Verify health check endpoint is responding: `curl localhost:5000/health`
- Check resource limits aren't exceeded: `docker stats`

### Nginx connection refused
- Ensure ghost services are running: `docker-compose ps`
- Check if services are bound to correct ports: `docker port ghost-stable`
- Verify network connectivity: `docker network inspect ghost-network`

### High memory usage
- Check resource limits in `docker-compose.canary.yml`
- Reduce MAX_WORKERS in environment variables
- Check for memory leaks in application logs

### Traffic not splitting 90/10
- Verify `nginx-canary.conf` syntax: `docker exec ghost-nginx nginx -t`
- Reload nginx: `docker-compose exec nginx nginx -s reload`
- Check nginx logs for errors: `docker-compose logs nginx`

## Production Considerations

1. **SSL/TLS**: Set up certificates in `./certs` directory and configure in `nginx-canary.conf`
2. **Backup**: Regularly backup the `logs` directory
3. **Monitoring**: Integrate with monitoring tools (Prometheus, Datadog, etc.)
4. **Auto-scaling**: Consider Docker Swarm or Kubernetes for larger deployments
5. **CI/CD**: Integrate with CI/CD pipeline for automated deployments

## See Also

- Docker Compose documentation: https://docs.docker.com/compose/
- Nginx documentation: https://nginx.org/en/docs/
- Docker best practices: https://docs.docker.com/develop/dev-best-practices/
