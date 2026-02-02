# Emergency Rollback Script

## Overview
`rollback.sh` performs an emergency rollback from canary to stable deployment with graceful connection handling, health verification, and comprehensive logging.

## Quick Start

### Interactive Rollback (with confirmation)
```bash
./deploy/rollback.sh
```

### Automated Rollback (for CI/CD)
```bash
./deploy/rollback.sh --force
```

## Exit Codes
- `0` - Rollback completed successfully
- `1` - Rollback failed (intervention required)
- `2` - Invalid arguments

## Key Features

### Safety Features
✓ **Connection Drain** (30s timeout) - Gracefully stops traffic to canary  
✓ **Health Verification** - Confirms stable service is healthy (5 retries)  
✓ **State Backup** - Creates backup before starting (`.rollback_backup/`)  
✓ **Detailed Logging** - Full audit trail (`logs/rollback_*.log`)  
✓ **Error Recovery** - Auto-attempts service restart if unhealthy  
✓ **Force Flag** - Skip confirmations for automated rollback  

### Rollback Flow
```
1. Prerequisites Check
   ├─ Docker/Docker-compose running
   ├─ Required config files present
   └─ User confirmation (unless --force)

2. Backup Current State
   ├─ Nginx config backup
   └─ Docker-compose state snapshot

3. Connection Drain
   ├─ Update nginx to 0% canary traffic
   ├─ Wait 30s for active requests to complete
   └─ Reload nginx gracefully

4. Stop Canary
   ├─ Stop canary container
   └─ Force kill if necessary

5. Verify Stable Health
   ├─ Check stable container running
   ├─ Attempt restart if needed
   ├─ Perform health check (5 retries, 3s each)
   └─ Fail fast if unhealthy

6. Update Nginx Configuration
   ├─ Create stable-only upstream config
   ├─ Deploy to nginx container
   └─ Reload nginx

7. Cleanup Canary Resources
   ├─ Remove canary container
   ├─ Prune unused networks
   └─ Cleanup temporary configs

8. Verify Rollback Success
   └─ Confirm nginx responding with stable
```

## Functions Reference

### Core Rollback Functions

**`drain_connections()`**
- Routes 0% traffic to canary (100% to stable)
- Waits 30 seconds for connections to complete
- Gracefully reloads nginx

**`stop_canary()`**
- Stops canary container
- Falls back to force kill if stop fails
- Logs all actions

**`verify_stable()`**
- Checks if stable container is running
- Auto-attempts start if down
- Performs 5 health check attempts
- Returns 1 if ultimately unhealthy

**`update_nginx()`**
- Creates stable-only nginx config
- Copies config to nginx container
- Reloads nginx with new routing

**`cleanup()`**
- Removes canary container
- Prunes unused docker networks
- Cleans up temporary configs

### Utility Functions

**`check_prerequisites()`**
- Verifies docker, docker-compose, curl, jq installed
- Checks Docker daemon is running
- Validates config files exist

**`create_backup()`**
- Backs up current nginx config
- Saves docker-compose state
- Creates `.rollback_backup/` directory

**`verify_rollback()`**
- Confirms nginx is responding
- Verifies traffic flowing to stable
- 3 retry attempts, 3s delays

## Configuration

Default values in script (all in `deploy/rollback.sh`):
```bash
STABLE_SERVICE="app-stable"
CANARY_SERVICE="app-canary"
NGINX_SERVICE="nginx-canary"
HEALTH_CHECK_RETRIES=5
HEALTH_CHECK_INTERVAL=3
CONNECTION_DRAIN_TIMEOUT=30
```

## Integration Points

### Docker Compose Integration
Uses: `docker-compose.canary.yml`
- Stops/removes canary service
- Verifies stable service health
- Manages container lifecycle

### Nginx Integration
Uses: `nginx-canary.conf`
- Updates upstream routing
- Routes traffic 100% to stable
- Maintains connection gracefully

### Service Contracts
Services must have:
- Health endpoint: `http://localhost:3000/health`
- Graceful shutdown support
- At least 30s timeout for existing connections

## Troubleshooting

### Rollback Failed - Check Logs
```bash
tail -f logs/rollback_*.log
```

### Manual Service Status
```bash
docker-compose -f deploy/docker-compose.canary.yml ps
```

### Check Nginx Status
```bash
docker exec nginx-canary curl -v http://localhost/
```

### Restore from Backup
```bash
# List available backups
ls -la .rollback_backup/

# Restore nginx config
docker cp .rollback_backup/nginx_backup_*.conf nginx-canary:/etc/nginx/conf.d/default.conf
docker exec nginx-canary nginx -s reload
```

### Manual Rollback Steps
If script fails, manual steps in order:
1. `docker-compose -f deploy/docker-compose.canary.yml stop app-canary`
2. Update nginx config to route 100% to stable
3. `docker exec nginx-canary nginx -s reload`
4. Verify: `docker-compose -f deploy/docker-compose.canary.yml ps`
5. Test health: `docker exec app-stable curl http://localhost:3000/health`

## Logs & Output

### Log Files
```
logs/rollback_20240202_054400.log  # Full audit trail
.rollback_backup/                   # Backup configs & state
```

### Log Format
```
[2024-02-02 05:44:00] [INFO] Connection drain started
[2024-02-02 05:44:30] [SUCCESS] Connection drain completed
[2024-02-02 05:44:31] [ERROR] Health check failed
```

### Console Output
- ✓ Green success messages
- ⚠ Yellow warnings
- ✗ Red errors (to stderr)
- Blue info messages

## Production Checklist

- [ ] Test rollback in staging first
- [ ] Ensure health endpoints are working
- [ ] Verify backup directory is accessible
- [ ] Confirm logs directory exists or is creatable
- [ ] Test with `--force` flag for CI/CD integration
- [ ] Have manual rollback steps documented
- [ ] Alert team on rollback triggers
- [ ] Monitor stable service after rollback
- [ ] Investigate canary failure reasons
- [ ] Document incident timeline

## Related Scripts

- `deploy/canary-rollout.sh` - Canary deployment (reverse of this)
- `deploy/docker-compose.canary.yml` - Canary environment config
- `deploy/nginx-canary.conf` - Nginx routing config

## Best Practices

1. **Always test rollback script in staging** before trusting in production
2. **Use interactive mode first** (`./rollback.sh`) to understand the process
3. **Use --force flag only in automated systems** with proper monitoring
4. **Monitor stable service performance** for 5-10 minutes after rollback
5. **Investigate canary failures** in logs before retrying deployment
6. **Keep backup configs** for forensic analysis
7. **Alert team immediately** when rollback is triggered
8. **Document the incident** with timeline and root cause

## Security Considerations

- Script runs with current user privileges (docker group assumed)
- Backups stored in `.rollback_backup/` (not versioned)
- Logs may contain sensitive environment info
- No credentials hardcoded (uses Docker socket)
- Health checks use internal localhost only
