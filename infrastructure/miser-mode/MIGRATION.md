# Ghost Platform Migration Guide

**Migrating from Distributed Architecture to Ultra Miser Mode**

This guide provides comprehensive instructions for migrating your Ghost Platform deployment from a distributed, multi-node architecture to a cost-optimized single-node Ultra Miser Mode deployment.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Migration Strategy](#migration-strategy)
- [Pre-Migration Checklist](#pre-migration-checklist)
- [Migration Process](#migration-process)
- [Post-Migration Validation](#post-migration-validation)
- [Rollback Procedure](#rollback-procedure)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)

---

## Overview

### What is Ultra Miser Mode?

Ultra Miser Mode is a single-node deployment configuration that maintains Ghost Platform's architectural integrity while drastically reducing infrastructure costs from $100+/month to $0-$15/month.

**Key Changes:**
- **Architecture:** Distributed microservices → Monolithic single-node
- **Database:** Clustered PostgreSQL → Single PostgreSQL instance
- **Cache:** Redis Cluster → Single Redis instance
- **Message Broker:** RabbitMQ Cluster → Single RabbitMQ node
- **Hosting:** Multiple cloud instances → Single VPS/instance

**What's Preserved:**
- All data and configuration
- Application functionality
- API contracts
- Monitoring and observability
- Backup capabilities

### Migration Timeline

| Phase | Duration | Description |
|-------|----------|-------------|
| Planning | 1-2 days | Review this guide, prepare environment |
| Pre-migration | 2-4 hours | Export data, validate source system |
| Migration | 1-2 hours | Import data, configure target system |
| Validation | 2-4 hours | Verify data integrity, test functionality |
| Monitoring | 24-48 hours | Observe system under real load |
| Cutover | 1 hour | Switch DNS/traffic to new system |

**Total:** 3-7 days (including monitoring period)

---

## Prerequisites

### Required Tools

Ensure the following tools are installed on your migration workstation:

```bash
# System utilities
docker >= 20.10
docker-compose >= 2.0
ssh
scp
jq

# Database tools
psql >= 12
pg_dump >= 12
pg_restore >= 12

# Cache tools
redis-cli >= 6.0

# Message broker tools
rabbitmqadmin
curl

# System tools
tar
gzip
sha256sum
nc (netcat)
```

Install missing tools:

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y docker.io docker-compose postgresql-client redis-tools curl jq netcat

# macOS
brew install docker docker-compose postgresql redis jq netcat

# Download rabbitmqadmin
wget http://your-rabbitmq-host:15672/cli/rabbitmqadmin
chmod +x rabbitmqadmin
sudo mv rabbitmqadmin /usr/local/bin/
```

### Target System Requirements

**Minimum Specifications:**
- **CPU:** 4 cores / vCPUs
- **RAM:** 8 GB
- **Disk:** 50 GB SSD (100 GB recommended)
- **Network:** 1 Gbps
- **OS:** Ubuntu 22.04 LTS or similar

**Recommended Providers:**
- **Hetzner Cloud:** CPX31 (€12.49/month) - Best value
- **DigitalOcean:** Droplet 4GB ($24/month)
- **Vultr:** High Performance 4GB ($18/month)
- **Oracle Cloud:** Free tier (4 OCPUs, 24GB RAM)

### Source System Access

Ensure you have:
- [ ] SSH access to source system with sudo privileges
- [ ] Database credentials (PostgreSQL, Redis, RabbitMQ)
- [ ] Access to configuration files
- [ ] Backup of any custom scripts or configurations
- [ ] List of all API keys and secrets

### Network Requirements

- [ ] Sufficient bandwidth for data transfer (estimate: 1-10 GB)
- [ ] Stable connection between source and target (if remote)
- [ ] Open ports on source system: 5432, 6379, 5672, 15672
- [ ] Firewall rules configured on target system

---

## Migration Strategy

### Migration Modes

#### 1. **Direct Migration (Recommended)**

Migrate directly from source to target with minimal downtime.

**Pros:** Fastest, simplest
**Cons:** Some downtime required
**Downtime:** 1-2 hours

```bash
./scripts/migrate.sh \
  --source-host prod.example.com \
  --target-host miser.example.com \
  --interactive
```

#### 2. **Staged Migration**

Export data, transfer separately, then import.

**Pros:** More control, can be done offline
**Cons:** More manual steps
**Downtime:** Can be near-zero with proper planning

```bash
# Step 1: Export from source
./scripts/export-data.sh \
  --host prod.example.com \
  --output-dir ./migration-data

# Step 2: Transfer data (if needed)
rsync -avz ./migration-data/ target-server:/tmp/migration-data/

# Step 3: Import to target
./scripts/import-data.sh \
  --input-dir /tmp/migration-data \
  --target-host localhost
```

#### 3. **Blue-Green Migration**

Run both systems in parallel, switch traffic after validation.

**Pros:** Zero downtime, easy rollback
**Cons:** Requires data sync strategy
**Downtime:** None (with proper setup)

### Downtime Considerations

**Minimize Downtime:**

1. **Pre-export data** during low-traffic periods
2. **Parallel setup** - configure target while source runs
3. **Incremental sync** - use database replication if possible
4. **DNS TTL** - Lower TTL 24h before migration (to 60s)
5. **Maintenance window** - Schedule during off-peak hours

**Acceptable Downtime:** Most migrations complete in 1-2 hours.

---

## Pre-Migration Checklist

### 1. Document Current State

```bash
# Capture current system state
ssh source-host << 'EOF'
  # Service versions
  docker --version
  docker-compose ps
  
  # Database size
  docker exec postgres psql -U ghost -c '\l+'
  
  # Redis keys
  docker exec redis redis-cli DBSIZE
  
  # RabbitMQ queues
  docker exec rabbitmq rabbitmqctl list_queues
EOF
```

### 2. Backup Source System

```bash
# Create full backup before migration
ssh source-host << 'EOF'
  cd /opt/ghost
  docker-compose exec -T postgres pg_dump -U ghost ghost > backup_$(date +%Y%m%d).sql
  docker-compose exec -T redis redis-cli --rdb /tmp/dump.rdb SAVE
  docker cp redis:/tmp/dump.rdb ./backup_redis_$(date +%Y%m%d).rdb
EOF

# Download backups to safe location
scp source-host:/opt/ghost/backup_* ./pre-migration-backups/
```

### 3. Test Scripts

```bash
# Dry run to verify everything works
./scripts/migrate.sh \
  --source-host prod.example.com \
  --dry-run
```

### 4. Notify Stakeholders

- [ ] Schedule maintenance window
- [ ] Send notification to users
- [ ] Prepare status page update
- [ ] Alert team members
- [ ] Prepare rollback plan

### 5. Prepare Target System

```bash
# SSH to target system
ssh target-host

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Clone repository
git clone https://github.com/your-org/Ghost.git
cd Ghost/infrastructure/miser-mode

# Create .env file
cp docker/.env.example docker/.env
nano docker/.env  # Edit with your credentials
```

---

## Migration Process

### Step 1: Pre-Migration Validation

```bash
# On your migration workstation
cd /path/to/Ghost/infrastructure/miser-mode/scripts

# Validate prerequisites
./migrate.sh \
  --source-host prod.example.com \
  --source-user admin \
  --dry-run
```

**Expected Output:**
```
✓ All required commands are available
✓ Sufficient disk space available: 45GB
✓ Source host is reachable
✓ Docker is running
```

### Step 2: Export Source Data

```bash
# Option A: As part of full migration
./migrate.sh --source-host prod.example.com

# Option B: Standalone export
./export-data.sh \
  --host prod.example.com \
  --user admin \
  --output-dir ../backups/export_$(date +%Y%m%d)
```

**What Gets Exported:**
- PostgreSQL database (full dump with schema)
- Redis data (RDB snapshot)
- RabbitMQ configuration and messages
- Environment configurations
- Application settings

**Export Location:** `infrastructure/miser-mode/backups/export_YYYYMMDD_HHMMSS/`

**Duration:** 10-30 minutes (depends on data size)

### Step 3: Transfer Data (if needed)

If source and target are different hosts:

```bash
# Compress export
cd ../backups
tar czf export_$(date +%Y%m%d).tar.gz export_*/

# Transfer to target
scp export_*.tar.gz target-host:/tmp/

# On target, extract
ssh target-host
cd /tmp
tar xzf export_*.tar.gz
```

### Step 4: Import to Target

```bash
# On target system (or remotely)
cd /path/to/Ghost/infrastructure/miser-mode/scripts

./import-data.sh \
  --input-dir ../backups/export_YYYYMMDD_HHMMSS \
  --target-host localhost
```

**Import Process:**
1. ✓ Start PostgreSQL, Redis, RabbitMQ containers
2. ✓ Wait for services to be healthy
3. ✓ Import PostgreSQL database
4. ✓ Import Redis data
5. ✓ Import RabbitMQ configuration
6. ✓ Start Ghost WebAPI
7. ✓ Start Nginx and monitoring

**Duration:** 20-40 minutes

### Step 5: Validate Migration

```bash
./validate-migration.sh \
  --export-dir ../backups/export_YYYYMMDD_HHMMSS \
  --target-host localhost
```

**Validation Checks:**
- ✓ All Docker containers running and healthy
- ✓ PostgreSQL connection and data integrity
- ✓ Redis connection and key count
- ✓ RabbitMQ topology and queues
- ✓ Application health endpoints
- ✓ API functionality
- ✓ Resource usage within limits
- ✓ Network connectivity between services

**Expected Result:** All checks pass or have minor warnings only.

---

## Post-Migration Validation

### Functional Testing

```bash
# Test API health
curl http://target-host:8080/health

# Test Swagger UI
open http://target-host:8080/swagger

# Test RabbitMQ Management
open http://target-host:15672  # guest/guest

# Test Grafana Dashboards
open http://target-host:3000   # admin/admin
```

### Data Integrity Checks

```bash
# Compare record counts
ssh source-host "docker exec postgres psql -U ghost -d ghost -c 'SELECT COUNT(*) FROM jobs;'"
ssh target-host "docker exec ghost-postgres psql -U ghost -d ghost -c 'SELECT COUNT(*) FROM jobs;'"

# Verify Redis keys
ssh source-host "docker exec redis redis-cli DBSIZE"
ssh target-host "docker exec ghost-redis redis-cli DBSIZE"

# Check RabbitMQ queues
ssh source-host "docker exec rabbitmq rabbitmqctl list_queues"
ssh target-host "docker exec ghost-rabbitmq rabbitmqctl list_queues"
```

### Performance Testing

```bash
# Monitor resource usage
docker stats

# Check response times
curl -w "@curl-format.txt" -o /dev/null -s http://target-host:8080/health

# Load test (optional)
ab -n 1000 -c 10 http://target-host:8080/api/v1/status
```

### Monitoring Period

**24-48 Hour Observation:**

1. **Monitor Logs:**
   ```bash
   docker-compose -f docker/docker-compose.yml logs -f ghost-webapi
   ```

2. **Watch Metrics:**
   - Open Grafana: http://target-host:3000
   - Check CPU, memory, disk usage
   - Monitor error rates

3. **Test Key Workflows:**
   - Job submission and processing
   - API endpoint functionality
   - Background task execution
   - Cache hit rates

4. **Check Alerts:**
   - Ensure no critical alerts firing
   - Verify monitoring is working

---

## Cutover Procedure

Once validation passes and monitoring looks good:

### 1. Prepare for Cutover

```bash
# Lower DNS TTL (if not done 24h prior)
# Update your DNS records to TTL=60

# Final sync (if using blue-green)
# Ensure any last-minute data is synced

# Alert team
# Send notification that cutover is beginning
```

### 2. Execute Cutover

```bash
# Put source in maintenance mode
ssh source-host "docker-compose stop ghost-webapi"

# Update DNS to point to target
# A records: old-ip → new-ip
# Wait for TTL to expire (60 seconds if lowered)

# Update load balancer (if applicable)
# Point to new target IP

# Test from external location
curl -I http://your-domain.com/health
```

### 3. Verify Cutover

```bash
# Check traffic is hitting new system
docker-compose logs -f ghost-webapi | grep "GET /api"

# Monitor metrics in Grafana
# Ensure requests are being served

# Test key user workflows
# Have team members test critical paths
```

### 4. Keep Source Running

**Do not decommission source system immediately!**

- Keep running for 7 days as backup
- Monitor for any issues
- Allow time for rollback if needed

---

## Rollback Procedure

If issues arise during or after migration:

### Immediate Rollback (During Migration)

```bash
# Stop target system
docker-compose -f docker/docker-compose.yml down

# Revert DNS (if changed)
# Point back to source IP

# Restart source system
ssh source-host "docker-compose up -d"

# Verify source is healthy
curl http://source-host:8080/health
```

### Rollback After Cutover

```bash
# 1. Alert team of rollback
echo "Rolling back to source system..."

# 2. Update DNS to source
# Change A records back to old-ip

# 3. Verify source system
ssh source-host << 'EOF'
  docker-compose ps
  docker-compose logs --tail=100 ghost-webapi
EOF

# 4. Test source system
curl http://source-host:8080/health

# 5. Investigate target issues
docker-compose -f docker/docker-compose.yml logs

# 6. Plan remediation
# Review logs and validation results
# Determine what needs to be fixed
```

### Data Recovery

If target system was used and data needs to be recovered:

```bash
# Export any new data from target
./scripts/export-data.sh \
  --host target-host \
  --output-dir ../backups/rollback_export

# Manually merge critical data back to source
# (This is complex and should be done carefully)
```

---

## Troubleshooting

### Common Issues

#### 1. "Cannot connect to source host"

**Cause:** SSH connection issues

**Solution:**
```bash
# Test SSH manually
ssh -v user@source-host

# Check firewall rules
ssh source-host "sudo ufw status"

# Verify SSH key
ssh-add -l
```

#### 2. "PostgreSQL dump failed"

**Cause:** Insufficient permissions or disk space

**Solution:**
```bash
# Check disk space
ssh source-host "df -h"

# Verify PostgreSQL is running
ssh source-host "docker exec postgres pg_isready -U ghost"

# Test dump manually
ssh source-host "docker exec postgres pg_dump -U ghost ghost" > test.sql
```

#### 3. "Redis BGSAVE in progress"

**Cause:** Ongoing background save

**Solution:**
```bash
# Wait for save to complete
ssh source-host "docker exec redis redis-cli LASTSAVE"

# Check Redis info
ssh source-host "docker exec redis redis-cli INFO persistence"

# Try again after a few minutes
```

#### 4. "Target system out of memory"

**Cause:** Insufficient RAM for all services

**Solution:**
```bash
# Check current usage
docker stats

# Reduce resource limits in docker-compose.yml
# Edit limits for services

# Restart with lower limits
docker-compose down
docker-compose up -d
```

#### 5. "Health checks failing"

**Cause:** Services not fully started

**Solution:**
```bash
# Check service status
docker-compose ps

# View logs
docker-compose logs ghost-webapi

# Wait longer for services to be ready
# Health checks can take 2-3 minutes

# Restart unhealthy service
docker-compose restart ghost-webapi
```

#### 6. "RabbitMQ topology import failed"

**Cause:** Incompatible RabbitMQ versions or permissions

**Solution:**
```bash
# Check RabbitMQ version
docker exec ghost-rabbitmq rabbitmqctl version

# Verify management plugin enabled
docker exec ghost-rabbitmq rabbitmq-plugins list

# Import manually via API
curl -u guest:guest -H "Content-Type: application/json" \
  -X POST http://localhost:15672/api/definitions \
  -d @definitions.json
```

### Log Locations

```bash
# Migration logs
infrastructure/miser-mode/logs/migration_*.log
infrastructure/miser-mode/logs/validation_*.log

# Docker logs
docker-compose logs ghost-webapi
docker-compose logs postgres
docker-compose logs redis
docker-compose logs rabbitmq

# Application logs
infrastructure/miser-mode/docker/logs/
```

### Performance Issues

If the target system is slow:

```bash
# 1. Check resource usage
docker stats

# 2. Analyze slow queries (PostgreSQL)
docker exec ghost-postgres psql -U ghost -d ghost -c \
  "SELECT query, calls, total_time, mean_time 
   FROM pg_stat_statements 
   ORDER BY mean_time DESC LIMIT 10;"

# 3. Check Redis memory
docker exec ghost-redis redis-cli INFO memory

# 4. Monitor RabbitMQ
open http://localhost:15672

# 5. Review Grafana dashboards
open http://localhost:3000
```

---

## FAQ

### General Questions

**Q: How long does migration take?**

A: Export: 10-30 min, Import: 20-40 min, Validation: 15-30 min. Total active time: 1-2 hours.

**Q: Will I lose any data?**

A: No. The migration creates complete backups and performs integrity checks. Source system remains untouched.

**Q: Can I rollback after migration?**

A: Yes. Keep source system running for 7 days. Rollback can be done anytime by reverting DNS.

**Q: What's the expected downtime?**

A: 1-2 hours for direct migration. Can be reduced to near-zero with blue-green approach.

**Q: Can I migrate a live system?**

A: Yes, but recommended to do during low-traffic periods for data consistency.

### Technical Questions

**Q: What if my database is very large (100GB+)?**

A: Export/import will take longer. Consider:
- Using pg_dump with --jobs=4 for parallel export
- Compression during transfer
- Direct database replication instead

**Q: Can I migrate between different cloud providers?**

A: Yes. Scripts support any SSH-accessible Linux host with Docker.

**Q: What about custom configurations?**

A: Configuration files are exported for reference. Review and merge manually.

**Q: Do I need to update application code?**

A: No. Ultra Miser Mode uses the same Docker images and API contracts.

**Q: What if I have data in external systems?**

A: Migration handles PostgreSQL, Redis, and RabbitMQ. External integrations (S3, external APIs) need separate consideration.

### Cost Questions

**Q: How much will I save?**

A: Typical savings: $100-300/month → $10-20/month (85-95% reduction)

**Q: What's the performance impact?**

A: For typical workloads (<10 concurrent users), minimal impact. Heavy workloads may see 10-30% slower response times.

**Q: Can I scale back up later?**

A: Yes. The reverse migration (Miser → Distributed) follows similar process.

**Q: What about backups and disaster recovery?**

A: Automated daily backups to local storage included. Optional S3-compatible backup available ($0.005/GB).

---

## Additional Resources

### Scripts Reference

- **migrate.sh** - Main orchestrator script
- **export-data.sh** - Export from source system
- **import-data.sh** - Import to target system
- **validate-migration.sh** - Verify migration success

### Documentation

- [Ultra Miser Mode Overview](../docs/OVERVIEW.md)
- [Docker Compose Reference](../docker/docker-compose.yml)
- [Cost Analysis](../docs/COST_ANALYSIS.md)
- [Performance Tuning](../docs/PERFORMANCE.md)

### Support

- **Issues:** https://github.com/your-org/Ghost/issues
- **Discussions:** https://github.com/your-org/Ghost/discussions
- **Email:** support@your-domain.com

---

## Appendix

### A. Configuration File Reference

**Source System Configuration:**

```bash
# .env on source system (example)
DB_PASSWORD=prod-password
RABBITMQ_PASSWORD=prod-rmq-password
```

**Target System Configuration:**

```bash
# docker/.env (must be created)
DB_PASSWORD=new-secure-password
RABBITMQ_PASSWORD=new-rmq-password
GRAFANA_PASSWORD=admin-password

# Enable platforms as needed
LINKEDIN_ENABLED=true
INDEED_ENABLED=true
```

### B. Migration Checklist

```
Pre-Migration:
☐ Review this guide completely
☐ Install all required tools
☐ Provision target system
☐ Test SSH connectivity
☐ Backup source system
☐ Test scripts in dry-run mode
☐ Lower DNS TTL (24h before)
☐ Schedule maintenance window
☐ Notify stakeholders

Migration Day:
☐ Final backup of source
☐ Run export script
☐ Verify export integrity
☐ Transfer data to target (if needed)
☐ Run import script
☐ Run validation script
☐ Review validation report
☐ Test API endpoints
☐ Test critical workflows

Post-Migration:
☐ Update DNS
☐ Verify cutover successful
☐ Monitor for 24-48 hours
☐ Test from external locations
☐ Keep source running (7 days)
☐ Update documentation
☐ Close maintenance window
☐ Notify stakeholders of completion

Week After:
☐ Final verification
☐ Decommission source (if all good)
☐ Update disaster recovery plan
☐ Celebrate cost savings! 🎉
```

### C. Quick Reference Commands

```bash
# Export only
./export-data.sh --host source.example.com --output-dir ./export

# Import only
./import-data.sh --input-dir ./export --target-host localhost

# Validate only
./validate-migration.sh --export-dir ./export

# Full migration
./migrate.sh --source-host source.example.com --interactive

# Dry run (test without changes)
./migrate.sh --source-host source.example.com --dry-run

# Check Docker status
docker-compose ps
docker stats

# View logs
docker-compose logs -f ghost-webapi

# Restart service
docker-compose restart ghost-webapi

# Stop everything
docker-compose down

# Start everything
docker-compose up -d
```

---

**Last Updated:** February 2026

**Version:** 1.0.0

**Maintainer:** Ghost Platform Team

For questions or issues, please open a GitHub issue or contact the team.
