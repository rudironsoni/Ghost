#!/bin/bash
# ============================================================================
# Ghost Platform - Ultra Miser Mode Migration Orchestrator
# ============================================================================
# 
# This script orchestrates the complete migration from a distributed 
# Ghost Platform deployment to a single-node Ultra Miser Mode deployment.
#
# Usage:
#   ./migrate.sh [OPTIONS]
#
# Options:
#   --dry-run              Run in dry-run mode (no actual changes)
#   --interactive          Run in interactive mode with prompts
#   --source-host HOST     Source system hostname/IP
#   --source-user USER     SSH user for source system (default: current user)
#   --target-host HOST     Target system hostname/IP (default: localhost)
#   --skip-validation      Skip pre-migration validation
#   --skip-backup          Skip backup creation (not recommended)
#   --config FILE          Load configuration from file
#   --help                 Show this help message
#
# Examples:
#   ./migrate.sh --dry-run --source-host prod.example.com
#   ./migrate.sh --interactive --config migration.conf
#   ./migrate.sh --source-host 10.0.1.50 --target-host localhost
#
# ============================================================================

set -euo pipefail

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATION_DIR="$(dirname "$SCRIPT_DIR")"
LOG_DIR="${MIGRATION_DIR}/logs"
BACKUP_DIR="${MIGRATION_DIR}/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${LOG_DIR}/migration_${TIMESTAMP}.log"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
DRY_RUN=false
INTERACTIVE=false
SOURCE_HOST=""
SOURCE_USER="${USER}"
TARGET_HOST="localhost"
SKIP_VALIDATION=false
SKIP_BACKUP=false
CONFIG_FILE=""
MIGRATION_STATE_FILE="${LOG_DIR}/migration_state_${TIMESTAMP}.json"

# Migration phases
declare -A MIGRATION_PHASES=(
    [1]="Prerequisites Check"
    [2]="Source System Export"
    [3]="Target System Preparation"
    [4]="Data Import"
    [5]="Validation"
    [6]="Cleanup"
)

# ============================================================================
# Utility Functions
# ============================================================================

log() {
    local level=$1
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    echo "[$timestamp] [$level] $message" | tee -a "$LOG_FILE"
    
    case $level in
        ERROR)
            echo -e "${RED}✗ $message${NC}" >&2
            ;;
        SUCCESS)
            echo -e "${GREEN}✓ $message${NC}"
            ;;
        WARNING)
            echo -e "${YELLOW}⚠ $message${NC}"
            ;;
        INFO)
            echo -e "${BLUE}ℹ $message${NC}"
            ;;
    esac
}

error_exit() {
    log ERROR "$1"
    update_state "failed" "$1"
    exit 1
}

update_state() {
    local status=$1
    local message=${2:-""}
    local current_time=$(date -Iseconds)
    
    cat > "$MIGRATION_STATE_FILE" <<EOF
{
    "timestamp": "$current_time",
    "status": "$status",
    "message": "$message",
    "source_host": "$SOURCE_HOST",
    "target_host": "$TARGET_HOST",
    "dry_run": $DRY_RUN
}
EOF
}

confirm() {
    if [ "$INTERACTIVE" = true ]; then
        read -p "$1 (yes/no): " response
        case "$response" in
            [yY][eE][sS]|[yY]) 
                return 0
                ;;
            *)
                return 1
                ;;
        esac
    fi
    return 0
}

check_command() {
    if ! command -v "$1" &> /dev/null; then
        error_exit "Required command '$1' not found. Please install it first."
    fi
}

print_banner() {
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════╗"
    echo "║        Ghost Platform - Ultra Miser Mode Migration           ║"
    echo "║                                                               ║"
    echo "║  Migrating from distributed to single-node deployment        ║"
    echo "╚═══════════════════════════════════════════════════════════════╝"
    echo ""
}

print_config() {
    log INFO "Migration Configuration:"
    echo "  Source Host:     $SOURCE_HOST"
    echo "  Source User:     $SOURCE_USER"
    echo "  Target Host:     $TARGET_HOST"
    echo "  Dry Run:         $DRY_RUN"
    echo "  Interactive:     $INTERACTIVE"
    echo "  Log File:        $LOG_FILE"
    echo "  Backup Dir:      $BACKUP_DIR"
    echo ""
}

# ============================================================================
# Configuration Loading
# ============================================================================

load_config() {
    if [ -n "$CONFIG_FILE" ] && [ -f "$CONFIG_FILE" ]; then
        log INFO "Loading configuration from: $CONFIG_FILE"
        # Source the config file safely
        set -a
        # shellcheck disable=SC1090
        source "$CONFIG_FILE"
        set +a
    fi
}

# ============================================================================
# Phase 1: Prerequisites Check
# ============================================================================

check_prerequisites() {
    log INFO "Phase 1: Checking prerequisites..."
    
    # Check required commands
    local required_commands=(
        "docker"
        "docker-compose"
        "pg_dump"
        "psql"
        "redis-cli"
        "rabbitmqadmin"
        "ssh"
        "scp"
        "jq"
        "tar"
        "gzip"
    )
    
    for cmd in "${required_commands[@]}"; do
        check_command "$cmd"
    done
    
    log SUCCESS "All required commands are available"
    
    # Check disk space
    local required_space_gb=20
    local available_space=$(df -BG "$BACKUP_DIR" | awk 'NR==2 {print $4}' | sed 's/G//')
    
    if [ "$available_space" -lt "$required_space_gb" ]; then
        log WARNING "Low disk space: ${available_space}GB available, ${required_space_gb}GB recommended"
        if ! confirm "Continue anyway?"; then
            error_exit "Insufficient disk space. Please free up space and try again."
        fi
    else
        log SUCCESS "Sufficient disk space available: ${available_space}GB"
    fi
    
    # Check source connectivity
    if [ -n "$SOURCE_HOST" ]; then
        log INFO "Checking connectivity to source host: $SOURCE_HOST"
        if ! ssh -o ConnectTimeout=5 "${SOURCE_USER}@${SOURCE_HOST}" "echo 'Connection test'" &>/dev/null; then
            error_exit "Cannot connect to source host: $SOURCE_HOST"
        fi
        log SUCCESS "Source host is reachable"
    fi
    
    # Check if target Docker is running
    if ! docker info &>/dev/null; then
        error_exit "Docker is not running on target system"
    fi
    log SUCCESS "Docker is running"
    
    # Check existing containers
    if [ "$TARGET_HOST" = "localhost" ]; then
        local existing_containers=$(docker ps -a --filter "name=ghost-*" --format "{{.Names}}" | wc -l)
        if [ "$existing_containers" -gt 0 ]; then
            log WARNING "Found $existing_containers existing Ghost containers"
            if ! confirm "Existing containers will be stopped. Continue?"; then
                error_exit "Migration cancelled by user"
            fi
        fi
    fi
    
    log SUCCESS "Prerequisites check completed"
}

# ============================================================================
# Phase 2: Source System Export
# ============================================================================

export_source_data() {
    log INFO "Phase 2: Exporting data from source system..."
    
    local export_script="${SCRIPT_DIR}/export-data.sh"
    local export_options=""
    
    if [ "$DRY_RUN" = true ]; then
        export_options="--dry-run"
    fi
    
    if [ -n "$SOURCE_HOST" ]; then
        export_options="$export_options --host $SOURCE_HOST --user $SOURCE_USER"
    fi
    
    export_options="$export_options --output-dir $BACKUP_DIR/export_${TIMESTAMP}"
    
    if [ ! -f "$export_script" ]; then
        error_exit "Export script not found: $export_script"
    fi
    
    log INFO "Running export script with options: $export_options"
    
    if ! bash "$export_script" $export_options 2>&1 | tee -a "$LOG_FILE"; then
        error_exit "Data export failed. Check logs for details."
    fi
    
    log SUCCESS "Data export completed"
}

# ============================================================================
# Phase 3: Target System Preparation
# ============================================================================

prepare_target_system() {
    log INFO "Phase 3: Preparing target system..."
    
    # Create necessary directories
    mkdir -p "${MIGRATION_DIR}/docker/backups"/{postgres,redis,rabbitmq}
    mkdir -p "${MIGRATION_DIR}/docker/logs"
    mkdir -p "${MIGRATION_DIR}/docker/init-scripts"
    
    # Stop existing containers if any
    if [ "$TARGET_HOST" = "localhost" ] && [ "$DRY_RUN" = false ]; then
        log INFO "Stopping existing Ghost containers..."
        docker-compose -f "${MIGRATION_DIR}/docker/docker-compose.yml" down 2>&1 | tee -a "$LOG_FILE" || true
    fi
    
    # Check if .env file exists
    if [ ! -f "${MIGRATION_DIR}/docker/.env" ]; then
        log WARNING ".env file not found in ${MIGRATION_DIR}/docker/"
        if [ -f "${MIGRATION_DIR}/docker/.env.example" ]; then
            if confirm "Create .env from .env.example?"; then
                cp "${MIGRATION_DIR}/docker/.env.example" "${MIGRATION_DIR}/docker/.env"
                log WARNING "Please review and update ${MIGRATION_DIR}/docker/.env with your credentials"
                if [ "$INTERACTIVE" = true ]; then
                    read -p "Press Enter after updating .env file..."
                fi
            fi
        else
            error_exit ".env.example not found. Cannot proceed without environment configuration."
        fi
    fi
    
    log SUCCESS "Target system prepared"
}

# ============================================================================
# Phase 4: Data Import
# ============================================================================

import_data() {
    log INFO "Phase 4: Importing data to target system..."
    
    local import_script="${SCRIPT_DIR}/import-data.sh"
    local import_options=""
    
    if [ "$DRY_RUN" = true ]; then
        import_options="--dry-run"
    fi
    
    import_options="$import_options --input-dir $BACKUP_DIR/export_${TIMESTAMP}"
    import_options="$import_options --target-host $TARGET_HOST"
    
    if [ ! -f "$import_script" ]; then
        error_exit "Import script not found: $import_script"
    fi
    
    log INFO "Running import script with options: $import_options"
    
    if ! bash "$import_script" $import_options 2>&1 | tee -a "$LOG_FILE"; then
        error_exit "Data import failed. Check logs for details."
    fi
    
    log SUCCESS "Data import completed"
}

# ============================================================================
# Phase 5: Validation
# ============================================================================

validate_migration() {
    log INFO "Phase 5: Validating migration..."
    
    local validation_script="${SCRIPT_DIR}/validate-migration.sh"
    local validation_options=""
    
    if [ "$DRY_RUN" = true ]; then
        validation_options="--dry-run"
    fi
    
    validation_options="$validation_options --export-dir $BACKUP_DIR/export_${TIMESTAMP}"
    validation_options="$validation_options --target-host $TARGET_HOST"
    
    if [ ! -f "$validation_script" ]; then
        error_exit "Validation script not found: $validation_script"
    fi
    
    log INFO "Running validation script with options: $validation_options"
    
    if ! bash "$validation_script" $validation_options 2>&1 | tee -a "$LOG_FILE"; then
        error_exit "Migration validation failed. Check logs for details."
    fi
    
    log SUCCESS "Migration validation completed"
}

# ============================================================================
# Phase 6: Cleanup
# ============================================================================

cleanup() {
    log INFO "Phase 6: Cleaning up temporary files..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would clean up temporary files"
        return
    fi
    
    # Keep backups but clean up temporary files
    find "$BACKUP_DIR/export_${TIMESTAMP}" -name "*.tmp" -delete 2>/dev/null || true
    
    log SUCCESS "Cleanup completed"
}

# ============================================================================
# Rollback Function
# ============================================================================

rollback() {
    log WARNING "Initiating rollback procedure..."
    
    if [ "$DRY_RUN" = true ]; then
        log INFO "[DRY RUN] Would perform rollback"
        return
    fi
    
    # Stop new containers
    docker-compose -f "${MIGRATION_DIR}/docker/docker-compose.yml" down 2>&1 | tee -a "$LOG_FILE" || true
    
    log WARNING "Rollback completed. Source system should still be intact."
    log INFO "Review logs at: $LOG_FILE"
}

# ============================================================================
# Generate Migration Report
# ============================================================================

generate_report() {
    local report_file="${LOG_DIR}/migration_report_${TIMESTAMP}.txt"
    
    cat > "$report_file" <<EOF
╔═══════════════════════════════════════════════════════════════╗
║           Ghost Platform Migration Report                     ║
╚═══════════════════════════════════════════════════════════════╝

Migration Date: $(date)
Migration Status: $1

Configuration:
  Source Host:     $SOURCE_HOST
  Target Host:     $TARGET_HOST
  Dry Run:         $DRY_RUN
  Interactive:     $INTERACTIVE

Artifacts:
  Log File:        $LOG_FILE
  State File:      $MIGRATION_STATE_FILE
  Export Dir:      $BACKUP_DIR/export_${TIMESTAMP}
  Report File:     $report_file

Migration Phases:
EOF
    
    for phase_num in {1..6}; do
        echo "  [$phase_num] ${MIGRATION_PHASES[$phase_num]}" >> "$report_file"
    done
    
    echo "" >> "$report_file"
    echo "For detailed logs, see: $LOG_FILE" >> "$report_file"
    echo "" >> "$report_file"
    
    log INFO "Migration report generated: $report_file"
    cat "$report_file"
}

# ============================================================================
# Main Execution
# ============================================================================

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --interactive)
                INTERACTIVE=true
                shift
                ;;
            --source-host)
                SOURCE_HOST="$2"
                shift 2
                ;;
            --source-user)
                SOURCE_USER="$2"
                shift 2
                ;;
            --target-host)
                TARGET_HOST="$2"
                shift 2
                ;;
            --skip-validation)
                SKIP_VALIDATION=true
                shift
                ;;
            --skip-backup)
                SKIP_BACKUP=true
                shift
                ;;
            --config)
                CONFIG_FILE="$2"
                shift 2
                ;;
            --help)
                grep "^#" "$0" | grep -v "#!/bin/bash" | sed 's/^# //g' | sed 's/^#//g'
                exit 0
                ;;
            *)
                error_exit "Unknown option: $1. Use --help for usage information."
                ;;
        esac
    done
}

main() {
    # Create log directory
    mkdir -p "$LOG_DIR"
    mkdir -p "$BACKUP_DIR"
    
    # Parse command line arguments
    parse_arguments "$@"
    
    # Load configuration file if specified
    load_config
    
    # Print banner
    print_banner
    
    # Print configuration
    print_config
    
    # Validate source host is provided
    if [ -z "$SOURCE_HOST" ]; then
        error_exit "Source host must be specified with --source-host"
    fi
    
    # Initialize state
    update_state "started" "Migration initiated"
    
    # Execute migration phases
    trap 'rollback' ERR
    
    if [ "$SKIP_VALIDATION" = false ]; then
        check_prerequisites
    else
        log WARNING "Skipping prerequisites check (--skip-validation)"
    fi
    
    export_source_data
    prepare_target_system
    import_data
    validate_migration
    cleanup
    
    # Update final state
    update_state "completed" "Migration completed successfully"
    
    # Generate report
    generate_report "SUCCESS"
    
    log SUCCESS "Migration completed successfully!"
    log INFO "Review the migration report and logs before switching traffic"
    
    if [ "$DRY_RUN" = false ]; then
        log INFO ""
        log INFO "Next steps:"
        log INFO "  1. Review logs: $LOG_FILE"
        log INFO "  2. Test the new deployment: http://${TARGET_HOST}:8080/health"
        log INFO "  3. Update DNS/load balancer to point to: $TARGET_HOST"
        log INFO "  4. Monitor the system for 24-48 hours"
        log INFO "  5. Decommission old infrastructure only after verification"
    fi
}

# Run main function
main "$@"
