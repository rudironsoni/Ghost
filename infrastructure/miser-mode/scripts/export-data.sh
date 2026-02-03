#!/bin/bash
# ============================================================================
# Ghost Platform - Data Export Script
# ============================================================================
#
# Exports all data from a distributed Ghost Platform deployment including:
# - PostgreSQL database (full dump with schemas and data)
# - Redis data (RDB snapshot)
# - RabbitMQ configuration and messages
# - Application configuration files
# - Secrets and credentials
#
# Usage:
#   ./export-data.sh [OPTIONS]
#
# Options:
#   --dry-run              Simulate export without actual data transfer
#   --host HOST            Source system hostname/IP
#   --user USER            SSH user for source system (default: current user)
#   --output-dir DIR       Output directory for exported data
#   --compress             Compress exported data (default: true)
#   --skip-postgres        Skip PostgreSQL export
#   --skip-redis           Skip Redis export
#   --skip-rabbitmq        Skip RabbitMQ export
#   --help                 Show this help message
#
# ============================================================================

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Default values
DRY_RUN=false
SOURCE_HOST=""
SOURCE_USER="${USER}"
OUTPUT_DIR=""
COMPRESS=true
SKIP_POSTGRES=false
SKIP_REDIS=false
SKIP_RABBITMQ=false

# Export metadata
EXPORT_MANIFEST=""

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

run_remote() {
    local command=$1
    if [ -n "$SOURCE_HOST" ]; then
        ssh "${SOURCE_USER}@${SOURCE_HOST}" "$command"
    else
        bash -c "$command"
    fi
}

copy_from_remote() {
    local source=$1
    local dest=$2
    
    if [ -n "$SOURCE_HOST" ]; then
        scp -r "${SOURCE_USER}@${SOURCE_HOST}:${source}" "$dest"
    else
        cp -r "$source" "$dest"
    fi
}

add_to_manifest() {
    local item_type=$1
    local item_name=$2
    local item_path=$3
    local item_size=$4
    local item_checksum=$5
    
    cat >> "$EXPORT_MANIFEST" <<EOF
{
  "type": "$item_type",
  "name": "$item_name",
  "path": "$item_path",
  "size_bytes": $item_size,
  "checksum": "$item_checksum",
  "exported_at": "$(date -Iseconds)"
}
EOF
}

# ============================================================================
# PostgreSQL Export
# ============================================================================

export_postgres() {
    log INFO "Exporting PostgreSQL database..."
    
    local pg_dir="${OUTPUT_DIR}/postgres"
    mkdir -p "$pg_dir"
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would export PostgreSQL to: $pg_dir"
        return
    fi
    
    # Detect PostgreSQL connection parameters from source
    log INFO "Detecting PostgreSQL configuration..."
    
    local pg_host="localhost"
    local pg_port="5432"
    local pg_database="ghost"
    local pg_user="ghost"
    
    # Check if running in Docker
    if run_remote "docker ps --filter name=postgres --format '{{.Names}}' 2>/dev/null" | grep -q postgres; then
        log INFO "PostgreSQL running in Docker on source system"
        
        # Export using docker exec
        local container_name=$(run_remote "docker ps --filter name=postgres --format '{{.Names}}' | head -1")
        
        log INFO "Creating database dump from container: $container_name"
        
        if [ -n "$SOURCE_HOST" ]; then
            # Remote export
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name pg_dump -U $pg_user -d $pg_database --format=custom --compress=9 --verbose" > "${pg_dir}/ghost_db_${TIMESTAMP}.dump"
        else
            # Local export
            docker exec "$container_name" pg_dump -U "$pg_user" -d "$pg_database" --format=custom --compress=9 --verbose > "${pg_dir}/ghost_db_${TIMESTAMP}.dump"
        fi
        
        # Also export schema separately for reference
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name pg_dump -U $pg_user -d $pg_database --schema-only" > "${pg_dir}/ghost_schema_${TIMESTAMP}.sql"
        else
            docker exec "$container_name" pg_dump -U "$pg_user" -d "$pg_database" --schema-only > "${pg_dir}/ghost_schema_${TIMESTAMP}.sql"
        fi
        
    else
        log INFO "PostgreSQL running as native service"
        
        # Native PostgreSQL export
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "pg_dump -h $pg_host -p $pg_port -U $pg_user -d $pg_database --format=custom --compress=9 --verbose" > "${pg_dir}/ghost_db_${TIMESTAMP}.dump"
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "pg_dump -h $pg_host -p $pg_port -U $pg_user -d $pg_database --schema-only" > "${pg_dir}/ghost_schema_${TIMESTAMP}.sql"
        else
            pg_dump -h "$pg_host" -p "$pg_port" -U "$pg_user" -d "$pg_database" --format=custom --compress=9 --verbose > "${pg_dir}/ghost_db_${TIMESTAMP}.dump"
            pg_dump -h "$pg_host" -p "$pg_port" -U "$pg_user" -d "$pg_database" --schema-only > "${pg_dir}/ghost_schema_${TIMESTAMP}.sql"
        fi
    fi
    
    # Get file size and checksum
    local dump_size=$(stat -f%z "${pg_dir}/ghost_db_${TIMESTAMP}.dump" 2>/dev/null || stat -c%s "${pg_dir}/ghost_db_${TIMESTAMP}.dump")
    local dump_checksum=$(sha256sum "${pg_dir}/ghost_db_${TIMESTAMP}.dump" | awk '{print $1}')
    
    log SUCCESS "PostgreSQL export completed: ${dump_size} bytes"
    log INFO "Checksum: $dump_checksum"
    
    # Add to manifest
    echo "  \"postgres\": " >> "$EXPORT_MANIFEST"
    add_to_manifest "database" "ghost" "${pg_dir}/ghost_db_${TIMESTAMP}.dump" "$dump_size" "$dump_checksum"
    echo "," >> "$EXPORT_MANIFEST"
    
    # Create metadata file
    cat > "${pg_dir}/metadata.json" <<EOF
{
  "export_timestamp": "$(date -Iseconds)",
  "database": "$pg_database",
  "user": "$pg_user",
  "dump_file": "ghost_db_${TIMESTAMP}.dump",
  "schema_file": "ghost_schema_${TIMESTAMP}.sql",
  "format": "custom",
  "compression": 9,
  "size_bytes": $dump_size,
  "checksum": "$dump_checksum"
}
EOF
}

# ============================================================================
# Redis Export
# ============================================================================

export_redis() {
    log INFO "Exporting Redis data..."
    
    local redis_dir="${OUTPUT_DIR}/redis"
    mkdir -p "$redis_dir"
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would export Redis to: $redis_dir"
        return
    fi
    
    # Trigger Redis save
    log INFO "Triggering Redis BGSAVE..."
    
    if run_remote "docker ps --filter name=redis --format '{{.Names}}' 2>/dev/null" | grep -q redis; then
        local container_name=$(run_remote "docker ps --filter name=redis --format '{{.Names}}' | head -1")
        
        # Trigger background save
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name redis-cli BGSAVE"
            
            # Wait for save to complete
            log INFO "Waiting for Redis save to complete..."
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name redis-cli --csv LASTSAVE" > /dev/null
            sleep 2
            
            # Copy RDB file
            log INFO "Copying Redis dump file..."
            local redis_data_path=$(ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker inspect -f '{{range .Mounts}}{{if eq .Destination \"/data\"}}{{.Source}}{{end}}{{end}}' $container_name")
            
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "sudo cat ${redis_data_path}/dump.rdb" > "${redis_dir}/redis_dump_${TIMESTAMP}.rdb"
        else
            docker exec "$container_name" redis-cli BGSAVE
            sleep 2
            
            local redis_data_path=$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/data"}}{{.Source}}{{end}}{{end}}' "$container_name")
            sudo cp "${redis_data_path}/dump.rdb" "${redis_dir}/redis_dump_${TIMESTAMP}.rdb"
        fi
        
        # Export Redis configuration
        log INFO "Exporting Redis configuration..."
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name redis-cli CONFIG GET '*'" > "${redis_dir}/redis_config_${TIMESTAMP}.txt"
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name redis-cli INFO ALL" > "${redis_dir}/redis_info_${TIMESTAMP}.txt"
        else
            docker exec "$container_name" redis-cli CONFIG GET '*' > "${redis_dir}/redis_config_${TIMESTAMP}.txt"
            docker exec "$container_name" redis-cli INFO ALL > "${redis_dir}/redis_info_${TIMESTAMP}.txt"
        fi
        
    else
        log WARNING "Redis container not found, attempting native Redis export"
        
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "redis-cli BGSAVE"
            sleep 2
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "cat /var/lib/redis/dump.rdb" > "${redis_dir}/redis_dump_${TIMESTAMP}.rdb"
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "redis-cli CONFIG GET '*'" > "${redis_dir}/redis_config_${TIMESTAMP}.txt"
        else
            redis-cli BGSAVE
            sleep 2
            cp /var/lib/redis/dump.rdb "${redis_dir}/redis_dump_${TIMESTAMP}.rdb"
            redis-cli CONFIG GET '*' > "${redis_dir}/redis_config_${TIMESTAMP}.txt"
        fi
    fi
    
    local rdb_size=$(stat -f%z "${redis_dir}/redis_dump_${TIMESTAMP}.rdb" 2>/dev/null || stat -c%s "${redis_dir}/redis_dump_${TIMESTAMP}.rdb")
    local rdb_checksum=$(sha256sum "${redis_dir}/redis_dump_${TIMESTAMP}.rdb" | awk '{print $1}')
    
    log SUCCESS "Redis export completed: ${rdb_size} bytes"
    log INFO "Checksum: $rdb_checksum"
    
    # Add to manifest
    echo "  \"redis\": " >> "$EXPORT_MANIFEST"
    add_to_manifest "cache" "redis" "${redis_dir}/redis_dump_${TIMESTAMP}.rdb" "$rdb_size" "$rdb_checksum"
    echo "," >> "$EXPORT_MANIFEST"
    
    # Create metadata
    cat > "${redis_dir}/metadata.json" <<EOF
{
  "export_timestamp": "$(date -Iseconds)",
  "dump_file": "redis_dump_${TIMESTAMP}.rdb",
  "config_file": "redis_config_${TIMESTAMP}.txt",
  "info_file": "redis_info_${TIMESTAMP}.txt",
  "size_bytes": $rdb_size,
  "checksum": "$rdb_checksum"
}
EOF
}

# ============================================================================
# RabbitMQ Export
# ============================================================================

export_rabbitmq() {
    log INFO "Exporting RabbitMQ data..."
    
    local rabbitmq_dir="${OUTPUT_DIR}/rabbitmq"
    mkdir -p "$rabbitmq_dir"
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would export RabbitMQ to: $rabbitmq_dir"
        return
    fi
    
    if run_remote "docker ps --filter name=rabbitmq --format '{{.Names}}' 2>/dev/null" | grep -q rabbitmq; then
        local container_name=$(run_remote "docker ps --filter name=rabbitmq --format '{{.Names}}' | head -1")
        
        # Export definitions (exchanges, queues, bindings, etc.)
        log INFO "Exporting RabbitMQ definitions..."
        
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker exec $container_name rabbitmqadmin export ${rabbitmq_dir}/definitions_${TIMESTAMP}.json" || \
                ssh "${SOURCE_USER}@${SOURCE_HOST}" "curl -u guest:guest http://localhost:15672/api/definitions" > "${rabbitmq_dir}/definitions_${TIMESTAMP}.json"
        else
            docker exec "$container_name" rabbitmqadmin export "${rabbitmq_dir}/definitions_${TIMESTAMP}.json" || \
                curl -u guest:guest http://localhost:15672/api/definitions > "${rabbitmq_dir}/definitions_${TIMESTAMP}.json"
        fi
        
        # Export message data (if any persistent messages exist)
        log INFO "Checking for persistent messages..."
        
        if [ -n "$SOURCE_HOST" ]; then
            local rmq_data_path=$(ssh "${SOURCE_USER}@${SOURCE_HOST}" "docker inspect -f '{{range .Mounts}}{{if eq .Destination \"/var/lib/rabbitmq\"}}{{.Source}}{{end}}{{end}}' $container_name")
            
            # Create tarball of RabbitMQ data directory
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "sudo tar czf /tmp/rabbitmq_data_${TIMESTAMP}.tar.gz -C ${rmq_data_path} ." && \
                scp "${SOURCE_USER}@${SOURCE_HOST}:/tmp/rabbitmq_data_${TIMESTAMP}.tar.gz" "${rabbitmq_dir}/" && \
                ssh "${SOURCE_USER}@${SOURCE_HOST}" "sudo rm /tmp/rabbitmq_data_${TIMESTAMP}.tar.gz"
        else
            local rmq_data_path=$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/var/lib/rabbitmq"}}{{.Source}}{{end}}{{end}}' "$container_name")
            sudo tar czf "${rabbitmq_dir}/rabbitmq_data_${TIMESTAMP}.tar.gz" -C "${rmq_data_path}" .
        fi
        
    else
        log WARNING "RabbitMQ container not found, attempting native export"
        
        if [ -n "$SOURCE_HOST" ]; then
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "rabbitmqadmin export ${rabbitmq_dir}/definitions_${TIMESTAMP}.json"
            ssh "${SOURCE_USER}@${SOURCE_HOST}" "sudo tar czf /tmp/rabbitmq_data_${TIMESTAMP}.tar.gz -C /var/lib/rabbitmq ." && \
                scp "${SOURCE_USER}@${SOURCE_HOST}:/tmp/rabbitmq_data_${TIMESTAMP}.tar.gz" "${rabbitmq_dir}/"
        else
            rabbitmqadmin export "${rabbitmq_dir}/definitions_${TIMESTAMP}.json"
            sudo tar czf "${rabbitmq_dir}/rabbitmq_data_${TIMESTAMP}.tar.gz" -C /var/lib/rabbitmq .
        fi
    fi
    
    local def_size=$(stat -f%z "${rabbitmq_dir}/definitions_${TIMESTAMP}.json" 2>/dev/null || stat -c%s "${rabbitmq_dir}/definitions_${TIMESTAMP}.json")
    local def_checksum=$(sha256sum "${rabbitmq_dir}/definitions_${TIMESTAMP}.json" | awk '{print $1}')
    
    log SUCCESS "RabbitMQ export completed"
    log INFO "Definitions checksum: $def_checksum"
    
    # Add to manifest
    echo "  \"rabbitmq\": " >> "$EXPORT_MANIFEST"
    add_to_manifest "messagebroker" "rabbitmq" "${rabbitmq_dir}/definitions_${TIMESTAMP}.json" "$def_size" "$def_checksum"
    echo "," >> "$EXPORT_MANIFEST"
    
    # Create metadata
    cat > "${rabbitmq_dir}/metadata.json" <<EOF
{
  "export_timestamp": "$(date -Iseconds)",
  "definitions_file": "definitions_${TIMESTAMP}.json",
  "data_archive": "rabbitmq_data_${TIMESTAMP}.tar.gz",
  "definitions_size_bytes": $def_size,
  "checksum": "$def_checksum"
}
EOF
}

# ============================================================================
# Configuration Export
# ============================================================================

export_configuration() {
    log INFO "Exporting configuration files..."
    
    local config_dir="${OUTPUT_DIR}/config"
    mkdir -p "$config_dir"
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would export configuration to: $config_dir"
        return
    fi
    
    # Export environment files
    log INFO "Exporting environment configuration..."
    
    if [ -n "$SOURCE_HOST" ]; then
        # Try to find .env files
        ssh "${SOURCE_USER}@${SOURCE_HOST}" "find /opt/ghost /var/ghost /home -name '.env' -o -name 'appsettings*.json' 2>/dev/null" | while read -r config_file; do
            local filename=$(basename "$config_file")
            scp "${SOURCE_USER}@${SOURCE_HOST}:${config_file}" "${config_dir}/${filename}.bak" 2>/dev/null || true
        done
    else
        find /opt/ghost /var/ghost . -name '.env' -o -name 'appsettings*.json' 2>/dev/null | while read -r config_file; do
            local filename=$(basename "$config_file")
            cp "$config_file" "${config_dir}/${filename}.bak" 2>/dev/null || true
        done
    fi
    
    log SUCCESS "Configuration export completed"
    
    # Create metadata
    cat > "${config_dir}/metadata.json" <<EOF
{
  "export_timestamp": "$(date -Iseconds)",
  "note": "Configuration files exported with .bak extension for safety"
}
EOF
}

# ============================================================================
# Create Export Summary
# ============================================================================

create_export_summary() {
    log INFO "Creating export summary..."
    
    # Finalize manifest
    echo "  \"export_completed_at\": \"$(date -Iseconds)\"" >> "$EXPORT_MANIFEST"
    echo "}" >> "$EXPORT_MANIFEST"
    
    # Calculate total size
    local total_size=$(du -sb "$OUTPUT_DIR" | awk '{print $1}')
    local total_size_mb=$((total_size / 1024 / 1024))
    
    # Create summary file
    cat > "${OUTPUT_DIR}/EXPORT_SUMMARY.txt" <<EOF
╔═══════════════════════════════════════════════════════════════╗
║            Ghost Platform Data Export Summary                 ║
╚═══════════════════════════════════════════════════════════════╝

Export Date: $(date)
Source Host: ${SOURCE_HOST:-localhost}
Export Directory: $OUTPUT_DIR
Total Size: ${total_size_mb} MB

Exported Components:
  ✓ PostgreSQL Database
  ✓ Redis Cache
  ✓ RabbitMQ Configuration
  ✓ Application Configuration

Files:
  - PostgreSQL: $(ls -lh "${OUTPUT_DIR}/postgres/ghost_db_"*.dump 2>/dev/null | awk '{print $5}' || echo "N/A")
  - Redis: $(ls -lh "${OUTPUT_DIR}/redis/redis_dump_"*.rdb 2>/dev/null | awk '{print $5}' || echo "N/A")
  - RabbitMQ: $(ls -lh "${OUTPUT_DIR}/rabbitmq/definitions_"*.json 2>/dev/null | awk '{print $5}' || echo "N/A")

Checksums:
  See individual metadata.json files in each subdirectory

Next Steps:
  1. Verify export integrity: check metadata.json files
  2. Transfer to target system if needed
  3. Run import-data.sh to restore on target system

For detailed information, see: ${OUTPUT_DIR}/export_manifest.json
EOF
    
    log SUCCESS "Export summary created"
    cat "${OUTPUT_DIR}/EXPORT_SUMMARY.txt"
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
            --host)
                SOURCE_HOST="$2"
                shift 2
                ;;
            --user)
                SOURCE_USER="$2"
                shift 2
                ;;
            --output-dir)
                OUTPUT_DIR="$2"
                shift 2
                ;;
            --no-compress)
                COMPRESS=false
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
    
    # Set default output directory if not specified
    if [ -z "$OUTPUT_DIR" ]; then
        OUTPUT_DIR="${SCRIPT_DIR}/../backups/export_${TIMESTAMP}"
    fi
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Initialize manifest
    EXPORT_MANIFEST="${OUTPUT_DIR}/export_manifest.json"
    echo "{" > "$EXPORT_MANIFEST"
    echo "  \"export_started_at\": \"$(date -Iseconds)\"," >> "$EXPORT_MANIFEST"
    echo "  \"source_host\": \"${SOURCE_HOST:-localhost}\"," >> "$EXPORT_MANIFEST"
    echo "  \"dry_run\": $DRY_RUN," >> "$EXPORT_MANIFEST"
    
    log INFO "Starting data export..."
    log INFO "Output directory: $OUTPUT_DIR"
    
    # Export components
    if [ "$SKIP_POSTGRES" = false ]; then
        export_postgres
    else
        log WARNING "Skipping PostgreSQL export"
    fi
    
    if [ "$SKIP_REDIS" = false ]; then
        export_redis
    else
        log WARNING "Skipping Redis export"
    fi
    
    if [ "$SKIP_RABBITMQ" = false ]; then
        export_rabbitmq
    else
        log WARNING "Skipping RabbitMQ export"
    fi
    
    export_configuration
    
    # Create summary
    create_export_summary
    
    log SUCCESS "Data export completed successfully!"
    log INFO "Export location: $OUTPUT_DIR"
}

main "$@"
