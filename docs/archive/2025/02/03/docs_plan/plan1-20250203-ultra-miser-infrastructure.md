# Ghost Platform - Ultra Miser Mode Infrastructure Plan
**Date:** 2025-02-03  
**Status:** In Progress  
**Priority:** High  
**Cost Target:** $0-$15/month  

---

## Executive Summary

Transform Ghost from distributed microservices to a cost-optimized single-node deployment while maintaining architectural integrity. The plan enables starting at $0 using Oracle Cloud Free Tier or existing hardware, with a clear scaling path to distributed architecture.

## Objectives

1. **Cost Minimization:** Deploy for $0-$15/month vs $1,250+/month standard
2. **Architectural Integrity:** Preserve transactional outbox, event-driven patterns, circuit breakers
3. **Scalability Path:** Clear migration from single-node to distributed without code changes
4. **Operational Excellence:** Automated backups, monitoring, health checks

## Phase 1: Infrastructure Components ($0-15/month)

### Single Node Stack
| Component | Resource | Allocation | Purpose |
|-----------|----------|------------|---------|
| PostgreSQL | 2GB RAM, 1.5 CPU | Primary DB + Outbox Pattern |
| Redis | 1GB RAM, 0.5 CPU | Distributed cache, session store |
| RabbitMQ | 1GB RAM, 0.5 CPU | Message broker, event bus |
| Ghost App | 3GB RAM, 3 CPU | Monolithic services |
| Nginx | 128MB RAM, 0.2 CPU | Reverse proxy, SSL |
| Prometheus | 512MB RAM, 0.5 CPU | Metrics collection |
| Grafana | 256MB RAM, 0.2 CPU | Dashboards |

### Zero-Cost Options
1. **Oracle Cloud Free Tier:** ARM instances (24GB RAM total)
2. **Self-Hosted:** Existing hardware + Cloudflare Tunnel
3. **Hetzner CPX21:** $8.21/month (4 vCPU / 8GB / 160GB NVMe)

## Phase 2: Implementation Deliverables

### Infrastructure as Code
- [ ] Docker Compose (single-node)
- [ ] Terraform (multi-cloud)
- [ ] Ansible (configuration management)

### Configuration Files
- [ ] Environment templates (.env)
- [ ] Nginx reverse proxy config
- [ ] RabbitMQ topology definitions
- [ ] Prometheus scraping configs
- [ ] Grafana dashboards

### Operational Scripts
- [ ] Backup automation (local + S3)
- [ ] Restore procedures
- [ ] Health monitoring
- [ ] Log rotation

### Documentation
- [ ] Migration guide from current architecture
- [ ] Deployment procedures
- [ ] Troubleshooting runbook
- [ ] Scaling path documentation

## Phase 3: Testing Requirements

### Unit Tests (>80% coverage)
- [ ] Docker Compose validation
- [ ] Configuration parsing
- [ ] Backup/restore logic
- [ ] Health check endpoints

### Integration Tests
- [ ] Service startup sequence
- [ ] Database connectivity
- [ ] Message broker functionality
- [ ] End-to-end API flows

### Infrastructure Tests
- [ ] Terraform plan validation
- [ ] Ansible playbook dry-run
- [ ] Resource limit compliance

## Phase 4: Migration Path

### Current → Miser Mode
1. Export current data
2. Deploy single-node stack
3. Import data with migration scripts
4. Validate functionality
5. Switch traffic

### Miser Mode → Distributed
1. Extract database to dedicated node
2. Add RabbitMQ clustering
3. Split services horizontally
4. Implement load balancing
5. Add Kubernetes orchestration

## Success Criteria

- [ ] All services start successfully
- [ ] API responds to requests
- [ ] Database migrations run automatically
- [ ] Backups complete successfully
- [ ] Monitoring dashboards accessible
- [ ] Zero data loss during migration
- [ ] Can scale to distributed without downtime

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Single point of failure | Automated backups every 6 hours |
| Resource contention | Resource limits in Docker Compose |
| Data loss | Multi-tier backup strategy |
| Security | Vault for secrets, mTLS ready |

## Timeline

- **Week 1:** Docker Compose implementation + tests
- **Week 2:** Terraform + Ansible implementation
- **Week 3:** Migration scripts + documentation
- **Week 4:** Integration testing + validation

---

**Approved By:** Distinguished Engineering Team  
**Next Review:** 2025-02-10