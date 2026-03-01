# Disaster Recovery Runbook - Ghost Platform

**Version:** 1.0  
**Last Updated:** 2026-02-03  
**Owner:** Platform Engineering & Database Team  
**Classification:** CONFIDENTIAL

## Table of Contents

1. [Overview](#overview)
2. [Disaster Scenarios](#disaster-scenarios)
3. [Recovery Procedures](#recovery-procedures)
4. [Backup Strategy](#backup-strategy)
5. [Recovery Testing](#recovery-testing)
6. [Emergency Contacts](#emergency-contacts)

---

## Overview

This runbook covers disaster recovery procedures for the Ghost Platform, including complete data center failures, data corruption, and catastrophic system failures.

### Key Metrics

- **Recovery Time Objective (RTO):** 4 hours
- **Recovery Point Objective (RPO):** 15 minutes
- **Backup Frequency:** Every 4 hours + transaction logs
- **Backup Retention:** 30 days full, 90 days compressed

### Disaster Classification

| Level | Description | RTO | RPO | Example |
|-------|-------------|-----|-----|---------|
| DR-1 | Complete region failure | 4 hours | 15 min | AWS region down |
| DR-2 | Data center failure | 2 hours | 15 min | Data center fire |
| DR-3 | Database cluster failure | 1 hour | 5 min | All DB nodes down |
| DR-4 | Data corruption | 30 min | 15 min | Corrupted tables |
| DR-5 | Ransomware/Security | Immediate | Last clean backup | Crypto attack |

---

## Disaster Scenarios

### Scenario DR-1: Complete Region Failure

**Trigger Conditions:**
- AWS/GCP/Azure region completely unavailable
- Multiple availability zones affected
- Expected recovery time > 4 hours

**Impact:**
- Complete service outage
- All users affected
- No automatic failover possible

**Recovery Strategy:** Failover to DR region

---

### Scenario DR-2: Data Center Failure

**Trigger Conditions:**
- Single data center/AZ unavailable
- Hardware failure affecting multiple nodes
- Network partition

**Impact:**
- Partial service degradation
- Some availability zones still functional

**Recovery Strategy:** Failover to healthy AZ

---

### Scenario DR-3: Database Cluster Failure

**Trigger Conditions:**
- Primary and replica databases down
- PostgreSQL cluster unrecoverable
- Data corruption detected

**Impact:**
- Complete application failure
- Data access unavailable

**Recovery Strategy:** Restore from backup

---

### Scenario DR-4: Data Corruption

**Trigger Conditions:**
- Corrupted database tables
- Application bug caused data loss
- Accidental deletion

**Impact:**
- Specific data unavailable or incorrect
- Application may be partially functional

**Recovery Strategy:** Point-in-time recovery (PITR)

---

### Scenario DR-5: Ransomware/Security Breach

**Trigger Conditions:**
- Ransomware detected
- Unauthorized data access
- Compromised credentials

**Impact:**
- Potential data loss
- Security compromise
- Legal/compliance implications

**Recovery Strategy:** Restore from clean backup + forensics

---

## Recovery Procedures

### DR-1: Complete Region Failover

#### Prerequisites

```bash
# Verify DR environment exists
export DR_REGION="us-west-2"
export PRIMARY_REGION="us-east-1"

# Test DR environment health
aws eks describe-cluster --name ghost-dr --region $DR_REGION

# Verify backup replication
aws s3 ls s3://ghost-backups-dr/latest/
```

#### Step 1: Declare Disaster (T+0 minutes)

```bash
#!/bin/bash
# disaster-declare.sh

cat > disaster-$(date +%Y%m%d-%H%M).log << EOF
=================================================
DISASTER RECOVERY DECLARED
=================================================
Scenario: Complete Region Failure
Primary Region: $PRIMARY_REGION
DR Region: $DR_REGION
Declared By: $(whoami)
Declared At: $(date -Iseconds)
=================================================
EOF

# Notify team
curl -X POST $SLACK_WEBHOOK_URL \
  -H 'Content-Type: application/json' \
  -d '{
    "text": "🚨 DISASTER RECOVERY INITIATED 🚨\nRegion Failure: '"$PRIMARY_REGION"'\nFailover to: '"$DR_REGION"'",
    "channel": "#incidents"
  }'

# Update status page
curl -X POST https://api.statuspage.io/v1/incidents \
  -H "Authorization: OAuth $STATUSPAGE_TOKEN" \
  -d '{
    "incident": {
      "name": "Major Service Disruption - DR Activated",
      "status": "investigating",
      "impact": "critical"
    }
  }'
```

#### Step 2: Validate DR Environment (T+5 minutes)

```bash
# Switch to DR region
export AWS_REGION=$DR_REGION
export KUBECONFIG=~/.kube/config-dr

# Check cluster health
kubectl cluster-info
kubectl get nodes
kubectl get pods -n ghost

# Verify database replica
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c "SELECT pg_is_in_recovery();"
# Should return 't' (true) - this is a replica

# Check backup availability
aws s3 ls s3://ghost-backups-dr/latest/ | tail -5
```

#### Step 3: Promote DR Database (T+10 minutes)

```bash
# Promote replica to primary
kubectl exec -it postgres-0 -n ghost -- bash

# Inside the pod:
# Stop PostgreSQL
su - postgres -c "pg_ctl stop -D /var/lib/postgresql/data"

# Promote replica
su - postgres -c "pg_ctl promote -D /var/lib/postgresql/data"

# Verify promotion
psql -U ghost -c "SELECT pg_is_in_recovery();"
# Should return 'f' (false) - now a primary

# Exit pod
exit

# Update connection strings to point to new primary
kubectl set env deployment/ghost-api -n ghost \
  DATABASE_HOST=postgres.ghost.svc.cluster.local
```

#### Step 4: Update DNS/Load Balancer (T+20 minutes)

```bash
# Update Route53 to point to DR region
aws route53 change-resource-record-sets \
  --hosted-zone-id $HOSTED_ZONE_ID \
  --change-batch '{
    "Changes": [{
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "api.ghost.example.com",
        "Type": "CNAME",
        "TTL": 60,
        "ResourceRecords": [{"Value": "'"$DR_LB_DNS"'"}]
      }
    }]
  }'

# Verify DNS propagation
watch -n 5 'dig api.ghost.example.com +short'

# Update Cloudflare (if used)
curl -X PUT "https://api.cloudflare.com/client/v4/zones/$CF_ZONE_ID/dns_records/$CF_RECORD_ID" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "CNAME",
    "name": "api.ghost.example.com",
    "content": "'"$DR_LB_DNS"'",
    "ttl": 60
  }'
```

#### Step 5: Verify Services (T+30 minutes)

```bash
# Check all pods are running
kubectl get pods -n ghost -o wide

# Verify health endpoints
curl -f https://api.ghost.example.com/health

# Check database connectivity
kubectl exec -it $(kubectl get pod -l app=ghost-api -n ghost -o jsonpath='{.items[0].metadata.name}') -n ghost -- \
  node -e "const pg = require('pg'); const client = new pg.Client(process.env.DATABASE_URL); client.connect().then(() => { console.log('✓ Connected'); client.end(); }).catch(console.error);"

# Verify Redis
kubectl exec -it redis-0 -n ghost -- redis-cli ping

# Verify RabbitMQ
curl -u admin:$RABBITMQ_PASS http://localhost:15672/api/healthchecks/node
```

#### Step 6: Enable Application (T+40 minutes)

```bash
# Scale up application
kubectl scale deployment ghost-api -n ghost --replicas=3
kubectl scale deployment ghost-worker -n ghost --replicas=2

# Wait for rollout
kubectl rollout status deployment/ghost-api -n ghost
kubectl rollout status deployment/ghost-worker -n ghost

# Verify application logs
kubectl logs -l app=ghost-api -n ghost --tail=50 | grep -i error
```

#### Step 7: Smoke Tests (T+50 minutes)

```bash
# Run automated smoke tests
cd tests/smoke
npm test -- --env=production

# Manual critical path testing
# 1. User Login
curl -X POST https://api.ghost.example.com/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"test123"}'

# 2. Search Jobs
TOKEN="<jwt-from-login>"
curl -H "Authorization: Bearer $TOKEN" \
  "https://api.ghost.example.com/jobs?q=engineer&limit=10"

# 3. Get User Profile
curl -H "Authorization: Bearer $TOKEN" \
  "https://api.ghost.example.com/users/me"

# 4. Check job details
curl "https://api.ghost.example.com/jobs/12345"
```

#### Step 8: Monitor & Update (T+60 minutes)

```bash
# Enable monitoring alerts
curl -X DELETE http://alertmanager:9093/api/v1/silence/$SILENCE_ID

# Update status page
curl -X PATCH https://api.statuspage.io/v1/incidents/$INCIDENT_ID \
  -d '{"incident": {"status": "monitoring", "message": "Services restored in DR region"}}'

# Monitor key metrics
watch -n 30 'curl -s http://prometheus:9090/api/v1/query?query=up{job="ghost-api"} | jq'

# Check error rates
curl 'http://prometheus:9090/api/v1/query?query=rate(http_requests_total{code=~"5.."}[5m])'
```

#### Step 9: Post-Recovery (T+4 hours)

```bash
# Document recovery
cat >> disaster-$(date +%Y%m%d-%H%M).log << EOF

Recovery Complete
=================
Completed At: $(date -Iseconds)
Duration: [Calculate from start]
Services Status: OPERATIONAL
DR Region: $DR_REGION

Verification:
- All pods running: $(kubectl get pods -n ghost --no-headers | wc -l)
- Health checks: PASS
- Smoke tests: PASS
- Monitoring: ACTIVE

Next Steps:
- Monitor for 24 hours
- Plan primary region recovery
- Schedule post-mortem
EOF

# Notify completion
curl -X POST $SLACK_WEBHOOK_URL \
  -d '{"text":"✅ Disaster recovery complete. Services operational in DR region."}'
```

---

### DR-3: Database Cluster Failure - Full Restore

#### Step 1: Assess Damage (T+0 minutes)

```bash
# Check database status
kubectl get pod postgres-0 -n ghost
kubectl logs postgres-0 -n ghost --tail=100

# Try to connect
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c "SELECT 1"

# Check data directory
kubectl exec -it postgres-0 -n ghost -- ls -lah /var/lib/postgresql/data/

# Verify backup availability
aws s3 ls s3://ghost-backups/postgres/ | tail -10
```

#### Step 2: Prepare for Restore (T+5 minutes)

```bash
# Scale down application
kubectl scale deployment ghost-api -n ghost --replicas=0
kubectl scale deployment ghost-worker -n ghost --replicas=0

# Verify no connections
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT count(*) FROM pg_stat_activity WHERE datname = 'ghost';"

# Backup current state (even if corrupted)
kubectl exec -it postgres-0 -n ghost -- bash -c \
  "pg_dump ghost -U ghost -F c > /tmp/corrupted-$(date +%Y%m%d-%H%M).dump"
```

#### Step 3: Download Latest Backup (T+10 minutes)

```bash
# Find latest backup
LATEST_BACKUP=$(aws s3 ls s3://ghost-backups/postgres/ | sort | tail -1 | awk '{print $4}')
echo "Latest backup: $LATEST_BACKUP"

# Download backup
aws s3 cp s3://ghost-backups/postgres/$LATEST_BACKUP /tmp/

# Download WAL files for PITR (if available)
aws s3 sync s3://ghost-backups/postgres/wal/ /tmp/wal/

# Copy backup to pod
kubectl cp /tmp/$LATEST_BACKUP ghost/postgres-0:/tmp/restore.dump
```

#### Step 4: Restore Database (T+15 minutes)

```bash
# Connect to database pod
kubectl exec -it postgres-0 -n ghost -- bash

# Drop and recreate database
su - postgres
psql -U postgres -c "DROP DATABASE IF EXISTS ghost;"
psql -U postgres -c "CREATE DATABASE ghost OWNER ghost;"

# Restore from backup
pg_restore -U ghost -d ghost -v --no-owner --no-acl /tmp/restore.dump

# If PITR is needed, restore WAL files
# cp /tmp/wal/* /var/lib/postgresql/data/pg_wal/

# Exit pod
exit
exit
```

#### Step 5: Verify Restore (T+30 minutes)

```bash
# Check database size
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT pg_size_pretty(pg_database_size('ghost'));"

# Check table counts
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT schemaname,tablename,n_live_tup FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 10;"

# Verify data integrity
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT COUNT(*) FROM users; SELECT COUNT(*) FROM jobs; SELECT COUNT(*) FROM applications;"

# Check for corruption
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "VACUUM ANALYZE;"

# Run consistency checks
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT * FROM pg_stat_database WHERE datname = 'ghost';"
```

#### Step 6: Restart Application (T+40 minutes)

```bash
# Scale up application
kubectl scale deployment ghost-api -n ghost --replicas=3
kubectl scale deployment ghost-worker -n ghost --replicas=2

# Monitor startup
kubectl logs -f deployment/ghost-api -n ghost

# Verify connections
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT count(*), state FROM pg_stat_activity WHERE datname = 'ghost' GROUP BY state;"
```

---

### DR-4: Point-in-Time Recovery (PITR)

**Use Case:** Recover from accidental deletion or data corruption at specific time.

```bash
# Step 1: Identify target recovery time
TARGET_TIME="2026-02-03 14:30:00+00"
echo "Target recovery time: $TARGET_TIME"

# Step 2: Find appropriate backup
# Get backup before target time
BACKUP_FILE=$(aws s3 ls s3://ghost-backups/postgres/ | \
  awk -v target="$TARGET_TIME" '$1" "$2 < target {latest=$0} END {print latest}' | \
  awk '{print $4}')
echo "Using backup: $BACKUP_FILE"

# Step 3: Download backup and WAL files
aws s3 cp s3://ghost-backups/postgres/$BACKUP_FILE /tmp/
aws s3 sync s3://ghost-backups/postgres/wal/ /tmp/wal/

# Step 4: Create recovery.conf
cat > /tmp/recovery.conf << EOF
restore_command = 'cp /tmp/wal/%f %p'
recovery_target_time = '$TARGET_TIME'
recovery_target_action = 'promote'
EOF

# Step 5: Restore to new database
kubectl exec -it postgres-0 -n ghost -- bash

# Inside pod:
su - postgres
createdb ghost_pitr
pg_restore -U ghost -d ghost_pitr /tmp/restore.dump

# Copy recovery.conf
cp /tmp/recovery.conf /var/lib/postgresql/data/recovery.conf

# Restart PostgreSQL to apply PITR
pg_ctl restart -D /var/lib/postgresql/data

# Step 6: Verify recovered data
psql -U ghost -d ghost_pitr -c "SELECT MAX(created_at) FROM users;"
# Should show timestamps up to $TARGET_TIME

# Step 7: Switch to recovered database
psql -U postgres -c "ALTER DATABASE ghost RENAME TO ghost_corrupted;"
psql -U postgres -c "ALTER DATABASE ghost_pitr RENAME TO ghost;"
```

---

## Backup Strategy

### Automated Backup Schedule

```yaml
# Backup Schedule Configuration
backups:
  full_backup:
    frequency: "0 */4 * * *"  # Every 4 hours
    retention: 30  # days
    format: "custom"  # PostgreSQL custom format
    compression: true
    
  incremental_backup:
    frequency: "*/15 * * * *"  # Every 15 minutes
    type: "WAL"  # Write-Ahead Logs
    retention: 7  # days
    
  snapshot_backup:
    frequency: "0 2 * * *"  # Daily at 2 AM
    type: "EBS"  # Volume snapshot
    retention: 14  # days
    
  offsite_backup:
    frequency: "0 3 * * *"  # Daily at 3 AM
    destination: "S3 Glacier"
    retention: 90  # days
    encryption: true
```

### Backup Verification

```bash
# Automated backup verification script
#!/bin/bash
# backup-verify.sh

BACKUP_FILE="$1"

echo "Verifying backup: $BACKUP_FILE"

# Download backup
aws s3 cp s3://ghost-backups/postgres/$BACKUP_FILE /tmp/verify.dump

# Test restore to temporary database
kubectl exec -it postgres-0 -n ghost -- bash << 'EOFPOD'
su - postgres -c "createdb verify_test"
su - postgres -c "pg_restore -U ghost -d verify_test /tmp/verify.dump"

# Run verification queries
psql -U ghost -d verify_test -c "SELECT COUNT(*) FROM users;" > /tmp/verify_result.txt
psql -U ghost -d verify_test -c "SELECT COUNT(*) FROM jobs;" >> /tmp/verify_result.txt

# Cleanup
psql -U postgres -c "DROP DATABASE verify_test;"
EOFPOD

# Check results
kubectl exec -it postgres-0 -n ghost -- cat /tmp/verify_result.txt

echo "✓ Backup verification complete"
```

### Backup Monitoring

```yaml
# Prometheus alerting rules for backups
groups:
  - name: backup_alerts
    rules:
      - alert: BackupFailed
        expr: ghost_backup_status{status="failed"} == 1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Backup failed"
          description: "Last backup failed for {{ $labels.backup_type }}"
      
      - alert: BackupDelayed
        expr: time() - ghost_backup_last_success_timestamp > 14400  # 4 hours
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Backup delayed"
          description: "No successful backup in last 4 hours"
      
      - alert: BackupSizeAnomaly
        expr: |
          abs(ghost_backup_size_bytes - avg_over_time(ghost_backup_size_bytes[7d])) 
          / avg_over_time(ghost_backup_size_bytes[7d]) > 0.3
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Backup size anomaly"
          description: "Backup size differs by >30% from 7-day average"
```

---

## Recovery Testing

### Monthly DR Drill Schedule

```bash
# DR Drill Procedure (Non-Production)
# Schedule: First Saturday of each month, 10 AM UTC

# 1. Prepare test environment
export TEST_ENV="dr-test"
kubectl config use-context $TEST_ENV

# 2. Simulate disaster
kubectl delete namespace ghost --wait=true

# 3. Execute recovery procedure
bash disaster-recovery.sh --scenario DR-3 --test-mode

# 4. Verify recovery
bash recovery-verification.sh

# 5. Document results
cat > dr-drill-$(date +%Y%m).md << EOF
# DR Drill Report - $(date +%Y-%m)

## Scenario
DR-3: Database Cluster Failure

## Results
- RTO Target: 1 hour
- RTO Actual: [measured time]
- RPO Target: 15 minutes  
- RPO Actual: [measured data loss]

## Issues Encountered
[List any issues]

## Action Items
[List improvements needed]

## Sign-off
Tested by: [name]
Date: $(date -Iseconds)
EOF
```

### Backup Restore Test

```bash
# Weekly backup restore test (automated)
# Runs every Sunday at 3 AM

#!/bin/bash
# weekly-restore-test.sh

echo "=== Weekly Backup Restore Test ==="
echo "Started: $(date -Iseconds)"

# Get last week's backup
BACKUP_FILE=$(aws s3 ls s3://ghost-backups/postgres/ | \
  awk -v cutoff="$(date -d '7 days ago' +%Y-%m-%d)" \
  '$1" "$2 > cutoff {print $4}' | head -1)

echo "Testing backup: $BACKUP_FILE"

# Restore to test database
kubectl exec -it postgres-0 -n ghost-test -- bash -c "
  createdb ghost_test
  pg_restore -U ghost -d ghost_test /backups/$BACKUP_FILE
  
  # Verify record counts
  psql -U ghost -d ghost_test -c 'SELECT COUNT(*) FROM users;' -t
  psql -U ghost -d ghost_test -c 'SELECT COUNT(*) FROM jobs;' -t
  
  # Cleanup
  dropdb ghost_test
"

echo "✓ Test complete: $(date -Iseconds)"

# Send results to monitoring
curl -X POST http://prometheus-pushgateway:9091/metrics/job/backup_test \
  -d "backup_restore_test_success 1"
```

---

## Emergency Contacts

### Disaster Recovery Team

| Role | Primary | Secondary | Phone | Availability |
|------|---------|-----------|-------|--------------|
| DR Commander | John Doe | Jane Smith | +1-555-0100 | 24/7 |
| Database Lead | Charlie Brown | Diana Prince | +1-555-0102 | 24/7 |
| Infrastructure Lead | Alice Johnson | Bob Wilson | +1-555-0101 | 24/7 |
| Security Lead | Eve Martinez | Frank Castle | +1-555-0103 | On-call |

### Vendor Contacts

- **AWS Support:** Enterprise Support Portal (24/7)
- **Database Consulting:** PostgreSQL Experts (+1-555-0200)
- **Backup Vendor:** Backup Solutions Inc. (+1-555-0300)

### Communication Channels

- **DR War Room:** meet.google.com/disaster-recovery
- **Slack Channel:** #disaster-recovery
- **Status Page:** https://status.ghost.example.com
- **Incident Hotline:** +1-555-DR-GHOST

---

## Appendix

### Pre-Disaster Checklist

```
DISASTER PREPAREDNESS CHECKLIST
================================

Infrastructure:
[ ] DR environment deployed and tested
[ ] Automated backups configured
[ ] Backup monitoring enabled
[ ] Offsite backup replication confirmed
[ ] DNS failover tested
[ ] Load balancer failover tested

Documentation:
[ ] Runbooks up to date
[ ] Contact list current
[ ] Access credentials documented
[ ] Architecture diagrams current

Testing:
[ ] Monthly DR drills completed
[ ] Weekly backup restore tests passing
[ ] Quarterly full region failover test
[ ] RTO/RPO metrics within targets

Team:
[ ] On-call rotation established
[ ] Team trained on procedures
[ ] Emergency communication channels tested
[ ] Vendor support agreements active
```

### Recovery Verification Checklist

```
POST-RECOVERY VERIFICATION
==========================

Infrastructure:
[ ] All pods running and healthy
[ ] Database connections successful
[ ] Cache (Redis) operational
[ ] Message queue (RabbitMQ) operational
[ ] Monitoring and alerting active

Data Integrity:
[ ] Record counts verified
[ ] Recent data present
[ ] No data corruption detected
[ ] Database constraints valid

Application:
[ ] Health endpoints responding
[ ] Authentication working
[ ] Critical user flows functional
[ ] API response times normal
[ ] Error rates < 0.5%

Monitoring:
[ ] Prometheus scraping all targets
[ ] Grafana dashboards showing data
[ ] Alerts configured correctly
[ ] Logs being collected

Communication:
[ ] Status page updated
[ ] Stakeholders notified
[ ] Post-mortem scheduled
[ ] DR drill report completed
```

### Useful Scripts

```bash
# Quick disaster assessment
cat > assess-disaster.sh << 'EOF'
#!/bin/bash
echo "=== Disaster Assessment ==="

# Check primary region
echo "Primary Region Status:"
aws eks describe-cluster --name ghost-prod --region us-east-1 2>&1 | \
  grep -q "active" && echo "✓ Available" || echo "✗ UNAVAILABLE"

# Check database
echo "Database Status:"
kubectl exec -it postgres-0 -n ghost -- pg_isready -U ghost 2>&1 | \
  grep -q "accepting" && echo "✓ Available" || echo "✗ UNAVAILABLE"

# Check application
echo "Application Status:"
curl -sf https://api.ghost.example.com/health >/dev/null 2>&1 && \
  echo "✓ Available" || echo "✗ UNAVAILABLE"

# Check backup freshness
echo "Last Backup:"
aws s3 ls s3://ghost-backups/postgres/ | tail -1
EOF

chmod +x assess-disaster.sh
```

---

**Document Classification:** CONFIDENTIAL - DR Team Only  
**Review Schedule:** Quarterly  
**Next Review Date:** 2026-05-03  
**Last DR Drill:** [Date]  
**Next DR Drill:** [First Saturday of next month]

**Version History:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-03 | Platform Team | Initial version |
