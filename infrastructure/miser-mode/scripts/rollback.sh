#!/bin/bash
# ============================================================================
# Ghost Platform - Migration Rollback Script
# ============================================================================
#
# Quickly rollback a failed migration and restore source system
#
# Usage:
#   ./rollback.sh [OPTIONS]
#
# Options:
#   --reason TEXT          Reason for rollback (for logging)
#   --export-dir DIR       Path to export backup directory
#   --source-host HOST     Source system to restore
#   --force                Skip confirmation prompts
#   --help                 Show this help message
#
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATION_DIR="$(dirname "$SCRIPT_DIR")"
DOCKER_DIR="${MIGRATION_DIR}/docker"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${MIGRATION_DIR}/logs/rollback_${TIMESTAMP}.log"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
REASON=""
EXPORT_DIR=""
SOURCE_HOST=""
FORCE=false

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
    exit 1
}

confirm() {
    if [ "$FORCE" = false ]; then
        read -p "$(echo -e "${YELLOW}$1 (yes/no): ${NC}")" response
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

print_banner() {
    echo ""
    echo -e "${RED}╔═══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${RED}║              MIGRATION ROLLBACK PROCEDURE                     ║${NC}"
    echo -e "${RED}║                                                               ║${NC}"
    echo -e "${RED}║  WARNING: This will stop the target system and attempt to    ║${NC}"
    echo -e "${RED}║  restore operation to the source system.                     ║${NC}"
    echo -e "${RED}╚═══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

# ============================================================================
# Rollback Steps
# ============================================================================

stop_target_system() {
    log INFO "Step 1: Stopping target system..."
    
    cd "$DOCKER_DIR"
    
    if [ -f docker-compose.yml ]; then
        log INFO "Stopping all Docker containers..."
        docker-compose down 2>&1 | tee -a "$LOG_FILE"
        
        # Verify all stopped
        local running_containers=$(docker ps -a --filter "name=ghost-*" --format "{{.Names}}" | wc -l)
        
        if [ "$running_containers" -eq 0 ]; then
            log SUCCESS "Target system stopped successfully"
        else
            log WARNING "Some containers still running: $running_containers"
        fi
    else
        log WARNING "docker-compose.yml not found, skipping container shutdown"
    fi
}

verify_source_system() {
    log INFO "Step 2: Verifying source system status..."
    
    if [ -z "$SOURCE_HOST" ]; then
        log WARNING "Source host not specified, skipping verification"
        return
    fi
    
    log INFO "Checking connectivity to source: $SOURCE_HOST"
    
    if ! ssh -o ConnectTimeout=5 "$SOURCE_HOST" "echo 'Connected'" >/dev/null 2>&1; then
        error_exit "Cannot connect to source host: $SOURCE_HOST"
    fi
    
    log SUCCESS "Source host is reachable"
    
    # Check if Docker is running
    log INFO "Checking Docker status on source..."
    
    if ssh "$SOURCE_HOST" "docker info" >/dev/null 2>&1; then
        log SUCCESS "Docker is running on source"
    else
        error_exit "Docker is not running on source host"
    fi
    
    # Check Ghost containers
    local ghost_containers=$(ssh "$SOURCE_HOST" "docker ps --filter 'name=ghost-*' --format '{{.Names}}'" | wc -l)
    
    if [ "$ghost_containers" -gt 0 ]; then
        log SUCCESS "Found $ghost_containers Ghost containers on source"
    else
        log WARNING "No Ghost containers found on source - may need to start them"
    fi
}

restart_source_system() {
    log INFO "Step 3: Ensuring source system is running..."
    
    if [ -z "$SOURCE_HOST" ]; then
        log WARNING "Source host not specified, skipping restart"
        return
    fi
    
    log INFO "Starting services on source system..."
    
    ssh "$SOURCE_HOST" << 'EOF'
        cd /opt/ghost 2>/dev/null || cd ~ || cd /
        
        if [ -f docker-compose.yml ]; then
            echo "Found docker-compose.yml, starting services..."
            docker-compose up -d
            sleep 10
            docker-compose ps
        else
            echo "WARNING: docker-compose.yml not found"
            echo "Attempting to start Ghost containers..."
            docker ps -a --filter "name=ghost-*" --format "{{.Names}}" | xargs -r docker start
        fi
EOF
    
    log SUCCESS "Source system restart initiated"
    
    # Wait and verify
    log INFO "Waiting for source system to be ready..."
    sleep 15
    
    if ssh "$SOURCE_HOST" "curl -sf http://localhost:8080/health" >/dev/null 2>&1; then
        log SUCCESS "Source system health check passed"
    else
        log WARNING "Source system health check failed - manual verification needed"
    fi
}

revert_dns() {
    log INFO "Step 4: DNS reversion instructions..."
    
    echo ""
    echo -e "${YELLOW}╔═══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${YELLOW}║                    DNS REVERSION REQUIRED                     ║${NC}"
    echo -e "${YELLOW}╚═══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo "You must manually revert DNS records to point back to the source system."
    echo ""
    
    if [ -n "$SOURCE_HOST" ]; then
        echo "Source Host: $SOURCE_HOST"
        
        # Try to get IP
        local source_ip=$(ssh "$SOURCE_HOST" "curl -s ifconfig.me" 2>/dev/null || echo "unknown")
        echo "Source IP: $source_ip"
    fi
    
    echo ""
    echo "Actions required:"
    echo "  1. Log into your DNS provider"
    echo "  2. Update A/AAAA records to point to source IP"
    echo "  3. If using load balancer, update backend pool"
    echo "  4. Wait for DNS propagation (60-300 seconds)"
    echo "  5. Verify with: nslookup your-domain.com"
    echo ""
    
    if ! confirm "Have you reverted DNS records?"; then
        log WARNING "DNS not yet reverted - remember to do this manually"
    else
        log INFO "DNS reversion confirmed"
    fi
}

create_rollback_report() {
    log INFO "Step 5: Creating rollback report..."
    
    local report_file="${MIGRATION_DIR}/logs/rollback_report_${TIMESTAMP}.txt"
    
    cat > "$report_file" <<EOF
╔═══════════════════════════════════════════════════════════════╗
║              Ghost Platform Rollback Report                   ║
╚═══════════════════════════════════════════════════════════════╝

Rollback Date: $(date)
Reason: ${REASON:-"Not specified"}

Source Host: ${SOURCE_HOST:-"Not specified"}
Export Backup: ${EXPORT_DIR:-"Not available"}

Actions Taken:
  ✓ Stopped target system
  ✓ Verified source system availability
  ✓ Attempted to restart source system
  ✓ Provided DNS reversion instructions

Current State:
  - Target system: STOPPED
  - Source system: ACTIVE (verify manually)
  - DNS: MANUAL REVERSION REQUIRED

Next Steps:
  1. Verify source system is fully operational
  2. Confirm DNS has been reverted
  3. Test source system from external location
  4. Monitor source system for stability
  5. Investigate cause of rollback
  6. Plan remediation for next migration attempt

Investigation:
  - Review target logs: ${DOCKER_DIR}/logs/
  - Review migration logs: ${MIGRATION_DIR}/logs/migration_*.log
  - Review validation results: ${MIGRATION_DIR}/logs/validation_*.log

Rollback Log: $LOG_FILE

EOF
    
    log SUCCESS "Rollback report created: $report_file"
    cat "$report_file"
}

backup_target_data() {
    log INFO "Backing up target system data (if any)..."
    
    local backup_dir="${MIGRATION_DIR}/backups/rollback_${TIMESTAMP}"
    mkdir -p "$backup_dir"
    
    # Check if there's data to backup
    if docker volume ls --filter "name=ghost" | grep -q ghost; then
        log INFO "Found Docker volumes, creating backup..."
        
        # PostgreSQL
        if docker ps -a --filter "name=ghost-postgres" --format "{{.Names}}" | grep -q postgres; then
            log INFO "Backing up PostgreSQL data..."
            docker exec ghost-postgres pg_dump -U ghost ghost 2>/dev/null > "${backup_dir}/postgres_rollback.sql" || true
        fi
        
        # Redis
        if docker ps -a --filter "name=ghost-redis" --format "{{.Names}}" | grep -q redis; then
            log INFO "Backing up Redis data..."
            docker exec ghost-redis redis-cli SAVE >/dev/null 2>&1 || true
            docker cp ghost-redis:/data/dump.rdb "${backup_dir}/redis_rollback.rdb" 2>/dev/null || true
        fi
        
        log SUCCESS "Target data backed up to: $backup_dir"
    else
        log INFO "No Docker volumes found, skipping backup"
    fi
}

# ============================================================================
# Main Function
# ============================================================================

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --reason)
                REASON="$2"
                shift 2
                ;;
            --export-dir)
                EXPORT_DIR="$2"
                shift 2
                ;;
            --source-host)
                SOURCE_HOST="$2"
                shift 2
                ;;
            --force)
                FORCE=true
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
    mkdir -p "$(dirname "$LOG_FILE")"
    
    parse_arguments "$@"
    
    print_banner
    
    log WARNING "Initiating rollback procedure..."
    
    if [ -n "$REASON" ]; then
        log INFO "Rollback reason: $REASON"
    fi
    
    if ! confirm "Are you sure you want to proceed with rollback?"; then
        log INFO "Rollback cancelled by user"
        exit 0
    fi
    
    # Execute rollback steps
    backup_target_data
    stop_target_system
    verify_source_system
    restart_source_system
    revert_dns
    create_rollback_report
    
    echo ""
    log SUCCESS "Rollback procedure completed"
    echo ""
    log WARNING "IMPORTANT: Verify the following manually:"
    echo "  1. Source system is responding to requests"
    echo "  2. DNS has been properly reverted"
    echo "  3. Users can access the source system"
    echo "  4. All critical functionality works on source"
    echo ""
    log INFO "Review the rollback report for details and next steps"
}

main "$@"
