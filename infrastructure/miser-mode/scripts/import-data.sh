#!/bin/bash
# ============================================================================
# Ghost Platform - Data Import Script
# ============================================================================
#
# Imports data exported from a distributed deployment into Ultra Miser Mode
#
# Usage:
#   ./import-data.sh [OPTIONS]
#
# Options:
#   --dry-run              Simulate import without actual changes
#   --input-dir DIR        Input directory with exported data
#   --target-host HOST     Target system hostname (default: localhost)
#   --force                Force import even if target has existing data
#   --skip-postgres        Skip PostgreSQL import
#   --skip-redis           Skip Redis import
#   --skip-rabbitmq        Skip RabbitMQ import
#   --help                 Show this help message
#
# ============================================================================

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATION_DIR="$(dirname "$SCRIPT_DIR")"
DOCKER_DIR="${MIGRATION_DIR}/docker"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Default values
DRY_RUN=false
INPUT_DIR=""
TARGET_HOST="localhost"
FORCE=false
SKIP_POSTGRES=false
SKIP_REDIS=false
SKIP_RABBITMQ=false

# ============================================================================
# Utility Functions
# ============================================================================

log() {
    local level=$1
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    case $level in
        ERROR)
            echo -e "${RED}✗ [$timestamp] $message${NC}" >&2
            ;;
        SUCCESS)
            echo -e "${GREEN}✓ [$timestamp] $message${NC}"
            ;;
        WARNING)
            echo -e "${YELLOW}⚠ [$timestamp] $message${NC}"
            ;;
        INFO)
            echo -e "${BLUE}ℹ [$timestamp] $message${NC}"
            ;;
    esac
}

error_exit() {
    log ERROR "$1"
    exit 1
}

wait_for_service() {
    local service=$1
    local max_attempts=60
    local attempt=1
    
    log INFO "Waiting for $service to be ready..."
    
    while [ $attempt -le $max_attempts ]; do
        if docker-compose -f "${DOCKER_DIR}/docker-compose.yml" ps "$service" | grep -q "healthy\|Up"; then
            log SUCCESS "$service is ready"
            return 0
        fi
        
        log INFO "Waiting for $service... (attempt $attempt/$max_attempts)"
        sleep 5
        ((attempt++))
    done
    
    error_exit "$service failed to become ready"
}

verify_export_integrity() {
    log INFO "Verifying export data integrity..."
    
    if [ ! -f "${INPUT_DIR}/export_manifest.json" ]; then
        error_exit "Export manifest not found: ${INPUT_DIR}/export_manifest.json"
    fi
    
    if [ ! -f "${INPUT_DIR}/EXPORT_SUMMARY.txt" ]; then
        log WARNING "Export summary not found (non-critical)"
    fi
    
    # Verify PostgreSQL dump exists
    if [ "$SKIP_POSTGRES" = false ]; then
        if [ ! -d "${INPUT_DIR}/postgres" ] || [ -z "$(ls -A "${INPUT_DIR}/postgres/"*.dump 2>/dev/null)" ]; then
            error_exit "PostgreSQL dump not found in ${INPUT_DIR}/postgres/"
        fi
    fi
    
    # Verify Redis dump exists
    if [ "$SKIP_REDIS" = false ]; then
        if [ ! -d "${INPUT_DIR}/redis" ] || [ -z "$(ls -A "${INPUT_DIR}/redis/"*.rdb 2>/dev/null)" ]; then
            error_exit "Redis dump not found in ${INPUT_DIR}/redis/"
        fi
    fi
    
    # Verify RabbitMQ definitions exist
    if [ "$SKIP_RABBITMQ" = false ]; then
        if [ ! -d "${INPUT_DIR}/rabbitmq" ] || [ -z "$(ls -A "${INPUT_DIR}/rabbitmq/"definitions_*.json 2>/dev/null)" ]; then
            error_exit "RabbitMQ definitions not found in ${INPUT_DIR}/rabbitmq/"
        fi
    fi
    
    log SUCCESS "Export data integrity verified"
}

# ============================================================================
# Start Docker Services
# ============================================================================

start_services() {
    log INFO "Starting Ultra Miser Mode services..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would start Docker services"
        return
    fi
    
    cd "$DOCKER_DIR"
    
    # Check if .env exists
    if [ ! -f .env ]; then
        log WARNING ".env file not found, using .env.example"
        if [ -f .env.example ]; then
            cp .env.example .env
            log WARNING "Created .env from .env.example - please review credentials"
        else
            error_exit ".env.example not found"
        fi
    fi
    
    # Start infrastructure services first
    log INFO "Starting database and cache services..."
    docker-compose up -d postgres redis rabbitmq
    
    # Wait for services to be healthy
    wait_for_service postgres
    wait_for_service redis
    wait_for_service rabbitmq
    
    log SUCCESS "Infrastructure services started"
}

# ============================================================================
# PostgreSQL Import
# ============================================================================

import_postgres() {
    log INFO "Importing PostgreSQL database..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would import PostgreSQL from: ${INPUT_DIR}/postgres/"
        return
    fi
    
    # Find the dump file
    local dump_file=$(ls "${INPUT_DIR}/postgres/"*.dump | head -1)
    
    if [ -z "$dump_file" ]; then
        error_exit "No PostgreSQL dump file found"
    fi
    
    log INFO "Found dump file: $dump_file"
    
    # Load metadata
    local metadata="${INPUT_DIR}/postgres/metadata.json"
    if [ -f "$metadata" ]; then
        log INFO "Database metadata:"
        cat "$metadata" | jq -r 'to_entries[] | "  \(.key): \(.value)"' || true
    fi
    
    # Check if database already has data
    log INFO "Checking target database state..."
    local table_count=$(docker exec ghost-postgres psql -U ghost -d ghost -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>/dev/null | tr -d ' ' || echo "0")
    
    if [ "$table_count" -gt 0 ] && [ "$FORCE" = false ]; then
        error_exit "Target database already contains $table_count tables. Use --force to overwrite."
    elif [ "$table_count" -gt 0 ]; then
        log WARNING "Target database has $table_count tables, dropping them..."
        docker exec ghost-postgres psql -U ghost -d ghost -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
    fi
    
    # Copy dump file to container
    log INFO "Copying dump file to container..."
    docker cp "$dump_file" ghost-postgres:/tmp/ghost_import.dump
    
    # Restore database
    log INFO "Restoring database (this may take a while)..."
    docker exec ghost-postgres pg_restore -U ghost -d ghost -v --no-owner --no-acl /tmp/ghost_import.dump
    
    # Verify restoration
    local restored_tables=$(docker exec ghost-postgres psql -U ghost -d ghost -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" | tr -d ' ')
    
    log SUCCESS "PostgreSQL import completed: $restored_tables tables restored"
    
    # Cleanup
    docker exec ghost-postgres rm /tmp/ghost_import.dump
    
    # Run ANALYZE for query optimization
    log INFO "Analyzing database for query optimization..."
    docker exec ghost-postgres psql -U ghost -d ghost -c "ANALYZE;"
    
    log SUCCESS "PostgreSQL import and optimization completed"
}

# ============================================================================
# Redis Import
# ============================================================================

import_redis() {
    log INFO "Importing Redis data..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would import Redis from: ${INPUT_DIR}/redis/"
        return
    fi
    
    # Find the RDB file
    local rdb_file=$(ls "${INPUT_DIR}/redis/"*.rdb | head -1)
    
    if [ -z "$rdb_file" ]; then
        error_exit "No Redis RDB file found"
    fi
    
    log INFO "Found RDB file: $rdb_file"
    
    # Stop Redis to safely replace dump
    log INFO "Stopping Redis for data import..."
    docker-compose -f "${DOCKER_DIR}/docker-compose.yml" stop redis
    
    # Get Redis data volume path
    local redis_volume=$(docker volume inspect ghost_redis_data --format '{{.Mountpoint}}' 2>/dev/null || \
                         docker volume inspect miser-mode_redis_data --format '{{.Mountpoint}}' 2>/dev/null || \
                         docker volume inspect docker_redis_data --format '{{.Mountpoint}}')
    
    if [ -z "$redis_volume" ]; then
        error_exit "Could not find Redis data volume"
    fi
    
    log INFO "Redis volume path: $redis_volume"
    
    # Backup existing data if any
    if [ -f "${redis_volume}/dump.rdb" ]; then
        log INFO "Backing up existing Redis data..."
        sudo mv "${redis_volume}/dump.rdb" "${redis_volume}/dump.rdb.bak.${TIMESTAMP}"
    fi
    
    # Copy new RDB file
    log INFO "Copying Redis dump to volume..."
    sudo cp "$rdb_file" "${redis_volume}/dump.rdb"
    sudo chown 999:999 "${redis_volume}/dump.rdb" 2>/dev/null || true
    
    # Start Redis
    log INFO "Starting Redis with imported data..."
    docker-compose -f "${DOCKER_DIR}/docker-compose.yml" start redis
    
    wait_for_service redis
    
    # Verify data import
    local key_count=$(docker exec ghost-redis redis-cli DBSIZE | grep -oP '\d+')
    
    log SUCCESS "Redis import completed: $key_count keys loaded"
}

# ============================================================================
# RabbitMQ Import
# ============================================================================

import_rabbitmq() {
    log INFO "Importing RabbitMQ configuration..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would import RabbitMQ from: ${INPUT_DIR}/rabbitmq/"
        return
    fi
    
    # Find the definitions file
    local definitions_file=$(ls "${INPUT_DIR}/rabbitmq/"definitions_*.json | head -1)
    
    if [ -z "$definitions_file" ]; then
        error_exit "No RabbitMQ definitions file found"
    fi
    
    log INFO "Found definitions file: $definitions_file"
    
    # Copy definitions to container
    log INFO "Copying definitions to container..."
    docker cp "$definitions_file" ghost-rabbitmq:/tmp/definitions.json
    
    # Import definitions via management API
    log INFO "Importing RabbitMQ definitions..."
    
    # Wait a bit for RabbitMQ management to be fully ready
    sleep 5
    
    # Import using rabbitmqadmin or API
    docker exec ghost-rabbitmq rabbitmqadmin import /tmp/definitions.json 2>/dev/null || \
        docker exec ghost-rabbitmq curl -u guest:guest -H "Content-Type: application/json" \
            -X POST http://localhost:15672/api/definitions -d @/tmp/definitions.json
    
    # Verify import
    local queue_count=$(docker exec ghost-rabbitmq rabbitmqctl list_queues --silent 2>/dev/null | wc -l || echo "0")
    local exchange_count=$(docker exec ghost-rabbitmq rabbitmqctl list_exchanges --silent 2>/dev/null | wc -l || echo "0")
    
    log SUCCESS "RabbitMQ import completed: $queue_count queues, $exchange_count exchanges"
    
    # Cleanup
    docker exec ghost-rabbitmq rm /tmp/definitions.json
    
    # Check for data archive
    local data_archive=$(ls "${INPUT_DIR}/rabbitmq/"rabbitmq_data_*.tar.gz 2>/dev/null | head -1)
    
    if [ -n "$data_archive" ]; then
        log INFO "Found RabbitMQ data archive, importing persistent messages..."
        
        # Stop RabbitMQ
        docker-compose -f "${DOCKER_DIR}/docker-compose.yml" stop rabbitmq
        
        # Get volume path
        local rmq_volume=$(docker volume inspect ghost_rabbitmq_data --format '{{.Mountpoint}}' 2>/dev/null || \
                          docker volume inspect miser-mode_rabbitmq_data --format '{{.Mountpoint}}' 2>/dev/null || \
                          docker volume inspect docker_rabbitmq_data --format '{{.Mountpoint}}')
        
        if [ -n "$rmq_volume" ]; then
            log INFO "Extracting RabbitMQ data to volume..."
            sudo tar xzf "$data_archive" -C "$rmq_volume"
            
            # Start RabbitMQ
            docker-compose -f "${DOCKER_DIR}/docker-compose.yml" start rabbitmq
            wait_for_service rabbitmq
            
            log SUCCESS "RabbitMQ persistent messages restored"
        else
            log WARNING "Could not find RabbitMQ volume, skipping persistent message restore"
        fi
    fi
}

# ============================================================================
# Configuration Import
# ============================================================================

import_configuration() {
    log INFO "Importing configuration files..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would import configuration from: ${INPUT_DIR}/config/"
        return
    fi
    
    if [ ! -d "${INPUT_DIR}/config" ]; then
        log WARNING "No configuration directory found, skipping"
        return
    fi
    
    # Copy configuration files for reference
    local config_backup_dir="${DOCKER_DIR}/config-backup-${TIMESTAMP}"
    mkdir -p "$config_backup_dir"
    
    log INFO "Configuration files backed up to: $config_backup_dir"
    cp -r "${INPUT_DIR}/config/"* "$config_backup_dir/" 2>/dev/null || true
    
    log INFO "Configuration files available for manual review at: $config_backup_dir"
    log WARNING "Review and merge configuration manually as needed"
}

# ============================================================================
# Start Application Services
# ============================================================================

start_application() {
    log INFO "Starting application services..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would start application services"
        return
    fi
    
    cd "$DOCKER_DIR"
    
    # Start remaining services
    log INFO "Starting Ghost WebAPI and supporting services..."
    docker-compose up -d
    
    # Wait for application to be ready
    log INFO "Waiting for Ghost WebAPI to be healthy..."
    local max_attempts=60
    local attempt=1
    
    while [ $attempt -le $max_attempts ]; do
        if curl -sf http://localhost:8080/health >/dev/null 2>&1; then
            log SUCCESS "Ghost WebAPI is ready"
            break
        fi
        
        log INFO "Waiting for Ghost WebAPI... (attempt $attempt/$max_attempts)"
        sleep 5
        ((attempt++))
    done
    
    if [ $attempt -gt $max_attempts ]; then
        log WARNING "Ghost WebAPI health check timed out, but continuing..."
    fi
    
    log SUCCESS "All services started"
}

# ============================================================================
# Create Import Summary
# ============================================================================

create_import_summary() {
    local summary_file="${DOCKER_DIR}/IMPORT_SUMMARY_${TIMESTAMP}.txt"
    
    cat > "$summary_file" <<EOF
╔═══════════════════════════════════════════════════════════════╗
║            Ghost Platform Data Import Summary                 ║
╚═══════════════════════════════════════════════════════════════╝

Import Date: $(date)
Import Source: $INPUT_DIR
Target Host: $TARGET_HOST

Imported Components:
  ✓ PostgreSQL Database
  ✓ Redis Cache
  ✓ RabbitMQ Configuration
  ✓ Configuration Files (backed up)

Service Status:
EOF
    
    docker-compose -f "${DOCKER_DIR}/docker-compose.yml" ps >> "$summary_file"
    
    cat >> "$summary_file" <<EOF

Service URLs:
  - API Health: http://localhost:8080/health
  - API Swagger: http://localhost:8080/swagger
  - RabbitMQ Management: http://localhost:15672 (guest/guest)
  - Grafana: http://localhost:3000 (admin/admin)
  - Prometheus: http://localhost:9090

Next Steps:
  1. Verify service health: docker-compose ps
  2. Check logs: docker-compose logs -f ghost-webapi
  3. Test API endpoints
  4. Run validation: ./validate-migration.sh
  5. Review configuration backup: ${DOCKER_DIR}/config-backup-${TIMESTAMP}

For more information, see the migration documentation.
EOF
    
    log SUCCESS "Import summary created: $summary_file"
    cat "$summary_file"
}

# ============================================================================
# Main Function
# ============================================================================

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --input-dir)
                INPUT_DIR="$2"
                shift 2
                ;;
            --target-host)
                TARGET_HOST="$2"
                shift 2
                ;;
            --force)
                FORCE=true
                shift
                ;;
            --skip-postgres)
                SKIP_POSTGRES=true
                shift
                ;;
            --skip-redis)
                SKIP_REDIS=true
                shift
                ;;
            --skip-rabbitmq)
                SKIP_RABBITMQ=true
                shift
                ;;
            --help)
                grep "^#" "$0" | grep -v "#!/bin/bash" | sed 's/^# //g' | sed 's/^#//g'
                exit 0
                ;;
            *)
                error_exit "Unknown option: $1"
                ;;
        esac
    done
}

main() {
    parse_arguments "$@"
    
    # Validate input directory
    if [ -z "$INPUT_DIR" ]; then
        error_exit "Input directory must be specified with --input-dir"
    fi
    
    if [ ! -d "$INPUT_DIR" ]; then
        error_exit "Input directory not found: $INPUT_DIR"
    fi
    
    log INFO "Starting data import..."
    log INFO "Input directory: $INPUT_DIR"
    log INFO "Target host: $TARGET_HOST"
    
    # Verify export integrity
    verify_export_integrity
    
    # Start services
    start_services
    
    # Import data
    if [ "$SKIP_POSTGRES" = false ]; then
        import_postgres
    else
        log WARNING "Skipping PostgreSQL import"
    fi
    
    if [ "$SKIP_REDIS" = false ]; then
        import_redis
    else
        log WARNING "Skipping Redis import"
    fi
    
    if [ "$SKIP_RABBITMQ" = false ]; then
        import_rabbitmq
    else
        log WARNING "Skipping RabbitMQ import"
    fi
    
    import_configuration
    
    # Start application
    start_application
    
    # Create summary
    create_import_summary
    
    log SUCCESS "Data import completed successfully!"
    log INFO "Services are now running. Run validate-migration.sh to verify."
}

main "$@"
