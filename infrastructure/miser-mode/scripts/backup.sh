#!/bin/bash
# Ghost Platform - Ultra Miser Mode Backup Script
# Performs comprehensive backup of all infrastructure components
#
# Usage:
#   ./backup.sh [options]
#
# Options:
#   --full              Full backup (default)
#   --database-only     Backup only database
#   --config-only       Backup only configuration
#   --upload-s3         Upload to S3 after backup
#   --retention DAYS    Keep backups for N days (default: 30)
#   --help              Show this help

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
BACKUP_DIR="${PROJECT_DIR}/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_NAME="ghost-backup-${TIMESTAMP}"
RETENTION_DAYS=30
UPLOAD_S3=false
S3_BUCKET="${S3_BUCKET:-}"
AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-}"
AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-}"
AWS_ENDPOINT="${AWS_ENDPOINT:-s3.amazonaws.com}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Logging
LOG_FILE="${BACKUP_DIR}/logs/backup-${TIMESTAMP}.log"
mkdir -p "$(dirname "$LOG_FILE")"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}ERROR: $1${NC}" | tee -a "$LOG_FILE"
}

success() {
    echo -e "${GREEN}SUCCESS: $1${NC}" | tee -a "$LOG_FILE"
}

warning() {
    echo -e "${YELLOW}WARNING: $1${NC}" | tee -a "$LOG_FILE"
}

# Parse arguments
FULL_BACKUP=true
DATABASE_ONLY=false
CONFIG_ONLY=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --full)
            FULL_BACKUP=true
            shift
            ;;
        --database-only)
            DATABASE_ONLY=true
            FULL_BACKUP=false
            shift
            ;;
        --config-only)
            CONFIG_ONLY=true
            FULL_BACKUP=false
            shift
            ;;
        --upload-s3)
            UPLOAD_S3=true
            shift
            ;;
        --retention)
            RETENTION_DAYS="$2"
            shift 2
            ;;
        --help)
            echo "Ghost Platform Backup Script"
            echo ""
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --full              Full backup (default)"
            echo "  --database-only     Backup only database"
            echo "  --config-only       Backup only configuration"
            echo "  --upload-s3         Upload to S3 after backup"
            echo "  --retention DAYS    Keep backups for N days (default: 30)"
            echo "  --help              Show this help"
            exit 0
            ;;
        *)
            error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Validate environment
validate_environment() {
    log "Validating environment..."
    
    if [ ! -d "$PROJECT_DIR" ]; then
        error "Project directory not found: $PROJECT_DIR"
        exit 1
    fi
    
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed or not in PATH"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        error "Docker daemon is not running"
        exit 1
    fi
    
    # Check if containers are running
    if [ "$FULL_BACKUP" = true ] || [ "$DATABASE_ONLY" = true ]; then
        if ! docker ps | grep -q "ghost-postgres"; then
            error "PostgreSQL container is not running"
            exit 1
        fi
    fi
    
    success "Environment validation passed"
}

# Create backup directory
setup_backup_dir() {
    log "Setting up backup directory: $BACKUP_DIR"
    mkdir -p "${BACKUP_DIR}"/{database,redis,rabbitmq,config,logs}
    mkdir -p "${BACKUP_DIR}/archives"
}

# Backup PostgreSQL
backup_database() {
    log "Starting PostgreSQL backup..."
    
    local db_backup_dir="${BACKUP_DIR}/database/${TIMESTAMP}"
    mkdir -p "$db_backup_dir"
    
    # Create database dump
    log "Creating database dump..."
    if docker exec ghost-postgres pg_dumpall -c -U ghost > "${db_backup_dir}/full_dump.sql" 2>> "$LOG_FILE"; then
        success "Database dump created: ${db_backup_dir}/full_dump.sql"
    else
        error "Failed to create database dump"
        return 1
    fi
    
    # Compress the dump
    log "Compressing database dump..."
    gzip -f "${db_backup_dir}/full_dump.sql"
    
    # Get database statistics
    docker exec ghost-postgres psql -U ghost -c "
        SELECT 
            schemaname,
            tablename,
            pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
        FROM pg_tables
        WHERE schemaname = 'public'
        ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
    " > "${db_backup_dir}/table_sizes.txt" 2>> "$LOG_FILE"
    
    log "Database backup completed"
}

# Backup Redis
backup_redis() {
    log "Starting Redis backup..."
    
    local redis_backup_dir="${BACKUP_DIR}/redis/${TIMESTAMP}"
    mkdir -p "$redis_backup_dir"
    
    # Trigger BGSAVE
    log "Triggering Redis BGSAVE..."
    docker exec ghost-redis redis-cli BGSAVE >> "$LOG_FILE" 2>&1
    
    # Wait for save to complete
    log "Waiting for Redis save to complete..."
    sleep 2
    
    while docker exec ghost-redis redis-cli INFO Persistence | grep -q "rdb_bgsave_in_progress:1"; do
        sleep 1
    done
    
    # Copy RDB file
    if docker cp ghost-redis:/data/dump.rdb "${redis_backup_dir}/dump.rdb" 2>> "$LOG_FILE"; then
        success "Redis backup created: ${redis_backup_dir}/dump.rdb"
    else
        warning "Failed to backup Redis data"
    fi
    
    # Backup Redis config
    docker exec ghost-redis cat /usr/local/etc/redis/redis.conf > "${redis_backup_dir}/redis.conf" 2>> "$LOG_FILE" || true
    
    log "Redis backup completed"
}

# Backup RabbitMQ
backup_rabbitmq() {
    log "Starting RabbitMQ backup..."
    
    local rabbitmq_backup_dir="${BACKUP_DIR}/rabbitmq/${TIMESTAMP}"
    mkdir -p "$rabbitmq_backup_dir"
    
    # Export definitions
    log "Exporting RabbitMQ definitions..."
    if docker exec ghost-rabbitmq rabbitmqctl export_definitions "${rabbitmq_backup_dir}/definitions.json" 2>> "$LOG_FILE"; then
        success "RabbitMQ definitions exported"
    else
        # Fallback to direct file copy
        docker cp ghost-rabbitmq:/var/lib/rabbitmq/definitions.json "${rabbitmq_backup_dir}/definitions.json" 2>> "$LOG_FILE" || true
    fi
    
    # Backup configuration
    docker cp ghost-rabbitmq:/etc/rabbitmq/rabbitmq.conf "${rabbitmq_backup_dir}/rabbitmq.conf" 2>> "$LOG_FILE" || true
    
    # List queues and their stats
    docker exec ghost-rabbitmq rabbitmqctl list_queues name messages consumers state > "${rabbitmq_backup_dir}/queue_stats.txt" 2>> "$LOG_FILE" || true
    
    log "RabbitMQ backup completed"
}

# Backup configuration
backup_config() {
    log "Starting configuration backup..."
    
    local config_backup_dir="${BACKUP_DIR}/config/${TIMESTAMP}"
    mkdir -p "$config_backup_dir"
    
    # Backup Docker Compose
    cp "${PROJECT_DIR}/docker-compose.yml" "${config_backup_dir}/" 2>> "$LOG_FILE" || true
    cp "${PROJECT_DIR}/.env" "${config_backup_dir}/.env.backup" 2>> "$LOG_FILE" || warning ".env file not found"
    
    # Backup nginx config
    if [ -d "${PROJECT_DIR}/nginx" ]; then
        cp -r "${PROJECT_DIR}/nginx" "${config_backup_dir}/" 2>> "$LOG_FILE"
    fi
    
    # Backup monitoring configs
    if [ -d "${PROJECT_DIR}/monitoring" ]; then
        cp -r "${PROJECT_DIR}/monitoring" "${config_backup_dir}/" 2>> "$LOG_FILE"
    fi
    
    # Backup SSL certificates
    if [ -d "${PROJECT_DIR}/ssl" ]; then
        cp -r "${PROJECT_DIR}/ssl" "${config_backup_dir}/" 2>> "$LOG_FILE"
    fi
    
    # Create backup manifest
    cat > "${config_backup_dir}/manifest.json" << EOF
{
    "backup_name": "$BACKUP_NAME",
    "timestamp": "$TIMESTAMP",
    "hostname": "$(hostname)",
    "docker_version": "$(docker --version)",
    "containers": $(docker ps --format '{{json .}}' | jq -s .),
    "images": $(docker images --format '{{json .}}' | jq -s .)
}
EOF
    
    success "Configuration backup completed"
}

# Create archive
create_archive() {
    log "Creating backup archive..."
    
    local archive_path="${BACKUP_DIR}/archives/${BACKUP_NAME}.tar.gz"
    
    cd "$BACKUP_DIR"
    if tar -czf "$archive_path" database/${TIMESTAMP} redis/${TIMESTAMP} rabbitmq/${TIMESTAMP} config/${TIMESTAMP} 2>> "$LOG_FILE"; then
        success "Archive created: $archive_path"
        
        # Get archive size
        local archive_size=$(du -h "$archive_path" | cut -f1)
        log "Archive size: $archive_size"
    else
        error "Failed to create archive"
        return 1
    fi
}

# Upload to S3
upload_to_s3() {
    if [ "$UPLOAD_S3" = false ]; then
        return 0
    fi
    
    log "Uploading to S3..."
    
    if [ -z "$S3_BUCKET" ]; then
        warning "S3_BUCKET not set, skipping upload"
        return 0
    fi
    
    if ! command -v aws &> /dev/null; then
        warning "AWS CLI not installed, skipping upload"
        return 0
    fi
    
    local archive_path="${BACKUP_DIR}/archives/${BACKUP_NAME}.tar.gz"
    local s3_key="ghost-backups/$(date +%Y/%m)/${BACKUP_NAME}.tar.gz"
    
    # Configure AWS CLI with custom endpoint if provided
    local aws_args=""
    if [ "$AWS_ENDPOINT" != "s3.amazonaws.com" ]; then
        aws_args="--endpoint-url https://$AWS_ENDPOINT"
    fi
    
    if aws s3 cp "$archive_path" "s3://${S3_BUCKET}/${s3_key}" $aws_args 2>> "$LOG_FILE"; then
        success "Backup uploaded to S3: s3://${S3_BUCKET}/${s3_key}"
    else
        error "Failed to upload to S3"
        return 1
    fi
}

# Cleanup old backups
cleanup_old_backups() {
    log "Cleaning up backups older than $RETENTION_DAYS days..."
    
    local deleted_count=0
    
    # Cleanup database backups
    if [ -d "${BACKUP_DIR}/database" ]; then
        deleted_count=$((deleted_count + $(find "${BACKUP_DIR}/database" -type d -mtime +$RETENTION_DAYS -print | wc -l)))
        find "${BACKUP_DIR}/database" -type d -mtime +$RETENTION_DAYS -exec rm -rf {} + 2>> "$LOG_FILE" || true
    fi
    
    # Cleanup Redis backups
    if [ -d "${BACKUP_DIR}/redis" ]; then
        deleted_count=$((deleted_count + $(find "${BACKUP_DIR}/redis" -type d -mtime +$RETENTION_DAYS -print | wc -l)))
        find "${BACKUP_DIR}/redis" -type d -mtime +$RETENTION_DAYS -exec rm -rf {} + 2>> "$LOG_FILE" || true
    fi
    
    # Cleanup RabbitMQ backups
    if [ -d "${BACKUP_DIR}/rabbitmq" ]; then
        deleted_count=$((deleted_count + $(find "${BACKUP_DIR}/rabbitmq" -type d -mtime +$RETENTION_DAYS -print | wc -l)))
        find "${BACKUP_DIR}/rabbitmq" -type d -mtime +$RETENTION_DAYS -exec rm -rf {} + 2>> "$LOG_FILE" || true
    fi
    
    # Cleanup config backups
    if [ -d "${BACKUP_DIR}/config" ]; then
        deleted_count=$((deleted_count + $(find "${BACKUP_DIR}/config" -type d -mtime +$RETENTION_DAYS -print | wc -l)))
        find "${BACKUP_DIR}/config" -type d -mtime +$RETENTION_DAYS -exec rm -rf {} + 2>> "$LOG_FILE" || true
    fi
    
    # Cleanup old archives
    if [ -d "${BACKUP_DIR}/archives" ]; then
        deleted_count=$((deleted_count + $(find "${BACKUP_DIR}/archives" -type f -mtime +$RETENTION_DAYS -print | wc -l)))
        find "${BACKUP_DIR}/archives" -type f -mtime +$RETENTION_DAYS -delete 2>> "$LOG_FILE" || true
    fi
    
    # Cleanup old logs
    if [ -d "${BACKUP_DIR}/logs" ]; then
        find "${BACKUP_DIR}/logs" -type f -mtime +7 -delete 2>> "$LOG_FILE" || true
    fi
    
    log "Cleaned up $deleted_count old backup(s)"
}

# Main execution
main() {
    log "=== Ghost Platform Backup Started ==="
    log "Backup name: $BACKUP_NAME"
    log "Mode: $([ "$FULL_BACKUP" = true ] && echo 'Full' || ([ "$DATABASE_ONLY" = true ] && echo 'Database Only' || echo 'Config Only'))"
    
    validate_environment
    setup_backup_dir
    
    if [ "$FULL_BACKUP" = true ] || [ "$DATABASE_ONLY" = true ]; then
        backup_database
        backup_redis
        backup_rabbitmq
    fi
    
    if [ "$FULL_BACKUP" = true ] || [ "$CONFIG_ONLY" = true ]; then
        backup_config
    fi
    
    if [ "$FULL_BACKUP" = true ]; then
        create_archive
        upload_to_s3
        cleanup_old_backups
    fi
    
    log "=== Ghost Platform Backup Completed ==="
    success "Backup completed successfully!"
    log "Log file: $LOG_FILE"
    
    return 0
}

# Run main
main "$@"