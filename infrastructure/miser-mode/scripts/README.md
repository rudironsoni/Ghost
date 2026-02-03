# Migration Scripts

This directory contains scripts for migrating Ghost Platform from a distributed architecture to Ultra Miser Mode single-node deployment.

## Scripts Overview

### Core Migration Scripts

| Script | Purpose | Usage |
|--------|---------|-------|
| **migrate.sh** | Main orchestrator - runs full migration | `./migrate.sh --source-host prod.example.com` |
| **export-data.sh** | Export data from source system | `./export-data.sh --host prod.example.com` |
| **import-data.sh** | Import data to target system | `./import-data.sh --input-dir ./export_data` |
| **validate-migration.sh** | Verify migration success | `./validate-migration.sh --export-dir ./export_data` |
| **rollback.sh** | Rollback failed migration | `./rollback.sh --source-host prod.example.com` |

### Supporting Scripts

| Script | Purpose | Usage |
|--------|---------|-------|
| **backup.sh** | Create backups of running system | `./backup.sh` |
| **restore.sh** | Restore from backup | `./restore.sh --backup-dir ./backups/backup_20260203` |

## Quick Start

### Full Migration

```bash
# Interactive migration (recommended)
./migrate.sh --source-host prod.example.com --interactive

# Automated migration
./migrate.sh --source-host prod.example.com --target-host localhost
```

### Dry Run (Test Mode)

```bash
# Test migration without making changes
./migrate.sh --source-host prod.example.com --dry-run
```

### Manual Step-by-Step

```bash
# 1. Export from source
./export-data.sh --host prod.example.com --output-dir ../backups/export_$(date +%Y%m%d)

# 2. Import to target
./import-data.sh --input-dir ../backups/export_20260203_120000 --target-host localhost

# 3. Validate
./validate-migration.sh --export-dir ../backups/export_20260203_120000

# 4. If issues, rollback
./rollback.sh --source-host prod.example.com --reason "Data validation failed"
```

## Script Options

### migrate.sh

```
Options:
  --dry-run              Run in dry-run mode (no actual changes)
  --interactive          Run in interactive mode with prompts
  --source-host HOST     Source system hostname/IP
  --source-user USER     SSH user for source system (default: current user)
  --target-host HOST     Target system hostname/IP (default: localhost)
  --skip-validation      Skip pre-migration validation
  --skip-backup          Skip backup creation (not recommended)
  --config FILE          Load configuration from file
  --help                 Show help message
```

### export-data.sh

```
Options:
  --dry-run              Simulate export without actual data transfer
  --host HOST            Source system hostname/IP
  --user USER            SSH user for source system
  --output-dir DIR       Output directory for exported data
  --compress             Compress exported data (default: true)
  --skip-postgres        Skip PostgreSQL export
  --skip-redis           Skip Redis export
  --skip-rabbitmq        Skip RabbitMQ export
  --help                 Show help message
```

### import-data.sh

```
Options:
  --dry-run              Simulate import without actual changes
  --input-dir DIR        Input directory with exported data
  --target-host HOST     Target system hostname (default: localhost)
  --force                Force import even if target has existing data
  --skip-postgres        Skip PostgreSQL import
  --skip-redis           Skip Redis import
  --skip-rabbitmq        Skip RabbitMQ import
  --help                 Show help message
```

### validate-migration.sh

```
Options:
  --dry-run              Simulate validation
  --export-dir DIR       Original export directory for comparison
  --target-host HOST     Target system hostname (default: localhost)
  --skip-data-compare    Skip detailed data comparison
  --help                 Show help message
```

### rollback.sh

```
Options:
  --reason TEXT          Reason for rollback (for logging)
  --export-dir DIR       Path to export backup directory
  --source-host HOST     Source system to restore
  --force                Skip confirmation prompts
  --help                 Show help message
```

## Examples

### Migrate from Remote Server

```bash
./migrate.sh \
  --source-host prod.example.com \
  --source-user admin \
  --target-host localhost \
  --interactive
```

### Export Only (for later import)

```bash
./export-data.sh \
  --host prod.example.com \
  --user admin \
  --output-dir /mnt/backup/ghost-export
```

### Import from Existing Export

```bash
./import-data.sh \
  --input-dir /mnt/backup/ghost-export \
  --force
```

### Validate Existing System

```bash
./validate-migration.sh \
  --export-dir /mnt/backup/ghost-export \
  --target-host localhost
```

### Emergency Rollback

```bash
./rollback.sh \
  --source-host prod.example.com \
  --reason "Critical production issue" \
  --force
```

## Output Locations

### Logs

```
infrastructure/miser-mode/logs/
├── migration_YYYYMMDD_HHMMSS.log
├── validation_YYYYMMDD_HHMMSS.log
├── rollback_YYYYMMDD_HHMMSS.log
├── migration_state_YYYYMMDD_HHMMSS.json
├── migration_report_YYYYMMDD_HHMMSS.txt
└── validation_report_YYYYMMDD_HHMMSS.txt
```

### Backups

```
infrastructure/miser-mode/backups/
├── export_YYYYMMDD_HHMMSS/
│   ├── postgres/
│   │   ├── ghost_db_*.dump
│   │   ├── ghost_schema_*.sql
│   │   └── metadata.json
│   ├── redis/
│   │   ├── redis_dump_*.rdb
│   │   └── metadata.json
│   ├── rabbitmq/
│   │   ├── definitions_*.json
│   │   └── metadata.json
│   ├── config/
│   │   └── *.env.bak
│   ├── export_manifest.json
│   └── EXPORT_SUMMARY.txt
└── rollback_YYYYMMDD_HHMMSS/
    └── ...
```

## Prerequisites

### Required Tools

All scripts require these tools to be installed:

```bash
# Core tools
docker
docker-compose
ssh
scp
jq
tar
gzip
sha256sum
nc (netcat)

# Database tools
psql
pg_dump
pg_restore

# Cache tools
redis-cli

# Message broker tools
rabbitmqadmin
curl
```

### Installation

**Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install -y docker.io docker-compose postgresql-client redis-tools curl jq netcat
```

**macOS:**
```bash
brew install docker docker-compose postgresql redis jq netcat
```

### SSH Access

Scripts require SSH access to source system:

```bash
# Test SSH connectivity
ssh user@source-host "echo 'Connection successful'"

# Set up SSH keys if needed
ssh-copy-id user@source-host
```

## Error Handling

All scripts:
- Use `set -euo pipefail` for strict error handling
- Log all operations to timestamped log files
- Create state files for resumability
- Provide detailed error messages
- Support dry-run mode for testing

### Common Exit Codes

- `0` - Success
- `1` - General error
- `2` - Missing prerequisites
- `3` - Connection error
- `4` - Data validation error

## Environment Variables

Scripts support environment variables for configuration:

```bash
# SSH configuration
export GHOST_SOURCE_HOST=prod.example.com
export GHOST_SOURCE_USER=admin

# Target configuration
export GHOST_TARGET_HOST=localhost

# Paths
export GHOST_EXPORT_DIR=/mnt/backup/ghost-export
export GHOST_BACKUP_DIR=/mnt/backup

# Then run scripts without arguments
./migrate.sh
```

## Configuration File

Create a configuration file to avoid repetitive arguments:

```bash
# migration.conf
SOURCE_HOST=prod.example.com
SOURCE_USER=admin
TARGET_HOST=localhost
SKIP_VALIDATION=false
SKIP_BACKUP=false
```

Use with:

```bash
./migrate.sh --config migration.conf
```

## Safety Features

### Backups

- All scripts create backups before making changes
- Export creates complete snapshots
- Import backs up target before overwrite
- Rollback preserves target data

### Validation

- Prerequisites checked before execution
- Disk space verified
- Connectivity tested
- Data integrity validated
- Service health confirmed

### Rollback

- Source system preserved during migration
- Easy rollback with rollback.sh
- DNS reversion instructions provided
- Data recovery procedures documented

## Performance

### Export Performance

- PostgreSQL: ~100MB/min (depends on compression)
- Redis: ~500MB/min (RDB format)
- RabbitMQ: ~50MB/min (JSON export)

**Tips:**
- Run during low-traffic periods
- Use compression for large datasets
- Consider network bandwidth

### Import Performance

- PostgreSQL: ~50MB/min (restore and index building)
- Redis: ~1GB/min (RDB load)
- RabbitMQ: ~20MB/min (definition import)

**Tips:**
- Ensure sufficient RAM on target
- Use SSD storage for best performance
- Monitor resource usage during import

## Troubleshooting

### "Command not found"

```bash
# Install missing tools
sudo apt-get install <missing-tool>

# Or check PATH
echo $PATH
```

### "Permission denied"

```bash
# Check script permissions
chmod +x *.sh

# Check SSH access
ssh -v user@host
```

### "Disk space full"

```bash
# Check available space
df -h

# Clean up old backups
rm -rf ../backups/export_old_*
```

### "Connection timeout"

```bash
# Test connectivity
ping source-host
ssh user@source-host "echo test"

# Check firewall
sudo ufw status
```

### Script Debugging

```bash
# Enable debug mode
bash -x ./migrate.sh --source-host prod.example.com

# Check logs
tail -f ../logs/migration_*.log

# Verify state
cat ../logs/migration_state_*.json | jq
```

## Best Practices

1. **Always run dry-run first**
   ```bash
   ./migrate.sh --source-host prod.example.com --dry-run
   ```

2. **Use interactive mode for first migration**
   ```bash
   ./migrate.sh --source-host prod.example.com --interactive
   ```

3. **Keep multiple backups**
   ```bash
   # Don't delete export data immediately
   # Keep for at least 7 days after successful migration
   ```

4. **Monitor during migration**
   ```bash
   # Open another terminal and monitor logs
   tail -f ../logs/migration_*.log
   ```

5. **Validate thoroughly**
   ```bash
   # Run validation multiple times
   ./validate-migration.sh --export-dir ./export_data
   ```

6. **Test rollback procedure**
   ```bash
   # Practice rollback in test environment first
   ./rollback.sh --source-host test.example.com --dry-run
   ```

## Support

For issues or questions:

1. Check the [MIGRATION.md](../MIGRATION.md) guide
2. Review script logs in `../logs/`
3. Open an issue on GitHub
4. Contact the Ghost Platform team

## Contributing

Improvements to migration scripts are welcome!

1. Test changes thoroughly
2. Update this README
3. Follow existing script patterns
4. Add error handling
5. Submit pull request

---

**Last Updated:** February 2026

**Maintained by:** Ghost Platform Team
