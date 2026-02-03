#!/bin/bash
# Ghost Platform - Ultra Miser Mode Restore Script
# Restores infrastructure from backup archives
#
# Usage:
#   ./restore.sh [options] <backup-archive>
#
# Options:
#   --dry-run           Show what would be restored without doing it
#   --database-only     Restore only database
#   --config-only       Restore only configuration
#   --force             Skip confirmation prompts
#   --help              Show this help

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
BACKUP_DIR="${PROJECT_DIR}/backups"
RESTORE_LOG="${BACKUP_DIR}/logs/restore-$(date +%Y%m%d_%H%M%S).log"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Logging
log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$RESTORE_LOG"
}

error() {
    echo -e "${RED}ERROR: $1${NC}" | tee -a "$RESTORE_LOG"
}

success() {
    echo -e "${GREEN}SUCCESS: $1${NC}" | tee -a "$RESTORE_LOG"
}

warning() {
    echo -e "${YELLOW}WARNING: $1${NC}" | tee -a "$RESTORE_LOG"
}

info() {
    echo -e "${BLUE}INFO: $1${NC}" | tee -a "$RESTORE_LOG"
}

# Show help
show_help() {
    cat << EOF
Ghost Platform Restore Script

Usage: $0 [options] <backup-archive>

Arguments:
  backup-archive    Path to backup archive (.tar.gz file)

Options:
  --dry-run           Show what would be restored without doing it
  --database-only     Restore only database
  --config-only       Restore only configuration
  --force             Skip confirmation prompts
  --help              Show this help

Examples:
  $0 /path/to/ghost-backup-20240203_120000.tar.gz
  $0 --database-only /path/to/backup.tar.gz
  $0 --dry-run /path/to/backup.tar.gz

EOF
}

# Parse arguments
DRY_RUN=false
DATABASE_ONLY=false
CONFIG_ONLY=false
FORCE=false
BACKUP_ARCHIVE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --database-only)
            DATABASE_ONLY=true
            shift
            ;;
        --config-only)
            CONFIG_ONLY=true
            shift
            ;;
        --force)
            FORCE=true
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        -*)
            error "Unknown option: $1"
            show_help
            exit 1
            ;;
        *)
            if [ -z "$BACKUP_ARCHIVE" ]; then
                BACKUP_ARCHIVE="$1"
            else
                error "Multiple backup archives specified"
                exit 1
            fi
            shift
            ;;
    esac
done

# Validate arguments
if [ -z "$BACKUP_ARCHIVE" ]; then
    error "No backup archive specified"
    show_help
    exit 1
fi

if [ ! -f "$BACKUP_ARCHIVE" ]; then
    error "Backup archive not found: $BACKUP_ARCHIVE"
    exit 1
fi

# Validate environment
validate_environment() {
    log "Validating environment..."
    
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        error "Docker daemon is not running"
        exit 1
    fi
    
    mkdir -p "$(dirname "$RESTORE_LOG")"
    
    log "Environment validation passed"
}

# Extract backup archive
extract_backup() {
    log "Extracting backup archive..."
    
    local extract_dir="${BACKUP_DIR}/restore-temp-$$"
    mkdir -p "$extract_dir"
    
    if [ "$DRY_RUN" = true ]; then
        info "[DRY-RUN] Would extract $BACKUP_ARCHIVE to $extract_dir"
        # Still extract for inspection
        tar -tzf "$BACKUP_ARCHIVE" > "${extract_dir}/contents.txt"
        cat "${extract_dir}/contents.txt"
    else
        if tar -xzf "$BACKUP_ARCHIVE" -C "$extract_dir" 2>> "$RESTORE_LOG"; then
            success "Archive extracted to $extract_dir"
        else
            error "Failed to extract archive"
            rm -rf "$extract_dir"
            exit 1
        fi
    fi
    
    echo "$extract_dir"
}

# Verify backup integrity
verify_backup() {
    local extract_dir="$1"
    
    log "Verifying backup integrity..."
    
    local issues=0
    
    # Check for database backup
    if [ ! -d "${extract_dir}/database" ]; then
        warning "No database backup found in archive"
        issues=$((issues + 1))
    else
        local db_backup_count=$(find "${extract_dir}/database" -name "*.sql.gz" -o -name "*.sql" | wc -l)
        if [ "$db_backup_count" -eq 0 ]; then
            warning "No database dump files found"
            issues=$((issues + 1))
        else
            log "Found $db_backup_count database backup(s)"
        fi
    fi
    
    # Check for Redis backup
    if [ ! -d "${extract_dir}/redis" ]; then
        warning "No Redis backup found in archive"
    else
        local redis_backup=$(find "${extract_dir}/redis" -name "dump.rdb" | head -1)
        if [ -z "$redis_backup" ]; then
            warning "No Redis dump.rdb found"
        else
            log "Found Redis backup: $redis_backup"
        fi
    fi
    
    # Check for RabbitMQ backup
    if [ ! -d "${extract_dir}/rabbitmq" ]; then
        warning "No RabbitMQ backup found in archive"
    else
        local rabbitmq_defs=$(find "${extract_dir}/rabbitmq" -name "definitions.json" | head -1)
        if [ -z "$rabbitmq_defs" ]; then
            warning "No RabbitMQ definitions found"
        else
            log "Found RabbitMQ definitions: $rabbitmq_defs"
        fi
    fi
    
    # Check for config backup
    if [ ! -d "${extract_dir}/config" ]; then
        warning "No configuration backup found in archive"
    else
        log "Found configuration backup"
    fi
    
    if [ $issues -gt 0 ]; then
        warning "Backup verification found $issues issue(s)"
        if [ "$FORCE" != true ]; then
            read -p "Continue anyway? (y/N) " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                log "Restore cancelled by user"
                exit 0
            fi
        fi
    else
        success "Backup verification passed"
    fi
}

# Confirm restore
confirm_restore() {
    if [ "$FORCE" = true ]; then
        return 0
    fi
    
    echo
    echo -e "${YELLOW}=============================================="
    echo "WARNING: This will OVERWRITE existing data!"
    echo "==============================================${NC}"
    echo
    echo "This action will:"
    if [ "$DATABASE_ONLY" = true ] || [ "$DRY_RUN" != true ]; then
        echo "  - Stop running containers"
        echo "  - Restore database (ALL existing data will be lost)"
    fi
    if [ "$CONFIG_ONLY" = true ] || [ "$DRY_RUN" != true ]; then
        echo "  - Restore configuration files"
    fi
    if [ "$DRY_RUN" = true ]; then
        echo "  - DRY RUN MODE: No actual changes will be made"
    fi
    echo
    read -p "Are you sure you want to continue? Type 'RESTORE' to confirm: " confirm
    
    if [ "$confirm" != "RESTORE" ]; then
        log "Restore cancelled by user"
        exit 0
    fi
}

# Restore database
restore_database() {
    local extract_dir="$1"
    
    log "Restoring database..."
    
    # Find database backup
    local db_backup=$(find "${extract_dir}/database" -name "*.sql.gz" | head -1)
    if [ -z "$db_backup" ]; then
        db_backup=$(find "${extract_dir}/database" -name "*.sql" | head -1)
    fi
    
    if [ -z "$db_backup" ]; then
        error "No database backup found"
        return 1
    fi
    
    log "Found database backup: $db_backup"
    
    if [ "$DRY_RUN" = true ]; then
        info "[DRY-RUN] Would restore database from $db_backup"
        return 0
    fi
    
    # Stop application containers
    log "Stopping application containers..."
    docker-compose -f "${PROJECT_DIR}/docker-compose.yml" stop ghost-webapi 2>> "$RESTORE_LOG" || true
    
    # Wait for PostgreSQL to be ready
    log "Waiting for PostgreSQL..."
    until docker exec ghost-postgres pg_isready -U ghost 2>/dev/null; do
        sleep 1
    done
    
    # Drop and recreate database
    log "Recreating database..."
    docker exec ghost-postgres psql -U ghost -c "DROP DATABASE IF EXISTS ghost;" 2>> "$RESTORE_LOG" || true
    docker exec ghost-postgres psql -U ghost -c "CREATE DATABASE ghost;" 2>> "$RESTORE_LOG"
    
    # Restore from backup
    log "Restoring from backup file..."
    if [[ "$db_backup" == *.gz ]]; then
        gunzip -c "$db_backup" | docker exec -i ghost-postgres psql -U ghost 2>> "$RESTORE_LOG"
    else
        docker exec -i ghost-postgres psql -U ghost < "$db_backup" 2>> "$RESTORE_LOG"
    fi
    
    if [ $? -eq 0 ]; then
        success "Database restored successfully"
    else
        error "Failed to restore database"
        return 1
    fi
    
    # Restart application
    log "Restarting application containers..."
    docker-compose -f "${PROJECT_DIR}/docker-compose.yml" start ghost-webapi 2>> "$RESTORE_LOG"
}

# Restore Redis
restore_redis() {
    local extract_dir="$1"
    
    log "Restoring Redis..."
    
    local redis_backup=$(find "${extract_dir}/redis" -name "dump.rdb" | head -1)
    
    if [ -z "$redis_backup" ]; then
        warning "No Redis backup found, skipping"
        return 0
    fi
    
    log "Found Redis backup: $redis_backup"
    
    if [ "$DRY_RUN" = true ]; then
        info "[DRY-RUN] Would restore Redis from $redis_backup"
        return 0
    fi
    
    # Stop Redis
    log "Stopping Redis..."
    docker-compose -f "${PROJECT_DIR}/docker-compose.yml" stop redis 2>> "$RESTORE_LOG"
    
    # Copy backup
    log "Restoring Redis data..."
    docker cp "$redis_backup" ghost-redis:/data/dump.rdb 2>> "$RESTORE_LOG"
    
    # Start Redis
    log "Starting Redis..."
    docker-compose -f "${PROJECT_DIR}/docker-compose.yml" start redis 2>> "$RESTORE_LOG"
    
    # Wait for Redis
    until docker exec ghost-redis redis-cli ping 2>/dev/null | grep -q "PONG"; do
        sleep 1
    done
    
    success "Redis restored successfully"
}

# Restore RabbitMQ
restore_rabbitmq() {
    local extract_dir="$1"
    
    log "Restoring RabbitMQ..."
    
    local rabbitmq_defs=$(find "${extract_dir}/rabbitmq" -name "definitions.json" | head -1)
    
    if [ -z "$rabbitmq_defs" ]; then
        warning "No RabbitMQ definitions found, skipping"
        return 0
    fi
    
    log "Found RabbitMQ definitions: $rabbitmq_defs"
    
    if [ "$DRY_RUN" = true ]; then
        info "[DRY-RUN] Would restore RabbitMQ from $rabbitmq_defs"
        return 0
    fi
    
    # Wait for RabbitMQ
    log "Waiting for RabbitMQ..."
    until curl -s http://localhost:15672/api/overview -u guest:guest 2>/dev/null | grep -q "rabbitmq_version"; do
        sleep 2
    done
    
    # Import definitions
    log "Importing RabbitMQ definitions..."
    curl -s -X POST \
        -H "Content-Type: application/json" \
        -u guest:guest \
        http://localhost:15672/api/definitions \
        -d "@$rabbitmq_defs" 2>> "$RESTORE_LOG"
    
    success "RabbitMQ restored successfully"
}

# Restore configuration
restore_config() {
    local extract_dir="$1"
    
    log "Restoring configuration..."
    
    local config_dir="${extract_dir}/config"
    local latest_config=$(find "$config_dir" -type d | sort | tail -1)
    
    if [ -z "$latest_config" ] || [ "$latest_config" = "$config_dir" ]; then
        warning "No configuration backup found"
        return 0
    fi
    
    log "Found configuration backup: $latest_config"
    
    if [ "$DRY_RUN" = true ]; then
        info "[DRY-RUN] Would restore configuration from $latest_config"
        return 0
    fi
    
    # Backup current config first
    if [ -f "${PROJECT_DIR}/.env" ]; then
        log "Backing up current configuration..."
        cp "${PROJECT_DIR}/.env" "${PROJECT_DIR}/.env.backup.$(date +%Y%m%d%H%M%S)"
    fi
    
    # Restore config files
    if [ -f "${latest_config}/.env.backup" ]; then
        cp "${latest_config}/.env.backup" "${PROJECT_DIR}/.env"
        log "Restored .env file"
    fi
    
    if [ -d "${latest_config}/nginx" ]; then
        cp -r "${latest_config}/nginx" "${PROJECT_DIR}/"
        log "Restored nginx configuration"
    fi
    
    if [ -d "${latest_config}/monitoring" ]; then
        cp -r "${latest_config}/monitoring" "${PROJECT_DIR}/"
        log "Restored monitoring configuration"
    fi
    
    success "Configuration restored successfully"
}

# Main execution
main() {
    log "=== Ghost Platform Restore Started ==="
    log "Backup archive: $BACKUP_ARCHIVE"
    log "Mode: $([ "$DRY_RUN" = true ] && echo 'DRY-RUN' || ([ "$DATABASE_ONLY" = true ] && echo 'Database Only' || ([ "$CONFIG_ONLY" = true ] && echo 'Config Only' || echo 'Full')))"
    
    validate_environment
    
    # Extract backup
    local extract_dir
    extract_dir=$(extract_backup)
    
    # Verify backup
    verify_backup "$extract_dir"
    
    # Confirm
    confirm_restore
    
    # Perform restore
    if [ "$CONFIG_ONLY" = true ]; then
        restore_config "$extract_dir"
    elif [ "$DATABASE_ONLY" = true ]; then
        restore_database "$extract_dir"
    else
        # Full restore
        restore_config "$extract_dir"
        restore_database "$extract_dir"
        restore_redis "$extract_dir"
        restore_rabbitmq "$extract_dir"
    fi
    
    # Cleanup
    if [ "$DRY_RUN" = false ]; then
        log "Cleaning up temporary files..."
        rm -rf "$extract_dir"
    fi
    
    log "=== Ghost Platform Restore Completed ==="
    success "Restore completed successfully!"
    log "Log file: $RESTORE_LOG"
    
    if [ "$DRY_RUN" = false ]; then
        info "Please verify all services are running correctly:"
        info "  docker-compose ps"
        info "  curl http://localhost:8080/health"
    fi
}

# Run main
main "$@"