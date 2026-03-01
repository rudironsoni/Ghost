#!/bin/bash

################################################################################
# Emergency Rollback Script
#
# Performs emergency rollback from canary to stable version
# Usage: ./rollback.sh [--force]
#
# Exit Codes:
#   0 - Successful rollback
#   1 - Rollback failed
#   2 - Invalid arguments
################################################################################

set -o pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_FILE="${PROJECT_ROOT}/logs/rollback_$(date +%Y%m%d_%H%M%S).log"
BACKUP_DIR="${PROJECT_ROOT}/.rollback_backup"
DOCKER_COMPOSE_CANARY="${SCRIPT_DIR}/docker-compose.canary.yml"
NGINX_CONF="${SCRIPT_DIR}/nginx-canary.conf"
STABLE_SERVICE="app-stable"
CANARY_SERVICE="app-canary"
NGINX_SERVICE="nginx-canary"
HEALTH_CHECK_RETRIES=5
HEALTH_CHECK_INTERVAL=3
CONNECTION_DRAIN_TIMEOUT=30
FORCE_MODE=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

################################################################################
# Logging Functions
################################################################################

log() {
    local level="$1"
    shift
    local message="$@"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')

    # Ensure log directory exists
    mkdir -p "$(dirname "$LOG_FILE")"

    echo "[${timestamp}] [${level}] ${message}" >> "$LOG_FILE"

    case "$level" in
        INFO)
            echo -e "${BLUE}[INFO]${NC} ${message}"
            ;;
        SUCCESS)
            echo -e "${GREEN}[SUCCESS]${NC} ${message}"
            ;;
        WARN)
            echo -e "${YELLOW}[WARN]${NC} ${message}"
            ;;
        ERROR)
            echo -e "${RED}[ERROR]${NC} ${message}" >&2
            ;;
    esac
}

################################################################################
# Utility Functions
################################################################################

confirm() {
    local prompt="$1"
    local response

    if [[ "$FORCE_MODE" == true ]]; then
        log INFO "Force mode enabled, skipping confirmation"
        return 0
    fi

    read -p "$(echo -e ${YELLOW}${prompt}${NC})" -n 1 -r response
    echo
    [[ "$response" =~ ^[Yy]$ ]]
}

check_prerequisites() {
    log INFO "Checking prerequisites..."

    local missing_tools=()

    # Check required commands
    for cmd in docker docker-compose curl jq; do
        if ! command -v "$cmd" &> /dev/null; then
            missing_tools+=("$cmd")
        fi
    done

    if [[ ${#missing_tools[@]} -gt 0 ]]; then
        log ERROR "Missing required tools: ${missing_tools[*]}"
        return 1
    fi

    # Check docker daemon
    if ! docker info &> /dev/null; then
        log ERROR "Docker daemon is not running"
        return 1
    fi

    # Check docker-compose files
    if [[ ! -f "$DOCKER_COMPOSE_CANARY" ]]; then
        log ERROR "Docker compose file not found: $DOCKER_COMPOSE_CANARY"
        return 1
    fi

    if [[ ! -f "$NGINX_CONF" ]]; then
        log ERROR "Nginx config not found: $NGINX_CONF"
        return 1
    fi

    log SUCCESS "Prerequisites check passed"
    return 0
}

create_backup() {
    log INFO "Creating backup of current state..."

    mkdir -p "$BACKUP_DIR"

    # Backup current nginx configuration
    if docker exec "$NGINX_SERVICE" cat /etc/nginx/conf.d/default.conf &> /dev/null; then
        docker exec "$NGINX_SERVICE" cat /etc/nginx/conf.d/default.conf \
            > "${BACKUP_DIR}/nginx_backup_$(date +%s).conf" || {
            log WARN "Failed to backup nginx config, continuing..."
        }
    fi

    # Backup docker-compose state
    docker-compose -f "$DOCKER_COMPOSE_CANARY" ps > "${BACKUP_DIR}/docker_state_$(date +%s).txt" 2>&1 || {
        log WARN "Failed to backup docker state, continuing..."
    }

    log SUCCESS "Backup created at: $BACKUP_DIR"
}

################################################################################
# Core Rollback Functions
################################################################################

drain_connections() {
    log INFO "Draining connections from canary service (${CONNECTION_DRAIN_TIMEOUT}s timeout)..."

    local start_time=$(date +%s)
    local end_time=$((start_time + CONNECTION_DRAIN_TIMEOUT))

    # Update nginx to route 0% traffic to canary (100% to stable)
    log INFO "Stopping traffic to canary in nginx..."

    # Create temporary nginx config with 0% canary traffic
    cat > "${BACKUP_DIR}/nginx_drain.conf" << 'EOF'
upstream stable {
    server app-stable:3000;
}

upstream canary {
    server app-canary:3000;
}

server {
    listen 80;
    server_name _;

    location / {
        # 100% traffic to stable during drain
        proxy_pass http://stable;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Connection settings for graceful drain
        proxy_connect_timeout 10s;
        proxy_send_timeout 30s;
        proxy_read_timeout 30s;

        # Keep connections alive for existing requests
        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
EOF

    docker cp "${BACKUP_DIR}/nginx_drain.conf" "${NGINX_SERVICE}:/etc/nginx/conf.d/default.conf" 2>&1 | \
        tee -a "$LOG_FILE" || {
        log WARN "Failed to update nginx config, continuing with connection drain wait..."
    }

    # Reload nginx if possible
    docker exec "$NGINX_SERVICE" nginx -s reload 2>&1 | tee -a "$LOG_FILE" || {
        log WARN "Failed to reload nginx gracefully, continuing..."
    }

    # Wait for connections to drain
    while [[ $(date +%s) -lt $end_time ]]; do
        local remaining=$((end_time - $(date +%s)))
        log INFO "Waiting for connections to drain... (${remaining}s remaining)"
        sleep 5
    done

    log SUCCESS "Connection drain completed"
}

stop_canary() {
    log INFO "Stopping canary service..."

    cd "$PROJECT_ROOT" || {
        log ERROR "Failed to change to project directory"
        return 1
    }

    # Stop canary container
    if docker-compose -f "$DOCKER_COMPOSE_CANARY" ps "$CANARY_SERVICE" | grep -q "Up"; then
        docker-compose -f "$DOCKER_COMPOSE_CANARY" stop "$CANARY_SERVICE" 2>&1 | tee -a "$LOG_FILE" || {
            log WARN "Stop command failed, attempting force stop..."
            docker-compose -f "$DOCKER_COMPOSE_CANARY" kill "$CANARY_SERVICE" 2>&1 | tee -a "$LOG_FILE"
        }
    else
        log WARN "Canary service already stopped"
    fi

    log SUCCESS "Canary service stopped"
    return 0
}

verify_stable() {
    log INFO "Verifying stable service health..."

    local retries=$HEALTH_CHECK_RETRIES
    local interval=$HEALTH_CHECK_INTERVAL

    while [[ $retries -gt 0 ]]; do
        # Check if stable container is running
        if ! docker-compose -f "$DOCKER_COMPOSE_CANARY" ps "$STABLE_SERVICE" | grep -q "Up"; then
            log WARN "Stable service is not running, attempting to start..."
            docker-compose -f "$DOCKER_COMPOSE_CANARY" up -d "$STABLE_SERVICE" 2>&1 | tee -a "$LOG_FILE" || {
                log ERROR "Failed to start stable service"
                ((retries--))
                if [[ $retries -gt 0 ]]; then
                    sleep "$interval"
                    continue
                fi
                return 1
            }
        fi

        # Perform health check
        if docker exec "$STABLE_SERVICE" curl -sf http://localhost:3000/health &> /dev/null; then
            log SUCCESS "Stable service health check passed"
            return 0
        fi

        log WARN "Health check failed, retrying... ($retries attempts remaining)"
        ((retries--))

        if [[ $retries -gt 0 ]]; then
            sleep "$interval"
        fi
    done

    log ERROR "Stable service health check failed after $HEALTH_CHECK_RETRIES attempts"
    return 1
}

update_nginx() {
    log INFO "Updating nginx configuration to route 100% traffic to stable..."

    # Create stable-only nginx config
    cat > "${BACKUP_DIR}/nginx_stable.conf" << 'EOF'
upstream stable {
    server app-stable:3000;
}

server {
    listen 80;
    server_name _;

    location / {
        proxy_pass http://stable;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        proxy_connect_timeout 10s;
        proxy_send_timeout 30s;
        proxy_read_timeout 30s;

        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
EOF

    # Copy and reload nginx
    docker cp "${BACKUP_DIR}/nginx_stable.conf" "${NGINX_SERVICE}:/etc/nginx/conf.d/default.conf" 2>&1 | \
        tee -a "$LOG_FILE" || {
        log ERROR "Failed to copy nginx config"
        return 1
    }

    docker exec "$NGINX_SERVICE" nginx -s reload 2>&1 | tee -a "$LOG_FILE" || {
        log ERROR "Failed to reload nginx"
        return 1
    }

    log SUCCESS "Nginx updated to route 100% traffic to stable"
    return 0
}

cleanup() {
    log INFO "Cleaning up canary resources..."

    cd "$PROJECT_ROOT" || {
        log ERROR "Failed to change to project directory"
        return 1
    }

    # Remove canary container
    docker-compose -f "$DOCKER_COMPOSE_CANARY" rm -f "$CANARY_SERVICE" 2>&1 | tee -a "$LOG_FILE" || {
        log WARN "Failed to remove canary container, continuing cleanup..."
    }

    # Prune unused networks and volumes (optional)
    docker network prune -f 2>&1 | tee -a "$LOG_FILE" || true

    log SUCCESS "Cleanup completed"
    return 0
}

verify_rollback() {
    log INFO "Verifying rollback success..."

    local retries=3

    while [[ $retries -gt 0 ]]; do
        # Check nginx is responding
        if docker exec "$NGINX_SERVICE" curl -sf http://localhost/health &> /dev/null 2>&1 || \
           docker exec "$NGINX_SERVICE" curl -sf http://localhost/ &> /dev/null 2>&1; then
            log SUCCESS "Nginx is responding with stable service"
            return 0
        fi

        log WARN "Verification check failed, retrying... ($retries attempts remaining)"
        ((retries--))
        sleep 3
    done

    log ERROR "Rollback verification failed"
    return 1
}

################################################################################
# Display Functions
################################################################################

display_warning() {
    cat << 'EOF'

╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║                     ⚠️  EMERGENCY ROLLBACK WARNING  ⚠️                     ║
║                                                                            ║
║  This operation will:                                                     ║
║  • Stop the canary (new) service                                          ║
║  • Drain active connections gracefully                                    ║
║  • Route 100% of traffic back to stable (previous) service                ║
║  • Remove canary containers                                               ║
║                                                                            ║
║  This action is IRREVERSIBLE. The canary deployment will be terminated.  ║
║                                                                            ║
║  Use --force flag to skip confirmation prompts (automated rollback)       ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝

EOF
}

display_success() {
    cat << 'EOF'

╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║                        ✓ ROLLBACK SUCCESSFUL ✓                           ║
║                                                                            ║
║  The canary deployment has been rolled back to stable.                    ║
║  All traffic is now routed to the stable service.                         ║
║                                                                            ║
║  Next steps:                                                              ║
║  • Monitor stable service performance                                     ║
║  • Investigate canary failure in deployment logs                          ║
║  • Address issues before attempting next deployment                       ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝

EOF
}

display_failure() {
    cat << 'EOF'

╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║                        ✗ ROLLBACK FAILED ✗                              ║
║                                                                            ║
║  The rollback operation did not complete successfully.                    ║
║                                                                            ║
║  CRITICAL: Manual intervention may be required!                          ║
║                                                                            ║
║  Actions to take:                                                         ║
║  1. Check logs: tail -f logs/rollback_*.log                               ║
║  2. Verify service status: docker-compose ps                              ║
║  3. Check nginx config: docker exec nginx-canary cat /etc/nginx/conf.d/*  ║
║  4. Contact DevOps team immediately                                       ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝

EOF
}

################################################################################
# Argument Parsing
################################################################################

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --force)
                FORCE_MODE=true
                log INFO "Force mode enabled"
                shift
                ;;
            --help|-h)
                display_help
                exit 0
                ;;
            *)
                log ERROR "Unknown argument: $1"
                display_help
                exit 2
                ;;
        esac
    done
}

display_help() {
    cat << 'EOF'
Emergency Rollback Script

Usage: ./rollback.sh [OPTIONS]

Options:
    --force     Skip confirmation prompts (automated rollback)
    --help      Display this help message

Examples:
    # Interactive rollback with confirmation
    ./rollback.sh

    # Automated rollback (CI/CD)
    ./rollback.sh --force

Environment:
    LOG_FILE    Location of rollback logs (default: logs/rollback_*.log)

Exit Codes:
    0           Rollback completed successfully
    1           Rollback failed
    2           Invalid arguments

Safety Features:
    • Connection drain timeout (prevents hanging)
    • Health check verification (ensures stability)
    • State backup (allows manual recovery)
    • Detailed logging (troubleshooting)
    • Graceful error handling (non-destructive)

EOF
}

################################################################################
# Main Execution Flow
################################################################################

main() {
    parse_arguments "$@"

    log INFO "================================"
    log INFO "Emergency Rollback Started"
    log INFO "================================"
    log INFO "Log file: $LOG_FILE"
    log INFO "Project root: $PROJECT_ROOT"

    # Display warning
    display_warning

    # Get confirmation (unless --force)
    if ! confirm "Do you want to proceed with rollback? [y/N] "; then
        log INFO "Rollback cancelled by user"
        exit 0
    fi

    log INFO "Proceeding with rollback..."

    # Pre-flight checks
    if ! check_prerequisites; then
        log ERROR "Prerequisites check failed"
        display_failure
        exit 1
    fi

    # Create backup
    if ! create_backup; then
        log WARN "Backup creation failed, continuing with rollback..."
    fi

    # Execute rollback steps
    if ! drain_connections; then
        log ERROR "Connection drain failed"
        display_failure
        exit 1
    fi

    if ! stop_canary; then
        log ERROR "Failed to stop canary service"
        display_failure
        exit 1
    fi

    if ! verify_stable; then
        log ERROR "Stable service verification failed"
        display_failure
        exit 1
    fi

    if ! update_nginx; then
        log ERROR "Failed to update nginx"
        display_failure
        exit 1
    fi

    if ! cleanup; then
        log ERROR "Cleanup failed"
        display_failure
        exit 1
    fi

    # Verify rollback
    if ! verify_rollback; then
        log ERROR "Rollback verification failed"
        display_failure
        exit 1
    fi

    # Success
    log SUCCESS "Rollback completed successfully"
    display_success
    log INFO "Backup location: $BACKUP_DIR"
    log INFO "Log location: $LOG_FILE"

    exit 0
}

# Trap errors and cleanup
trap 'log ERROR "Script interrupted"; exit 1' INT TERM

# Run main function
main "$@"
