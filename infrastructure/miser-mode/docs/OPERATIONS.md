# Ghost Platform - Operations Runbook

Day-to-day operations guide for Ultra Miser Mode infrastructure.

## Daily Operations

### Health Check

```bash
# Quick health check
./scripts/health-check.sh --quick

# Full health check
./scripts/health-check.sh --full

# Watch mode (continuous monitoring)
./scripts/health-check.sh --watch
```

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f ghost-webapi

# Last 100 lines
docker-compose logs --tail=100 ghost-webapi
```

### Check Resource Usage

```bash
# Docker stats
docker stats

# System resources
htop

# Disk usage
df -h
docker system df
```

## Weekly Operations

### Backup Verification

```bash
# List backups
ls -la backups/archives/

# Test restore (dry-run)
./scripts/restore.sh --dry-run backups/archives/latest.tar.gz

# Check backup integrity
tar -tzf backups/archives/latest.tar.gz > /dev/null && echo "Backup OK"
```

### Log Rotation

Logs are automatically rotated. To manually rotate:

```bash
# Rotate Docker logs
docker run --rm -v /var/lib/docker/containers:/var/lib/docker/containers logrotate

# Clean old logs
find logs/ -name "*.log" -mtime +7 -delete
```

### Update Containers

```bash
# Check for updates
docker-compose pull --dry-run

# Apply updates
docker-compose pull
docker-compose up -d

# Verify
docker-compose ps
```

## Monthly Operations

### Certificate Renewal

If using Let's Encrypt:

```bash
# Renew certificates
docker-compose run --rm certbot renew

# Reload nginx
docker-compose exec nginx nginx -s reload
```

### Database Maintenance

```bash
# Connect to PostgreSQL
docker exec -it ghost-postgres psql -U ghost

# Vacuum and analyze
VACUUM ANALYZE;

# Check table sizes
\dt+

# Exit
\q
```

### Security Updates

```bash
# Update system packages
apt update && apt upgrade -y

# Update Docker images
docker-compose pull
docker-compose up -d

# Reboot if needed
reboot
```

## Troubleshooting

### High CPU Usage

1. Identify process:
   ```bash
   docker stats --no-stream
   top
   ```

2. Check logs:
   ```bash
   docker-compose logs <service>
   ```

3. Restart service:
   ```bash
   docker-compose restart <service>
   ```

### High Memory Usage

1. Check memory:
   ```bash
   free -h
   docker system df
   ```

2. Prune if needed:
   ```bash
   docker system prune -a
   ```

3. Adjust limits in `docker-compose.yml`

### Disk Full

1. Check usage:
   ```bash
   df -h
   du -sh /var/lib/docker/*
   ```

2. Clean up:
   ```bash
   # Remove old backups
   find backups/archives/ -mtime +30 -delete
   
   # Prune Docker
   docker system prune -a --volumes
   
   # Clean logs
   find logs/ -name "*.log" -size +100M -delete
   ```

### Service Down

1. Check status:
   ```bash
   docker-compose ps
   ```

2. View logs:
   ```bash
   docker-compose logs <service>
   ```

3. Restart:
   ```bash
   docker-compose restart <service>
   ```

4. If persistent:
   ```bash
   docker-compose down
   docker-compose up -d
   ```

## Emergency Procedures

### Complete System Recovery

```bash
# 1. Stop all services
docker-compose down

# 2. Restore from backup
./scripts/restore.sh backups/archives/backup.tar.gz

# 3. Start services
docker-compose up -d

# 4. Verify
./scripts/health-check.sh --full
```

### Database Corruption

```bash
# 1. Stop application
docker-compose stop ghost-webapi

# 2. Restore database only
./scripts/restore.sh --database-only backup.tar.gz

# 3. Start application
docker-compose start ghost-webapi
```

### Network Issues

```bash
# Reset Docker network
docker-compose down
docker network prune -f
docker-compose up -d
```

## Monitoring

### Prometheus Queries

```promql
# CPU usage
100 - (avg(irate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory usage
100 * (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes))

# Disk usage
100 * (1 - (node_filesystem_avail_bytes / node_filesystem_size_bytes))

# Request rate
rate(http_requests_total[5m])

# Error rate
rate(http_requests_total{status=~"5.."}[5m])
```

### Grafana Dashboards

Access at http://localhost:3000

- **Infrastructure Overview**: System metrics
- **Application Performance**: API metrics
- **Database Metrics**: PostgreSQL stats
- **Cache Performance**: Redis stats
- **Message Queue**: RabbitMQ stats
- **Business Metrics**: Jobs and platforms

## Maintenance Windows

Schedule maintenance during low-traffic periods:

1. **Weekly**: Sunday 2-4 AM
2. **Monthly**: First Sunday 2-6 AM
3. **Quarterly**: Planned downtime with 1 week notice

## Escalation

1. **Level 1**: Check logs, restart service
2. **Level 2**: Restore from backup
3. **Level 3**: Contact team lead
4. **Level 4**: Full disaster recovery