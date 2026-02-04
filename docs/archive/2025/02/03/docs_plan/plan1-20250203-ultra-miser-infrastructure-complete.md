# Ghost Platform - Ultra Miser Mode Infrastructure Plan
**Version:** 1.0  
**Date:** 2025-02-03  
**Status:** Implementation Phase  
**Priority:** Critical  
**Cost Target:** $0-$15/month  
**Target Coverage:** >80% test coverage  

---

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Design Decisions & Rationale](#design-decisions--rationale)
4. [Infrastructure Components](#infrastructure-components)
5. [Implementation Details](#implementation-details)
6. [Migration Strategy](#migration-strategy)
7. [Testing Strategy](#testing-strategy)
8. [Risk Analysis](#risk-analysis)
9. [Success Criteria](#success-criteria)
10. [Appendices](#appendices)

---

## Executive Summary

### Problem Statement
Ghost Platform currently requires significant infrastructure investment ($1,250+/month) for a distributed microservices architecture. This creates a barrier to entry for small teams and early-stage deployments.

### Solution
Implement an "Ultra Miser Mode" - a single-node deployment that maintains all architectural patterns (transactional outbox, event-driven, circuit breakers) while operating at $0-$15/month. The solution provides a clear migration path to full distributed architecture without code changes.

### Key Metrics
| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Monthly Cost | $1,250+ | $0-$15 | 99% reduction |
| Infrastructure Nodes | 15+ | 1 | 93% reduction |
| Time to Deploy | 2-3 days | 10 minutes | 95% faster |
| Maintenance Overhead | High | Low | 80% reduction |

---

## Architecture Overview

### Single-Node Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SINGLE NODE (4 vCPU / 8GB RAM / 100GB SSD)          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    Docker Compose Stack                          │    │
│  │                                                                   │    │
│  │   ┌─────────────┐  ┌─────────────┐  ┌──────────────────────┐   │    │
│  │   │ PostgreSQL  │  │    Redis    │  │      RabbitMQ        │   │    │
│  │   │  (2GB RAM)  │  │  (1GB RAM)  │  │     (1GB RAM)        │   │    │
│  │   │  + Outbox   │  │  + Cache    │  │  + Event Bus         │   │    │
│  │   └─────────────┘  └─────────────┘  └──────────────────────┘   │    │
│  │                                                                   │    │
│  │   ┌──────────────────────────────────────────────────────────┐  │    │
│  │   │           Ghost Application (Monolithic)                  │  │    │
│  │   │  ┌─────────────┐  ┌─────────────┐  ┌────────────────┐   │  │    │
│  │   │  │   Session   │  │   Platform  │  │   Scheduler    │   │  │    │
│  │   │  │   Service   │  │   Gateway   │  │    Service     │   │  │    │
│  │   │  │  (In-Proc)  │  │  (In-Proc)  │  │   (In-Proc)    │   │  │    │
│  │   │  └─────────────┘  └─────────────┘  └────────────────┘   │  │    │
│  │   │                                                           │  │    │
│  │   │  ┌─────────────┐  ┌─────────────┐  ┌────────────────┐   │  │    │
│  │   │  │  Analytics  │  │    Config   │  │   Notification │   │  │    │
│  │   │  │   Service   │  │   Service   │  │     Service    │   │  │    │
│  │   │  │  (In-Proc)  │  │  (In-Proc)  │  │   (In-Proc)    │   │  │    │
│  │   │  └─────────────┘  └─────────────┘  └────────────────┘   │  │    │
│  │   └──────────────────────────────────────────────────────────┘  │    │
│  │                              │                                    │    │
│  │   ┌─────────────┐  ┌─────────┴──────┐  ┌────────────────┐      │    │
│  │   │    Nginx    │  │   Prometheus   │  │    Grafana     │      │    │
│  │   │   (Proxy)   │  │  (Metrics)     │  │ (Dashboards)   │      │    │
│  │   └─────────────┘  └────────────────┘  └────────────────┘      │    │
│  │                                                                   │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
│  Total Memory: ~7.4GB used / 8GB available                              │
│  Resource Efficiency: 92.5%                                             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Design Decisions & Rationale

### Decision 1: Single Node vs Distributed

**Question:** Should we start with single-node or maintain distributed architecture?

**Options Considered:**
1. **Distributed (Current):** 15+ nodes, high availability, $1,250+/month
2. **Single Node (Selected):** 1 node, fault-tolerant patterns, $0-15/month
3. **Hybrid:** 3 nodes minimum, partial HA, $150/month

**Decision:** Single Node

**Rationale:**
- 99% cost reduction enables broader adoption
- Modern container orchestration provides sufficient fault tolerance
- Clear migration path to distributed when revenue justifies
- Architectural patterns (outbox, sagas) work identically in both modes

**Trade-offs:**
- **Pros:** Extreme cost savings, simpler operations, faster deployment
- **Cons:** Single point of failure (mitigated by automated backups)

### Decision 2: Message Broker Selection

**Question:** Kafka vs RabbitMQ for the message broker?

**Options Considered:**
1. **Apache Kafka:** High throughput, complex operations, $400/month infrastructure
2. **RabbitMQ (Selected):** Simpler operations, sufficient throughput, integrated management
3. **Redis Streams:** No additional infrastructure, limited features

**Decision:** RabbitMQ

**Rationale:**
- 50K msg/sec sufficient for Ghost's workload
- Built-in management UI reduces operational overhead
- Native quorum queues provide high availability when scaled
- Easier backup/restore procedures

**Trade-offs:**
- **Pros:** Lower operational complexity, better documentation, native priority queues
- **Cons:** Lower throughput than Kafka (not a constraint for Ghost)

### Decision 3: Database Architecture

**Question:** Single PostgreSQL vs Primary-Replica setup?

**Options Considered:**
1. **Single Instance (Selected):** Simple, fast backups, easy recovery
2. **Primary-Replica:** Read scaling, failover, 2x infrastructure cost
3. **Managed Service:** RDS/Cloud SQL, $200+/month, less control

**Decision:** Single PostgreSQL with aggressive backup strategy

**Rationale:**
- Read load is low enough for single instance
- Automated backups every 6 hours to S3
- Can promote replica later without application changes
- Connection pooling (PgBouncer) handles concurrent load

### Decision 4: Service Deployment Model

**Question:** Separate containers per service vs single monolithic container?

**Options Considered:**
1. **Microservice Containers:** True isolation, 5+ containers, orchestration complexity
2. **Single Monolithic (Selected):** All services in one process, simpler deployment
3. **Hybrid:** Core services separate, utilities in monolith

**Decision:** Single monolithic container with logical service separation

**Rationale:**
- Services already designed for independent deployment
- Same codebase, just different entry points disabled
- Zero code changes required for future extraction
- Shared memory reduces overhead

---

## Infrastructure Components

### Component 1: PostgreSQL Database

**Purpose:** Primary data store + Transactional Outbox pattern implementation

**Configuration:**
```yaml
Version: 16-alpine
Memory: 2GB (25% of total)
CPU: 1.5 cores
Storage: 50GB SSD
Connections: 100 max
Backup: Every 6 hours to S3
```

**Optimization Parameters:**
- `shared_buffers`: 512MB (25% of RAM)
- `effective_cache_size`: 1536MB (75% of RAM)
- `work_mem`: 2621kB (per connection)
- `maintenance_work_mem`: 128MB
- `max_wal_size`: 4GB

**Outbox Pattern Tables:**
```sql
outbox_messages (id, message_id, exchange, routing_key, payload, headers, status, retry_count, created_at)
inbox_messages (message_id, consumer_name, processed_at)
```

### Component 2: Redis Cache

**Purpose:** Session store, distributed locking, rate limiting

**Configuration:**
```yaml
Version: 7-alpine
Memory: 1GB
Persistence: AOF + RDB
Maxmemory Policy: allkeys-lru
TCP Keepalive: 60s
```

**Data Structures:**
- Sessions: `ghost:session:{id}` (Hash)
- Session Pool: `ghost:sessions:pool:{tier}` (Sorted Set)
- Circuit Breakers: `ghost:circuitbreaker:{platform}` (Hash)
- Rate Limits: `ghost:ratelimit:{platform}:{window}` (String)

### Component 3: RabbitMQ Message Broker

**Purpose:** Event bus, saga coordination, async processing

**Configuration:**
```yaml
Version: 3.13-management-alpine
Memory: 1GB
Management UI: Enabled
Plugins: rabbitmq_prometheus, rabbitmq_management
Queue Type: Quorum (HA-ready)
```

**Topology:**
```
Exchanges:
  - ghost.jobs (topic)
  - ghost.sessions (topic)
  - ghost.platforms (direct)
  - ghost.metrics (fanout)

Queues:
  - ghost.jobs.requests (quorum)
  - ghost.jobs.results (classic)
  - ghost.sessions.events (quorum)
  - ghost.platform.linkedin (quorum)
  - ghost.metrics.raw (stream)
```

### Component 4: Ghost Application

**Purpose:** Monolithic deployment of all services

**Configuration:**
```yaml
Runtime: .NET 9
Memory: 3GB
CPU: 3 cores
Health Check: /health endpoint
Restart Policy: unless-stopped
```

**Service Activation (via Environment):**
- `GHost__Services__Session__Enabled: true`
- `GHost__Services__Platform__Enabled: true`
- `GHost__Services__Scheduler__Enabled: true`
- `GHost__Services__Analytics__Enabled: true`
- `GHost__Services__Config__Enabled: true`

### Component 5: Nginx Reverse Proxy

**Purpose:** SSL termination, load balancing, rate limiting

**Configuration:**
```yaml
Worker Processes: auto
Connections: 1024
Gzip: Enabled
Rate Limiting: 10 req/s per IP
Timeout: 60s
```

**Features:**
- HTTP → HTTPS redirect
- WebSocket support
- Static file serving
- Health check endpoint

### Component 6: Prometheus

**Purpose:** Metrics collection and storage

**Configuration:**
```yaml
Retention: 15 days
Scrape Interval: 15s
Storage: Local TSDB
Targets: All services
```

**Scrape Targets:**
- Ghost WebAPI: :8080/metrics
- PostgreSQL: postgres-exporter:9187
- Redis: redis-exporter:9121
- RabbitMQ: rabbitmq:15692
- Node: node-exporter:9100

### Component 7: Grafana

**Purpose:** Metrics visualization and alerting

**Configuration:**
```yaml
Anonymous Access: Enabled (Viewer)
Admin Auth: Password protected
Plugins: Clock, JSON datasource
Dashboards: Pre-configured
```

**Dashboards Included:**
1. Infrastructure Overview
2. Application Performance
3. Database Metrics
4. Cache Performance
5. Message Queue Metrics
6. Business Metrics (Jobs scraped, success rates)

---

## Implementation Details

### Directory Structure

```
infrastructure/
├── miser-mode/
│   ├── docker/
│   │   ├── docker-compose.yml
│   │   ├── .env.example
│   │   ├── nginx/
│   │   │   └── nginx.conf
│   │   ├── rabbitmq/
│   │   │   ├── rabbitmq.conf
│   │   │   └── definitions.json
│   │   ├── monitoring/
│   │   │   ├── prometheus/
│   │   │   │   └── prometheus.yml
│   │   │   ├── grafana/
│   │   │   │   └── provisioning/
│   │   │   └── fluent-bit/
│   │   │       └── fluent-bit.conf
│   │   ├── init-scripts/
│   │   │   └── 01-init-db.sql
│   │   ├── backups/
│   │   │   ├── postgres/
│   │   │   ├── redis/
│   │   │   └── rabbitmq/
│   │   └── logs/
│   │       ├── nginx/
│   │       └── app/
│   ├── terraform/
│   │   ├── modules/
│   │   │   ├── hetzner/
│   │   │   ├── digitalocean/
│   │   │   └── oracle/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   ├── outputs.tf
│   │   └── versions.tf
│   ├── ansible/
│   │   ├── playbooks/
│   │   │   ├── setup.yml
│   │   │   └── deploy.yml
│   │   ├── roles/
│   │   │   ├── docker/
│   │   │   ├── monitoring/
│   │   │   └── backup/
│   │   ├── inventory/
│   │   └── ansible.cfg
│   ├── scripts/
│   │   ├── backup.sh
│   │   ├── restore.sh
│   │   ├── migrate.sh
│   │   ├── health-check.sh
│   │   └── setup-ssl.sh
│   └── tests/
│       ├── unit/
│       ├── integration/
│       └── infrastructure/
└── docs/
    └── plan/
        └── plan1-20250203-ultra-miser-infrastructure.md
```

---

## Migration Strategy

### Phase 1: Pre-Migration Preparation

1. **Audit Current State**
   - Document all active sessions
   - Export job configuration
   - Record platform credentials
   - Note custom configurations

2. **Backup Current Data**
   ```bash
   # Export all data from current system
   ./scripts/migrate.sh --export --source=current
   ```

3. **Provision New Infrastructure**
   ```bash
   # Using Terraform
   cd infrastructure/miser-mode/terraform
   terraform init
   terraform plan -var="environment=production"
   terraform apply
   ```

### Phase 2: Data Migration

1. **Database Migration**
   ```bash
   # Run migration scripts
   ./scripts/migrate.sh --import --target=postgres
   ```

2. **Configuration Migration**
   ```bash
   # Copy and transform configs
   ./scripts/migrate.sh --import --target=config
   ```

3. **Validate Migration**
   ```bash
   # Run validation checks
   ./scripts/health-check.sh --full
   ```

### Phase 3: Cutover

1. **Blue-Green Deployment**
   - Start new environment
   - Run parallel for 24 hours
   - Monitor error rates
   - Switch DNS/traefik

2. **Rollback Plan**
   - Keep old environment for 7 days
   - Automated rollback triggers
   - Data sync maintained

---

## Testing Strategy

### Unit Tests (>80% Coverage)

**Docker Compose Tests:**
```csharp
[TestClass]
public class DockerComposeValidationTests
{
    [TestMethod]
    public void ValidateDockerComposeSchema()
    {
        // Test that docker-compose.yml is valid YAML
        // Test that all services have required configuration
        // Test resource limits are specified
    }
    
    [TestMethod]
    public void ValidateServiceDependencies()
    {
        // Test that dependency graph is acyclic
        // Test that health checks are configured
        // Test that no circular dependencies exist
    }
}
```

**Configuration Tests:**
```csharp
[TestClass]
public class ConfigurationTests
{
    [TestMethod]
    public void ValidateEnvironmentVariables()
    {
        // Test all required env vars are documented
        // Test default values are reasonable
        // Test sensitive values are marked as secrets
    }
    
    [TestMethod]
    public void ValidateNginxConfiguration()
    {
        // Test nginx.conf syntax
        // Test SSL configuration
        // Test rate limiting rules
    }
}
```

**Backup/Restore Tests:**
```csharp
[TestClass]
public class BackupRestoreTests
{
    [TestMethod]
    public async Task BackupCreatesValidArchive()
    {
        // Test backup script execution
        // Test archive integrity
        // Test backup includes all required data
    }
    
    [TestMethod]
    public async Task RestoreFromBackupSucceeds()
    {
        // Test restore script
        // Test data integrity after restore
        // Test service starts after restore
    }
}
```

### Integration Tests

**Service Startup Sequence:**
```bash
#!/bin/bash
# tests/integration/test-startup.sh

echo "Testing service startup sequence..."

# 1. Start infrastructure
docker-compose up -d postgres redis rabbitmq
wait_for_healthy postgres 30
wait_for_healthy redis 30
wait_for_healthy rabbitmq 30

# 2. Start application
docker-compose up -d ghost-webapi
wait_for_healthy ghost-webapi 60

# 3. Test connectivity
curl -f http://localhost:8080/health || exit 1
curl -f http://localhost:15672 || exit 1

echo "All services started successfully!"
```

**End-to-End API Tests:**
```csharp
[TestClass]
public class EndToEndTests
{
    [TestMethod]
    public async Task FullJobSearchWorkflow()
    {
        // 1. Create search criteria
        // 2. Submit job request
        // 3. Verify message in queue
        // 4. Process job
        // 5. Verify result in database
        // 6. Check metrics recorded
    }
}
```

### Infrastructure Tests

**Terraform Validation:**
```bash
#!/bin/bash
# tests/infrastructure/test-terraform.sh

cd infrastructure/miser-mode/terraform

# Validate syntax
terraform validate

# Check formatting
terraform fmt -check

# Plan and check for errors
terraform plan -detailed-exitcode -out=tfplan
```

**Ansible Dry-Run:**
```bash
#!/bin/bash
# tests/infrastructure/test-ansible.sh

ansible-playbook -i inventory/test setup.yml --check --diff
```

---

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation | Status |
|------|------------|--------|------------|--------|
| Single node failure | Medium | High | Automated backups every 6h, 30-day retention | Mitigated |
| Resource exhaustion | Low | High | Resource limits, monitoring alerts, auto-restart | Mitigated |
| Data loss | Low | Critical | Multi-tier backup strategy (local + S3 + offsite) | Mitigated |
| Security breach | Medium | High | Network isolation, secrets management, regular updates | Monitoring |
| Performance degradation | Low | Medium | Resource monitoring, scaling triggers documented | Documented |
| Vendor lock-in | Low | Medium | Terraform multi-cloud, container portability | Mitigated |

---

## Success Criteria

### Functional Requirements
- [ ] All Docker services start without errors
- [ ] API responds to health checks within 5 seconds
- [ ] Database migrations run automatically on startup
- [ ] RabbitMQ topology is created on first run
- [ ] Grafana dashboards display real-time metrics
- [ ] Backup completes successfully within 10 minutes
- [ ] Restore from backup completes within 15 minutes

### Non-Functional Requirements
- [ ] API latency < 100ms for health checks
- [ ] Database queries < 50ms p99
- [ ] Memory usage < 7.5GB of 8GB available
- [ ] CPU usage < 80% under normal load
- [ ] Zero data loss during migration
- [ ] < 5 minutes downtime for deployment

### Test Coverage
- [ ] Unit tests: >80% coverage
- [ ] Integration tests: All critical paths
- [ ] Infrastructure tests: All providers
- [ ] End-to-end tests: Complete workflows

---

## Appendices

### Appendix A: Cost Breakdown

**Oracle Cloud Free Tier:**
| Resource | Specs | Cost |
|----------|-------|------|
| ARM Instance | 4 OCPU / 24GB RAM | $0 |
| Block Storage | 200GB | $0 |
| Outbound Traffic | 10TB/month | $0 |
| **Total** | | **$0/month** |

**Hetzner Cloud (Recommended):**
| Resource | Specs | Cost |
|----------|-------|------|
| CPX21 Server | 4 vCPU / 8GB / 160GB | €5.35 |
| Additional Volume | 100GB | €4.40 |
| Snapshots | 50GB | €2.25 |
| **Total** | | **€11.99 (~$13/month)** |

### Appendix B: Scaling Triggers

**When to Scale to Distributed:**
- CPU consistently > 70% for 1 hour
- Memory consistently > 85% for 30 minutes
- Response time > 500ms p99
- Queue depth > 1000 messages
- Concurrent sessions > 50

**Scaling Steps:**
1. Add dedicated database node
2. Add Redis cluster node
3. Split services to separate containers
4. Add load balancer
5. Implement Kubernetes

### Appendix C: Troubleshooting

**PostgreSQL won't start:**
```bash
# Check logs
docker logs ghost-postgres

# Verify disk space
df -h

# Check configuration
docker exec ghost-postgres cat /var/lib/postgresql/data/pgdata/postgresql.conf
```

**RabbitMQ connection refused:**
```bash
# Check if service is healthy
docker ps | grep rabbitmq

# Verify port is listening
docker exec ghost-rabbitmq netstat -tlnp | grep 5672

# Check Erlang cookie
docker exec ghost-rabbitmq cat /var/lib/rabbitmq/.erlang.cookie
```

---

**Document Owner:** Distinguished Engineering Team  
**Last Updated:** 2025-02-03  
**Next Review:** 2025-02-10