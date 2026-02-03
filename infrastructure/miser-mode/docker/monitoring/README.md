# Ghost Platform Ultra Miser Mode - Monitoring Configuration

This directory contains comprehensive monitoring configuration for the Ghost Platform Ultra Miser Mode deployment using Prometheus, Grafana, and Alertmanager.

## Directory Structure

```
monitoring/
├── prometheus/
│   ├── prometheus.yml          # Main Prometheus configuration
│   ├── recording-rules.yml     # Recording rules for common queries
│   └── alerting-rules.yml      # Alert definitions
├── alertmanager/
│   └── alertmanager.yml        # Alert routing and notification config
└── grafana/
    └── provisioning/
        ├── datasources/
        │   └── prometheus.yml  # Prometheus datasource config
        ├── dashboards/
        │   └── dashboard.yml   # Dashboard provider config
        └── dashboards-json/
            ├── infrastructure-overview.json
            ├── application-performance.json
            ├── database-metrics.json
            ├── cache-performance.json
            ├── message-queue.json
            └── business-metrics.json
```

## Components

### Prometheus

**Scrape Targets:**
- Prometheus itself (self-monitoring)
- Node Exporter (host metrics)
- cAdvisor (container metrics)
- PostgreSQL Exporter
- Redis Exporter
- RabbitMQ Exporter
- Ghost API Service
- Ghost Scraper Service
- Ghost AI Service
- Ghost Auth Service
- Nginx Exporter
- Blackbox Exporter (endpoint health checks)

**Recording Rules:**
- Resource utilization (CPU, memory, disk)
- Application metrics (request rates, latencies, errors)
- Database metrics (connections, transactions, queries)
- Cache metrics (hit rate, operations)
- Queue metrics (message rates, consumers)
- Business metrics (jobs scraped, success rates)

**Alerting Rules:**
- Infrastructure alerts (CPU, memory, disk usage)
- Service health alerts (down services, error rates)
- Database alerts (connections, long queries)
- Cache alerts (memory, hit rate, evictions)
- Queue alerts (depth, consumers, alarms)
- Business logic alerts (circuit breakers, scrape failures)

### Grafana Dashboards

#### 1. Infrastructure Overview
- CPU, memory, and disk usage gauges
- Service health status
- Container resource usage
- Network traffic and disk I/O

#### 2. Application Performance
- Request rate per service
- Request latency percentiles (P95, P99)
- Error rates by service
- Status code distribution
- Request rate by HTTP method

#### 3. Database Metrics
- Connection usage and states
- Transaction rates
- Database operations (reads, writes)
- Cache hit ratio
- Locks and conflicts
- Long running queries

#### 4. Cache Performance
- Redis memory usage
- Cache hit rate
- Command processing rate
- Client connections
- Key evictions and expirations
- Network I/O

#### 5. Message Queue
- Queue depths
- Consumers per queue
- Message publish/consume rates
- Consumer utilization
- Message states (ready, unacked)
- Alarms (memory, disk)

#### 6. Business Metrics
- Jobs scraped per hour
- Scrape success rates
- Active scraping sessions
- Circuit breaker states
- Platform performance summary
- Scrape duration percentiles

### Alertmanager

**Alert Routing:**
- Critical alerts: Immediate notification to on-call team
- Infrastructure alerts: Routed to infrastructure team
- Database alerts: Routed to database team
- Application alerts: Routed to application team
- Warning alerts: Less frequent notifications

**Inhibit Rules:**
- Critical alerts suppress warning alerts
- Service down alerts suppress service-specific alerts
- Database/cache/queue down alerts suppress component-specific alerts

## Configuration

### Email Notifications (Optional)

To enable email notifications, update the following in `alertmanager/alertmanager.yml`:

```yaml
global:
  smtp_smarthost: 'your-smtp-server:587'
  smtp_auth_username: 'your-email@example.com'
  smtp_auth_password: 'your-password'
  smtp_from: 'alertmanager@your-domain.com'
```

Update receiver email addresses:

```yaml
receivers:
  - name: 'critical-alerts'
    email_configs:
      - to: 'your-on-call@example.com'
```

### Docker Compose Integration

Add these services to your `docker-compose.yml`:

```yaml
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./monitoring/prometheus:/etc/prometheus
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
    ports:
      - "9090:9090"
    networks:
      - ghost-network

  grafana:
    image: grafana/grafana:latest
    volumes:
      - ./monitoring/grafana/provisioning:/etc/grafana/provisioning
      - grafana-data:/var/lib/grafana
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    ports:
      - "3001:3000"
    networks:
      - ghost-network

  alertmanager:
    image: prom/alertmanager:latest
    volumes:
      - ./monitoring/alertmanager:/etc/alertmanager
    command:
      - '--config.file=/etc/alertmanager/alertmanager.yml'
    ports:
      - "9093:9093"
    networks:
      - ghost-network

  node-exporter:
    image: prom/node-exporter:latest
    command:
      - '--path.rootfs=/host'
    pid: host
    volumes:
      - '/:/host:ro,rslave'
    networks:
      - ghost-network

  cadvisor:
    image: gcr.io/cadvisor/cadvisor:latest
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker:/var/lib/docker:ro
    networks:
      - ghost-network

  postgres-exporter:
    image: prometheuscommunity/postgres-exporter:latest
    environment:
      - DATA_SOURCE_NAME=postgresql://user:password@postgres:5432/ghost?sslmode=disable
    networks:
      - ghost-network

  redis-exporter:
    image: oliver006/redis_exporter:latest
    environment:
      - REDIS_ADDR=redis:6379
    networks:
      - ghost-network

  nginx-exporter:
    image: nginx/nginx-prometheus-exporter:latest
    command:
      - '-nginx.scrape-uri=http://nginx/nginx_status'
    networks:
      - ghost-network

  blackbox-exporter:
    image: prom/blackbox-exporter:latest
    networks:
      - ghost-network

volumes:
  prometheus-data:
  grafana-data:
```

## Accessing Dashboards

After starting the stack:

1. **Prometheus**: http://localhost:9090
2. **Grafana**: http://localhost:3001 (default credentials: admin/admin)
3. **Alertmanager**: http://localhost:9093

## Alert Severity Levels

- **Critical**: Immediate action required (service down, critical resources)
- **Warning**: Action needed soon (high resource usage, degraded performance)

## Retention

- **Prometheus**: 30 days of metrics data
- **Grafana**: Persistent dashboards and configurations

## Customization

### Adding New Metrics

1. Add scrape target to `prometheus/prometheus.yml`
2. Create recording rules in `prometheus/recording-rules.yml`
3. Add alert rules in `prometheus/alerting-rules.yml`
4. Update dashboards or create new ones

### Modifying Thresholds

Edit alert expressions in `prometheus/alerting-rules.yml`:

```yaml
- alert: HighCPUUsage
  expr: node:cpu_usage_percent > 80  # Change threshold here
  for: 5m                             # Change duration here
```

## Troubleshooting

### Prometheus not scraping targets

- Check target health in Prometheus UI: Status > Targets
- Verify service endpoints are accessible
- Check network connectivity between containers

### Grafana dashboards not loading

- Verify datasource configuration in Grafana UI
- Check Prometheus URL in datasource settings
- Ensure Prometheus is running and accessible

### Alerts not firing

- Check alert rules in Prometheus UI: Alerts
- Verify Alertmanager configuration
- Test email settings with a test alert

## Best Practices

1. **Regular Review**: Review dashboards and alerts weekly
2. **Threshold Tuning**: Adjust alert thresholds based on actual usage patterns
3. **Dashboard Maintenance**: Keep dashboards up to date with system changes
4. **Alert Fatigue**: Disable or adjust noisy alerts
5. **Backup**: Regularly backup Prometheus data and Grafana configurations

## Support

For issues or questions:
- Check Prometheus documentation: https://prometheus.io/docs/
- Check Grafana documentation: https://grafana.com/docs/
- Review alert definitions and recording rules in this repository
