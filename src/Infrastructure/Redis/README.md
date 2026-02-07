# Ghost Redis Queue

Self-hosted Redis queue for distributed job processing.

## Quick Start

```bash
# Start Redis
./start-redis.sh

# Stop Redis
./stop-redis.sh
```

## Configuration

Redis is configured for:
- **Port**: 6379
- **Persistence**: AOF (append-only file)
- **Max Memory**: 2GB
- **Eviction**: allkeys-lru

## Queue Structure

```
ghost:jobs:pending:{priority}  - Sorted set (score = timestamp)
ghost:jobs:active:{worker_id}  - Hash (job_id → job_data)
ghost:jobs:completed           - List (trimmed to 10k)
ghost:jobs:failed:{job_id}     - Hash (retry metadata)
ghost:jobs:dead                - List (exhausted retries)
```

## Priority Levels

- **P0 (0)**: Critical (platform health checks)
- **P1 (1)**: High (user-initiated searches)
- **P2 (2)**: Normal (scheduled jobs)
- **P3 (3)**: Low (background tasks)

## Retry Strategy

- Max retries: 3
- Backoff: 2^attempt minutes (2, 4, 8 minutes)
- After exhaustion: Move to dead letter queue

## Monitoring

```bash
# Check Redis status
docker exec ghost-redis redis-cli ping

# Monitor queue stats
docker exec ghost-redis redis-cli --scan --pattern "ghost:jobs:*"

# Get pending count
docker exec ghost-redis redis-cli ZCARD ghost:jobs:pending:2

# Get all keys
docker exec ghost-redis redis-cli KEYS "ghost:jobs:*"
```

## Data Persistence

Redis data is persisted to Docker volume `redis-data`. Data survives container restarts.

To clear all data:
```bash
docker compose down -v
```

## Production Considerations

For production deployments:
1. **Redis Sentinel**: High availability (HA)
2. **Redis Cluster**: Horizontal scaling (6+ nodes)
3. **Monitoring**: Prometheus + Grafana
4. **Backup**: Scheduled RDB snapshots
5. **TLS**: Encrypt connections
6. **Authentication**: Require password (requirepass)

## Scaling

Current setup: Single Redis instance

For 100K concurrent jobs:
- Phase 2: 3-node Sentinel (HA)
- Phase 3: 6-node Cluster (sharding)
- Phase 4: 12-node Cluster (100K target)
