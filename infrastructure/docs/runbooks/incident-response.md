# Incident Response Runbook - Ghost Platform

**Version:** 1.0  
**Last Updated:** 2026-02-03  
**Owner:** Platform Engineering & SRE Team  
**Classification:** CONFIDENTIAL

## Table of Contents

1. [Overview](#overview)
2. [Incident Classification](#incident-classification)
3. [Response Procedures](#response-procedures)
4. [Incident Scenarios](#incident-scenarios)
5. [Communication Guidelines](#communication-guidelines)
6. [Post-Incident Activities](#post-incident-activities)

---

## Overview

This runbook provides standardized procedures for responding to incidents affecting the Ghost Platform. It ensures consistent, efficient response and minimizes impact to users and business operations.

### Key Principles

1. **User Impact First**: Prioritize restoring service over root cause analysis
2. **Clear Communication**: Keep stakeholders informed with regular updates
3. **Document Everything**: Record all actions and observations
4. **Learn and Improve**: Conduct blameless post-mortems

### Incident Lifecycle

```
Detection → Triage → Response → Resolution → Post-Mortem → Prevention
```

---

## Incident Classification

### Severity Levels

#### SEV-1 (Critical) - Response Time: 15 minutes

**Definition:** Complete service outage or critical functionality unavailable affecting all users.

**Examples:**
- Ghost Platform completely down
- Database unavailable
- Critical security breach
- Data loss or corruption
- Payment processing failure

**Response Requirements:**
- Immediate page to on-call engineer
- Engage incident commander
- Start war room bridge
- Update status page immediately
- Executive notification required

#### SEV-2 (High) - Response Time: 30 minutes

**Definition:** Major functionality degraded, affecting significant subset of users.

**Examples:**
- API error rate > 10%
- Search functionality unavailable
- Authentication issues
- High latency (p95 > 5s)
- Single region outage

**Response Requirements:**
- Page on-call engineer
- Update status page
- Regular status updates
- Notify stakeholders

#### SEV-3 (Medium) - Response Time: 2 hours

**Definition:** Minor functionality issue, affecting small subset of users or non-critical features.

**Examples:**
- Non-critical feature unavailable
- Performance degradation (p95 > 2s)
- Elevated error rate (2-5%)
- Minor UI issues

**Response Requirements:**
- Create incident ticket
- Assign to on-call
- Email notification
- Fix within SLA

#### SEV-4 (Low) - Response Time: Next business day

**Definition:** Minor issue, cosmetic problems, or single user impact.

**Examples:**
- UI cosmetic issues
- Documentation errors
- Non-critical alerts
- Single user reports

**Response Requirements:**
- Create support ticket
- Track in backlog
- Fix in regular sprint

---

## Response Procedures

### Phase 1: Detection & Alert (0-5 minutes)

#### Automatic Detection

```bash
# Alerts are configured in Prometheus/Alertmanager
# Check active alerts
curl http://localhost:9090/api/v1/alerts | jq '.data.alerts[] | select(.state=="firing")'

# Common alert sources:
# - Prometheus alerts → PagerDuty → SMS/Voice
# - Uptime monitors → PagerDuty
# - Log-based alerts → Loki
# - User reports → Support system
```

#### Manual Detection

```bash
# Quick health check script
#!/bin/bash
# save as: incident-detect.sh

echo "=== Ghost Platform Health Check ==="
echo "Timestamp: $(date -Iseconds)"

# Check API health
if curl -sf https://api.ghost.example.com/health > /dev/null; then
    echo "✓ API is UP"
else
    echo "✗ API is DOWN - INCIDENT!"
fi

# Check database
if kubectl exec -it postgres-0 -n ghost -- pg_isready -U ghost > /dev/null 2>&1; then
    echo "✓ Database is UP"
else
    echo "✗ Database is DOWN - INCIDENT!"
fi

# Check Redis
if redis-cli ping > /dev/null 2>&1; then
    echo "✓ Redis is UP"
else
    echo "✗ Redis is DOWN - INCIDENT!"
fi

# Check RabbitMQ
if curl -sf http://localhost:15672/api/healthchecks/node > /dev/null; then
    echo "✓ RabbitMQ is UP"
else
    echo "✗ RabbitMQ is DOWN - INCIDENT!"
fi
```

### Phase 2: Initial Triage (5-10 minutes)

#### Triage Checklist

```bash
# 1. Verify the incident
# Run health checks on all components
kubectl get pods -n ghost -o wide
kubectl get events -n ghost --sort-by='.lastTimestamp' | tail -20

# 2. Assess impact
# Check error rates
curl 'http://localhost:9090/api/v1/query?query=rate(http_requests_total{code=~"5.."}[5m])'

# Check affected users
curl 'http://localhost:9090/api/v1/query?query=count(up{job="ghost-api"}==0)'

# 3. Determine severity (SEV-1 to SEV-4)
# Use classification guide above

# 4. Create incident record
cat > incident-$(date +%Y%m%d-%H%M).md << EOF
# Incident Record

**Incident ID:** INC-$(date +%Y%m%d-%H%M)
**Severity:** SEV-?
**Status:** INVESTIGATING
**Impact:** [Describe user impact]
**Started:** $(date -Iseconds)

## Timeline
- $(date -Iseconds): Incident detected via [alert/user-report]
- $(date -Iseconds): On-call engineer paged
- $(date -Iseconds): Triage started

## Investigation Notes
[Add notes here]
EOF
```

#### Triage Decision Tree

```
Is service completely down?
├── YES → SEV-1
│   ├── Page incident commander
│   ├── Start war room
│   └── Update status page
└── NO → Check error rate
    ├── > 10% → SEV-2
    ├── 2-10% → SEV-3
    └── < 2% → SEV-4
```

### Phase 3: Incident Response (10-60 minutes)

#### SEV-1 Response Procedure

```bash
# STEP 1: Declare incident (Immediate)
echo "DECLARING SEV-1 INCIDENT"
echo "Incident Commander: [Your Name]"
echo "Started: $(date -Iseconds)"

# Update status page
curl -X POST https://api.statuspage.io/v1/incidents \
  -H "Authorization: OAuth YOUR_TOKEN" \
  -d '{
    "incident": {
      "name": "Service Disruption - Investigation",
      "status": "investigating",
      "impact": "critical",
      "message": "We are investigating reports of service unavailability."
    }
  }'

# STEP 2: Start war room
# Send to #incidents Slack channel:
echo "@here SEV-1 INCIDENT DECLARED - Ghost Platform Down"
echo "War Room: meet.google.com/incident-$(date +%Y%m%d)"
echo "Incident Commander: [Your Name]"
echo "Status: INVESTIGATING"

# STEP 3: Quick diagnostics
# Run automated diagnostics
bash incident-detect.sh

# Check recent deployments
kubectl rollout history deployment/ghost-api -n ghost

# Check recent changes
git log --oneline --since="2 hours ago"

# STEP 4: Immediate mitigation
# If recent deployment caused issue:
kubectl rollout undo deployment/ghost-api -n ghost

# If infrastructure issue:
kubectl get nodes
kubectl describe node <node-name>

# If database issue:
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT pid, now() - query_start as duration, query 
   FROM pg_stat_activity 
   WHERE state = 'active' AND query_start < now() - interval '5 minutes';"

# Kill long-running queries if needed
# kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
#   "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE ..."

# STEP 5: Monitor recovery
watch -n 5 'kubectl get pods -n ghost'

# STEP 6: Verify service restoration
curl https://api.ghost.example.com/health
curl 'http://localhost:9090/api/v1/query?query=up{job="ghost-api"}'

# STEP 7: Update status page
curl -X PATCH https://api.statuspage.io/v1/incidents/[incident-id] \
  -d '{"incident": {"status": "monitoring", "message": "Service has been restored. Monitoring for stability."}}'
```

#### Communication Template - Initial Alert

```
Subject: [SEV-1] Ghost Platform Service Disruption

Status: INVESTIGATING
Impact: All users unable to access Ghost Platform
Start Time: [timestamp]
Next Update: In 15 minutes

We are aware of an issue affecting the Ghost Platform and are 
actively investigating. We will provide updates every 15 minutes.

Status Page: https://status.ghost.example.com
War Room: [if applicable]

- Incident Commander: [Name]
- Platform Engineering Team
```

#### Communication Template - Updates

```
Subject: [SEV-1] UPDATE: Ghost Platform Service Disruption

Status: IDENTIFIED / MONITORING / RESOLVED
Impact: [Current impact description]
Start Time: [timestamp]
Duration: [X minutes]

UPDATE:
We have [identified the root cause / implemented a fix / restored service].
[Detailed description of actions taken]

Current Status:
- API: [UP/DOWN]
- Database: [UP/DOWN]
- Services Affected: [list]

Next Update: In [15/30] minutes

- Incident Commander: [Name]
```

### Phase 4: Resolution & Recovery (Variable)

#### Standard Recovery Procedures

**Scenario: Application Rollback**

```bash
# 1. Identify last good version
kubectl rollout history deployment/ghost-api -n ghost

# 2. Rollback to previous version
kubectl rollout undo deployment/ghost-api -n ghost

# 3. Wait for rollout to complete
kubectl rollout status deployment/ghost-api -n ghost --timeout=10m

# 4. Verify health
kubectl get pods -n ghost -l app=ghost-api
curl https://api.ghost.example.com/health

# 5. Monitor metrics
# Check Grafana dashboard for error rates, latency
```

**Scenario: Database Recovery**

```bash
# 1. Check database status
kubectl exec -it postgres-0 -n ghost -- pg_isready -U ghost

# 2. If database is down, check pod status
kubectl describe pod postgres-0 -n ghost

# 3. Check persistent volume
kubectl get pvc -n ghost
kubectl describe pvc postgres-data -n ghost

# 4. If volume issue, restore from backup
# First, scale down app
kubectl scale deployment ghost-api -n ghost --replicas=0

# Restore database (see disaster-recovery.md for details)
kubectl exec -it postgres-0 -n ghost -- bash
pg_restore -U ghost -d ghost -v /backups/latest.dump

# 5. Verify restore
psql -U ghost -c "SELECT COUNT(*) FROM users;"

# 6. Scale up app
kubectl scale deployment ghost-api -n ghost --replicas=3
```

**Scenario: Node Failure**

```bash
# 1. Identify failed node
kubectl get nodes
kubectl describe node <node-name>

# 2. Cordon the node (prevent new pods)
kubectl cordon <node-name>

# 3. Drain the node (evict pods)
kubectl drain <node-name> --ignore-daemonsets --delete-emptydir-data

# 4. Terminate the node (cloud provider)
# AWS
aws ec2 terminate-instances --instance-ids i-1234567890abcdef0

# 5. Auto-scaling will create replacement node
# Wait for new node to join
watch kubectl get nodes

# 6. Verify pods are rescheduled
kubectl get pods -n ghost -o wide
```

---

## Incident Scenarios

### Scenario 1: Complete Service Outage

**Symptoms:**
- All API endpoints returning errors
- Monitoring shows all pods down
- Users cannot access platform

**Diagnosis:**

```bash
# Check pod status
kubectl get pods -n ghost

# Check recent events
kubectl get events -n ghost --sort-by='.lastTimestamp'

# Check node status
kubectl get nodes

# Check image pull status
kubectl describe pod <pod-name> -n ghost | grep -A 10 "Events:"
```

**Common Causes & Solutions:**

1. **Failed Deployment**
   ```bash
   kubectl rollout undo deployment/ghost-api -n ghost
   ```

2. **Image Pull Failure**
   ```bash
   kubectl get pod <pod-name> -n ghost -o yaml | grep image:
   # Fix image tag or pull secret
   ```

3. **Resource Exhaustion**
   ```bash
   kubectl describe nodes | grep -A 5 "Allocated resources"
   # Scale down non-critical services or add nodes
   ```

4. **ConfigMap/Secret Issues**
   ```bash
   kubectl get configmap -n ghost
   kubectl get secret -n ghost
   # Verify and recreate if needed
   ```

### Scenario 2: Database Connection Failures

**Symptoms:**
- API returns 500 errors
- Logs show "connection refused" or "too many clients"
- Database metrics show connection pool exhaustion

**Diagnosis:**

```bash
# Check database pod
kubectl get pod postgres-0 -n ghost
kubectl logs postgres-0 -n ghost --tail=100

# Check connections
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT count(*), state FROM pg_stat_activity GROUP BY state;"

# Check connection pool settings
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SHOW max_connections;"
```

**Solutions:**

1. **Connection Pool Exhausted**
   ```bash
   # Kill idle connections
   kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
     "SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
      WHERE state = 'idle' AND state_change < now() - interval '5 minutes';"
   ```

2. **Increase Connection Limit**
   ```bash
   # Update PostgreSQL config
   kubectl edit configmap postgres-config -n ghost
   # Set max_connections = 200
   
   # Restart database
   kubectl rollout restart statefulset postgres -n ghost
   ```

3. **Application Connection Leak**
   ```bash
   # Check application logs
   kubectl logs -l app=ghost-api -n ghost | grep "connection"
   
   # Restart application pods
   kubectl rollout restart deployment ghost-api -n ghost
   ```

### Scenario 3: High Memory Usage / OOM Kills

**Symptoms:**
- Pods restarting frequently
- OOMKilled status in pod events
- Memory alerts firing

**Diagnosis:**

```bash
# Check pod restarts
kubectl get pods -n ghost -o wide

# Check events for OOM kills
kubectl get events -n ghost | grep OOM

# Check memory usage
kubectl top pods -n ghost

# Check memory limits
kubectl get pod <pod-name> -n ghost -o yaml | grep -A 5 resources:
```

**Solutions:**

1. **Increase Memory Limits**
   ```bash
   kubectl edit deployment ghost-api -n ghost
   # Update resources.limits.memory
   ```

2. **Memory Leak Investigation**
   ```bash
   # Get heap dump (Node.js)
   kubectl exec -it <pod-name> -n ghost -- \
     node --expose-gc --max-old-space-size=512 --heap-prof
   
   # Download heap snapshot
   kubectl cp <pod-name>:/tmp/heap.heapsnapshot ./heap.heapsnapshot -n ghost
   ```

3. **Scale Horizontally**
   ```bash
   kubectl scale deployment ghost-api -n ghost --replicas=5
   ```

### Scenario 4: High CPU Usage

**Symptoms:**
- API response times degraded
- CPU alerts firing
- Pods consuming > 80% CPU

**Diagnosis:**

```bash
# Check CPU usage
kubectl top pods -n ghost

# Check CPU limits
kubectl get pod <pod-name> -n ghost -o yaml | grep -A 5 resources:

# Check for CPU throttling
kubectl exec -it <pod-name> -n ghost -- cat /sys/fs/cgroup/cpu/cpu.stat
```

**Solutions:**

1. **Scale Horizontally**
   ```bash
   kubectl scale deployment ghost-api -n ghost --replicas=5
   ```

2. **Increase CPU Limits**
   ```bash
   kubectl edit deployment ghost-api -n ghost
   # Update resources.limits.cpu
   ```

3. **Identify CPU-intensive Processes**
   ```bash
   kubectl exec -it <pod-name> -n ghost -- top
   
   # Get Node.js CPU profile
   kubectl exec -it <pod-name> -n ghost -- \
     node --prof /app/server.js
   ```

### Scenario 5: Cache (Redis) Failure

**Symptoms:**
- Increased database load
- Slower API response times
- Redis unavailable errors in logs

**Diagnosis:**

```bash
# Check Redis pod
kubectl get pod redis-0 -n ghost
kubectl logs redis-0 -n ghost

# Test Redis connection
kubectl exec -it redis-0 -n ghost -- redis-cli ping

# Check Redis memory
kubectl exec -it redis-0 -n ghost -- redis-cli info memory
```

**Solutions:**

1. **Restart Redis**
   ```bash
   kubectl rollout restart statefulset redis -n ghost
   ```

2. **Clear Redis Cache** (if corrupted)
   ```bash
   kubectl exec -it redis-0 -n ghost -- redis-cli FLUSHALL
   ```

3. **Increase Redis Memory**
   ```bash
   kubectl edit statefulset redis -n ghost
   # Update resources.limits.memory
   ```

### Scenario 6: Message Queue (RabbitMQ) Issues

**Symptoms:**
- Queue depth increasing
- No consumers processing messages
- RabbitMQ memory/disk alarms

**Diagnosis:**

```bash
# Check RabbitMQ pod
kubectl get pod rabbitmq-0 -n ghost
kubectl logs rabbitmq-0 -n ghost

# Check queue depth
curl -u admin:password http://localhost:15672/api/queues | \
  jq '.[] | {name, messages, consumers}'

# Check alarms
curl -u admin:password http://localhost:15672/api/health/checks/alarms
```

**Solutions:**

1. **Restart Consumers**
   ```bash
   kubectl rollout restart deployment ghost-worker -n ghost
   ```

2. **Clear Queue** (if stuck)
   ```bash
   kubectl exec -it rabbitmq-0 -n ghost -- rabbitmqctl purge_queue jobs
   ```

3. **Increase RabbitMQ Resources**
   ```bash
   kubectl edit statefulset rabbitmq -n ghost
   # Update resources and disk limits
   ```

### Scenario 7: Network Issues

**Symptoms:**
- Intermittent connection failures
- Timeouts between services
- DNS resolution failures

**Diagnosis:**

```bash
# Test DNS resolution
kubectl exec -it <pod-name> -n ghost -- nslookup postgres
kubectl exec -it <pod-name> -n ghost -- nslookup redis

# Test network connectivity
kubectl exec -it <pod-name> -n ghost -- nc -zv postgres 5432
kubectl exec -it <pod-name> -n ghost -- nc -zv redis 6379

# Check network policies
kubectl get networkpolicies -n ghost

# Check service endpoints
kubectl get endpoints -n ghost
```

**Solutions:**

1. **Restart CoreDNS**
   ```bash
   kubectl rollout restart deployment coredns -n kube-system
   ```

2. **Review Network Policies**
   ```bash
   kubectl get networkpolicy -n ghost -o yaml
   # Temporarily delete to test
   kubectl delete networkpolicy <policy-name> -n ghost
   ```

3. **Check Service Configuration**
   ```bash
   kubectl get svc -n ghost
   kubectl describe svc postgres -n ghost
   ```

---

## Communication Guidelines

### Status Page Updates

**Investigating:**
```
We are investigating reports of [issue description]. 
Users may experience [impact description].
```

**Identified:**
```
We have identified the root cause as [brief description].
Our team is working on implementing a fix.
```

**Monitoring:**
```
The issue has been resolved and we are monitoring the system for stability.
All services should be operating normally.
```

**Resolved:**
```
This incident has been resolved. The root cause was [description].
Services are fully operational. We apologize for any inconvenience.
```

### Internal Communication

**Slack Templates:**

```
# Initial Alert
🚨 SEV-1 INCIDENT DECLARED 🚨
Component: [Ghost API / Database / etc]
Impact: [User impact description]
Status: INVESTIGATING
Incident Commander: @username
War Room: [link]
Status: https://status.ghost.example.com

---

# Update Template
📢 INCIDENT UPDATE - [HH:MM]
Status: [INVESTIGATING / IDENTIFIED / MONITORING / RESOLVED]
Current Impact: [description]

Actions Taken:
- [Action 1]
- [Action 2]

Next Steps:
- [Next step 1]
- [Next step 2]

Next Update: [time]

---

# Resolution
✅ INCIDENT RESOLVED
Total Duration: [X hours Y minutes]
Root Cause: [brief description]
Resolution: [brief description]

Next Steps:
- Post-mortem scheduled for [date/time]
- Action items to be tracked in [ticket system]

Thank you to everyone involved in the response!
```

### Stakeholder Communication

**Executive Summary Template:**

```
TO: Executive Team
FROM: Platform Engineering
SUBJECT: Incident Summary - [Date]

INCIDENT OVERVIEW
-----------------
Severity: SEV-[1-4]
Duration: [X hours Y minutes]
Start: [timestamp]
End: [timestamp]
Impact: [# of users affected / % of requests failed]

ROOT CAUSE
----------
[1-2 sentence description]

RESOLUTION
----------
[1-2 sentence description of fix]

PREVENTION
----------
[Key action items to prevent recurrence]

BUSINESS IMPACT
---------------
- Revenue Impact: $[amount] (estimated)
- Users Affected: [number]
- Support Tickets: [number]

Full post-mortem will be completed by [date].

Contact: [Incident Commander Name/Email]
```

---

## Post-Incident Activities

### Immediate Post-Resolution (Within 1 hour)

```bash
# 1. Update incident record
cat >> incident-$(date +%Y%m%d-%H%M).md << EOF

## Resolution
- Resolved at: $(date -Iseconds)
- Duration: [calculate from start time]
- Root Cause: [brief description]
- Resolution: [brief description]

## Impact Summary
- Users affected: [estimate]
- Requests failed: [from metrics]
- Revenue impact: [if applicable]
EOF

# 2. Re-enable monitoring
# Un-silence alerts
curl -X DELETE http://localhost:9093/api/v1/silence/[silence-id]

# 3. Verify complete recovery
# Run full health check
bash incident-detect.sh

# Check all metrics are normal
curl 'http://localhost:9090/api/v1/query?query=rate(http_requests_total[5m])'

# 4. Final status page update
curl -X PATCH https://api.statuspage.io/v1/incidents/[incident-id] \
  -d '{"incident": {"status": "resolved"}}'
```

### Post-Mortem Process (Within 48 hours)

#### 1. Schedule Post-Mortem Meeting

- **Attendees:** All responders + key stakeholders
- **Duration:** 60-90 minutes
- **Facilitator:** Incident Commander or SRE Lead
- **Note Taker:** Designated person (not IC)

#### 2. Post-Mortem Document Template

```markdown
# Post-Mortem: [Incident Title]

**Date:** [YYYY-MM-DD]  
**Incident ID:** INC-YYYYMMDD-HHMM  
**Severity:** SEV-[1-4]  
**Duration:** [X hours Y minutes]  
**Author:** [Name]  
**Reviewers:** [Names]  

## Executive Summary

[2-3 sentence summary of incident, impact, and resolution]

## Impact

- **Users Affected:** [number or percentage]
- **Duration:** [start time] to [end time] ([X hours Y minutes])
- **Failed Requests:** [number] ([percentage]% error rate)
- **Revenue Impact:** $[amount] (estimated)
- **Customer Support:** [number] tickets created

## Timeline (All times in UTC)

| Time | Event |
|------|-------|
| 14:23 | Alert fired: HighErrorRate on ghost-api |
| 14:25 | On-call engineer paged |
| 14:28 | Incident declared SEV-2 |
| 14:30 | Initial investigation started |
| 14:35 | Root cause identified: Database connection pool exhausted |
| 14:40 | Mitigation: Killed idle connections |
| 14:42 | Error rate decreased to 5% |
| 14:45 | Increased connection pool size |
| 14:47 | Deployed configuration change |
| 14:50 | Service fully restored |
| 14:55 | Incident upgraded to SEV-1 (retroactively) |
| 15:00 | Monitoring for stability |
| 15:30 | Incident resolved |

## Root Cause Analysis

### What Happened

[Detailed technical explanation of what went wrong]

### Why It Happened

[Underlying causes - technical, process, or organizational]

### Contributing Factors

1. [Factor 1]
2. [Factor 2]
3. [Factor 3]

## Detection

- **Detection Method:** [Alert / User Report / Monitoring]
- **Detection Time:** [Time from start to detection]
- **Time to Triage:** [Time from detection to severity assignment]
- **Time to Resolution:** [Time from detection to resolution]

## What Went Well

1. [Positive aspect 1]
2. [Positive aspect 2]
3. [Positive aspect 3]

## What Went Wrong

1. [Issue 1]
2. [Issue 2]
3. [Issue 3]

## Action Items

| Action | Owner | Priority | Due Date | Status |
|--------|-------|----------|----------|--------|
| Increase default connection pool size | @engineer | P0 | 2026-02-05 | ✅ Done |
| Add connection pool monitoring | @engineer | P1 | 2026-02-10 | 🔄 In Progress |
| Implement circuit breaker | @engineer | P1 | 2026-02-15 | ⏳ Todo |
| Update runbook with learnings | @sre | P2 | 2026-02-12 | ⏳ Todo |
| Connection leak detection | @engineer | P2 | 2026-02-20 | ⏳ Todo |

## Lessons Learned

1. [Lesson 1]
2. [Lesson 2]
3. [Lesson 3]

## Prevention

### Short-term (0-2 weeks)
- [Immediate fix to prevent recurrence]

### Medium-term (2-8 weeks)
- [Improvements to detect earlier or mitigate faster]

### Long-term (2-6 months)
- [Architectural changes or major improvements]

## Appendix

### Relevant Logs

```
[Include relevant log excerpts]
```

### Metrics/Graphs

[Include relevant Grafana screenshots or metric data]

### Related Incidents

- [Link to similar past incidents]

---

**Document Status:** [DRAFT / UNDER REVIEW / APPROVED]  
**Review Date:** [YYYY-MM-DD]  
**Approved By:** [Name, Title]
```

#### 3. Follow-up Actions

```bash
# Create tracking tickets for action items
# Example using GitHub Issues

gh issue create \
  --title "[Post-Incident] Increase database connection pool" \
  --body "From incident INC-20260203-1423. See post-mortem for details." \
  --label "incident-followup,priority-high" \
  --assignee "@engineer"

# Schedule follow-up review
# Add calendar reminder for 2 weeks to review action items
```

### Incident Metrics to Track

```bash
# Create metrics dashboard in Grafana
# Track these KPIs:

# 1. Time to Detect (TTD)
# Time from incident start to detection

# 2. Time to Acknowledge (TTA)
# Time from alert to engineer acknowledgment

# 3. Time to Mitigate (TTM)
# Time from acknowledgment to initial mitigation

# 4. Time to Resolve (TTR)
# Time from acknowledgment to full resolution

# 5. Mean Time to Recovery (MTTR)
# Average TTR across all incidents

# 6. Incident Frequency
# Number of incidents per month by severity

# 7. Repeat Incidents
# Incidents with similar root causes
```

---

## Appendix

### Quick Reference Commands

```bash
# Incident Response Alias Setup
cat >> ~/.bashrc << 'EOF'
# Incident Response Aliases
alias inc-detect='bash ~/scripts/incident-detect.sh'
alias inc-logs='kubectl logs -n ghost --tail=100 -l app=ghost-api'
alias inc-metrics='curl -s http://localhost:9090/api/v1/alerts | jq'
alias inc-pods='kubectl get pods -n ghost -o wide'
alias inc-events='kubectl get events -n ghost --sort-by=.lastTimestamp | tail -20'
alias inc-rollback='kubectl rollout undo deployment/ghost-api -n ghost'
EOF
```

### Emergency Toolbox

```bash
# Create emergency response toolkit
mkdir -p ~/incident-response
cd ~/incident-response

# Download this runbook
curl -O https://github.com/ghost/docs/runbooks/incident-response.md

# Create quick scripts
cat > rollback.sh << 'EOF'
#!/bin/bash
kubectl rollout undo deployment/ghost-api -n ghost
kubectl rollout status deployment/ghost-api -n ghost --timeout=5m
EOF

cat > scale-up.sh << 'EOF'
#!/bin/bash
kubectl scale deployment ghost-api -n ghost --replicas=10
EOF

cat > kill-db-connections.sh << 'EOF'
#!/bin/bash
kubectl exec -it postgres-0 -n ghost -- psql -U ghost -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state = 'idle' AND state_change < now() - interval '5 minutes';"
EOF

chmod +x *.sh
```

### On-Call Handoff Checklist

```
ON-CALL HANDOFF CHECKLIST
=========================

OUTGOING ON-CALL:
[ ] Review open incidents
[ ] Document any workarounds in place
[ ] List monitoring anomalies
[ ] Note recent changes/deployments
[ ] Share tribal knowledge
[ ] Verify contact information current
[ ] Transfer PagerDuty on-call

INCOMING ON-CALL:
[ ] Test PagerDuty notifications
[ ] Verify access to all systems
[ ] Review recent incidents
[ ] Check monitoring dashboards
[ ] Test emergency access (VPN, bastion)
[ ] Bookmark key dashboards
[ ] Review this runbook

BOTH:
[ ] Joint review of handoff notes
[ ] Q&A session
[ ] Schedule overlap period
```

---

**Document Classification:** CONFIDENTIAL - Internal Use Only  
**Review Schedule:** Quarterly  
**Next Review Date:** 2026-05-03

**Version History:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-03 | SRE Team | Initial version |
