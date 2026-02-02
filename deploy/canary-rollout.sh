#!/bin/bash

################################################################################
# Canary Rollout Script
# 
# Automates gradual canary rollout with health checks, metrics monitoring,
# and automatic rollback on failure.
#
# Usage: ./canary-rollout.sh [OPTIONS]
#   --dry-run           Show what would happen without making changes
#   --no-confirm        Skip confirmation prompts
#   --help              Show this help message
#
################################################################################

set -euo pipefail

# ============================================================================
# CONFIGURATION
# ============================================================================

# Canary rollout percentages (must be in ascending order)
readonly CANARY_PERCENTAGES=(10 25 50 100)

# Health check configuration
readonly HEALTH_CHECK_ENDPOINT="${HEALTH_CHECK_ENDPOINT:-http://localhost:8080/health}"
readonly HEALTH_CHECK_TIMEOUT=10  # seconds
readonly HEALTH_CHECK_RETRIES=3

# Metrics and error rate configuration
readonly ERROR_RATE_THRESHOLD=5.0  # percentage
readonly ERROR_RATE_QUERY_ENDPOINT="${ERROR_RATE_QUERY_ENDPOINT:-http://localhost:9090/api/v1/query}"
readonly METRICS_RETENTION_MINUTES=5

# Wait time between rollout stages
readonly WAIT_TIME_BETWEEN_STAGES=300  # seconds (5 minutes)

# Nginx configuration
readonly NGINX_CONFIG_DIR="/etc/nginx/conf.d"
readonly NGINX_CANARY_CONFIG="${NGINX_CONFIG_DIR}/canary-weights.conf"

# Logging configuration
readonly LOG_DIR="./logs"
readonly LOG_FILE="${LOG_DIR}/canary-rollout-$(date +%Y%m%d-%H%M%S).log"

# Script state
DRY_RUN=false
SKIP_CONFIRM=false
CURRENT_CANARY_VERSION=""
CURRENT_STABLE_VERSION=""
START_TIME=$(date +%s)

# ============================================================================
# LOGGING FUNCTIONS
# ============================================================================

log() {
    local level="$1"
    shift
    local message="$@"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${timestamp}] [${level}] ${message}" | tee -a "${LOG_FILE}"
}

log_info() {
    log "INFO" "$@"
}

log_warn() {
    log "WARN" "$@"
}

log_error() {
    log "ERROR" "$@"
}

log_success() {
    log "SUCCESS" "$@"
}

# ============================================================================
# SETUP FUNCTIONS
# ============================================================================

setup_logging() {
    mkdir -p "${LOG_DIR}"
    touch "${LOG_FILE}"
    log_info "Canary rollout started"
    log_info "Log file: ${LOG_FILE}"
}

cleanup() {
    local exit_code=$?
    if [ ${exit_code} -ne 0 ]; then
        log_error "Script exited with code ${exit_code}"
    fi
    exit ${exit_code}
}

trap cleanup EXIT

print_usage() {
    cat << EOF
Canary Rollout Script

Usage: $0 [OPTIONS]

Options:
  --dry-run           Show what would happen without making changes
  --no-confirm        Skip confirmation prompts
  --help              Show this help message

Configuration (via environment variables):
  HEALTH_CHECK_ENDPOINT      Health check URL (default: http://localhost:8080/health)
  ERROR_RATE_QUERY_ENDPOINT  Metrics query endpoint (default: http://localhost:9090/api/v1/query)

Examples:
  # Dry run to see what would happen
  $0 --dry-run

  # Run with custom health check endpoint
  HEALTH_CHECK_ENDPOINT=http://api.example.com/health $0

  # Run without confirmation prompts
  $0 --no-confirm
EOF
}

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --dry-run)
                DRY_RUN=true
                log_info "DRY RUN MODE ENABLED - no changes will be made"
                shift
                ;;
            --no-confirm)
                SKIP_CONFIRM=true
                log_info "Confirmation prompts disabled"
                shift
                ;;
            --help)
                print_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                print_usage
                exit 1
                ;;
        esac
    done
}

# ============================================================================
# HEALTH CHECK FUNCTIONS
# ============================================================================

check_health() {
    local endpoint="$1"
    local attempt=1

    while [ ${attempt} -le ${HEALTH_CHECK_RETRIES} ]; do
        log_info "Health check attempt ${attempt}/${HEALTH_CHECK_RETRIES}: ${endpoint}"

        if ${DRY_RUN}; then
            log_info "[DRY RUN] Would check: ${endpoint}"
            return 0
        fi

        local response=$(curl -s -w "\n%{http_code}" \
            --max-time "${HEALTH_CHECK_TIMEOUT}" \
            "${endpoint}" 2>/dev/null || echo "error")
        
        local http_code=$(echo "${response}" | tail -n1)
        local body=$(echo "${response}" | head -n-1)

        if [ "${http_code}" = "200" ]; then
            log_success "Health check passed (HTTP ${http_code})"
            return 0
        fi

        log_warn "Health check failed (HTTP ${http_code}), attempt ${attempt}/${HEALTH_CHECK_RETRIES}"
        
        if [ ${attempt} -lt ${HEALTH_CHECK_RETRIES} ]; then
            sleep 5
        fi

        ((attempt++))
    done

    log_error "Health check failed after ${HEALTH_CHECK_RETRIES} attempts"
    return 1
}

# ============================================================================
# METRICS FUNCTIONS
# ============================================================================

get_error_rate() {
    local window_minutes=${METRICS_RETENTION_MINUTES}
    local query="rate(http_requests_total{status=~'5..'}[${window_minutes}m]) / rate(http_requests_total[${window_minutes}m]) * 100"
    local error_rate

    if ${DRY_RUN}; then
        error_rate="0.5"
        echo "${error_rate}"
        return 0
    fi

    log_info "Querying error rate (window: ${window_minutes}m)"

    local response=$(curl -s \
        --max-time 10 \
        "${ERROR_RATE_QUERY_ENDPOINT}?query=$(echo -n "${query}" | jq -sRr @uri)" \
        2>/dev/null || echo "")

    if [ -z "${response}" ]; then
        log_warn "Could not query metrics, assuming error rate is acceptable"
        echo "0"
        return 0
    fi

    error_rate=$(echo "${response}" | jq -r '.data.result[0].value[1]' 2>/dev/null || echo "0")

    if [ -z "${error_rate}" ] || [ "${error_rate}" = "null" ]; then
        log_warn "No metrics available, assuming error rate is acceptable"
        echo "0"
        return 0
    fi

    log_info "Current error rate: ${error_rate}%"
    echo "${error_rate}"
}

wait_for_metrics() {
    local percentage=$1
    local wait_time=${WAIT_TIME_BETWEEN_STAGES}

    log_info "Collecting metrics for canary at ${percentage}% (waiting ${wait_time}s)"

    if ${DRY_RUN}; then
        log_info "[DRY RUN] Would wait ${wait_time} seconds"
        return 0
    fi

    sleep "${wait_time}"
    log_info "Metrics collection complete"
}

check_error_threshold() {
    local current_error_rate=$1
    local threshold=${ERROR_RATE_THRESHOLD}

    if (( $(echo "${current_error_rate} > ${threshold}" | bc -l 2>/dev/null || echo "0") )); then
        log_error "Error rate (${current_error_rate}%) exceeds threshold (${threshold}%)"
        return 1
    fi

    log_success "Error rate (${current_error_rate}%) is within acceptable range (< ${threshold}%)"
    return 0
}

# ============================================================================
# TRAFFIC SPLIT FUNCTIONS
# ============================================================================

get_current_weights() {
    if [ ! -f "${NGINX_CANARY_CONFIG}" ]; then
        log_warn "Nginx config not found, initializing with stable=100, canary=0"
        echo "stable=100 canary=0"
        return 0
    fi

    if ${DRY_RUN}; then
        log_info "[DRY RUN] Would read current weights from ${NGINX_CANARY_CONFIG}"
        echo "stable=100 canary=0"
        return 0
    fi

    grep -E "(stable|canary)_weight" "${NGINX_CANARY_CONFIG}" | \
        sed 's/.*\([a-z]*\)_weight \([0-9]*\).*/\1=\2/' | \
        paste -sd ' ' - || echo "stable=100 canary=0"
}

update_traffic_split() {
    local canary_percentage=$1

    if [ ! -d "${NGINX_CONFIG_DIR}" ]; then
        log_warn "Nginx config directory not found: ${NGINX_CONFIG_DIR}"
        log_info "Creating mock config directory for demonstration"
        mkdir -p "${NGINX_CONFIG_DIR}"
    fi

    local stable_percentage=$((100 - canary_percentage))

    log_info "Updating traffic split: stable=${stable_percentage}%, canary=${canary_percentage}%"

    if ${DRY_RUN}; then
        log_info "[DRY RUN] Would update ${NGINX_CANARY_CONFIG}:"
        log_info "  set \$stable_weight ${stable_percentage};"
        log_info "  set \$canary_weight ${canary_percentage};"
        return 0
    fi

    # Create or update nginx canary weights configuration
    cat > "${NGINX_CANARY_CONFIG}" << EOF
# Auto-generated by canary-rollout.sh
# Generated at $(date)
#
# This configuration controls traffic split between stable and canary versions

# Stable version weight (0-100)
set \$stable_weight ${stable_percentage};

# Canary version weight (0-100)
set \$canary_weight ${canary_percentage};

# Verify weights sum to 100
# stable_weight + canary_weight must equal 100
EOF

    log_success "Traffic split updated in ${NGINX_CANARY_CONFIG}"

    # Reload nginx
    if command -v nginx &> /dev/null; then
        log_info "Reloading nginx configuration"
        sudo nginx -s reload 2>/dev/null || log_warn "Could not reload nginx (may require elevated privileges)"
    else
        log_warn "Nginx not found in PATH, skipping reload"
    fi
}

# ============================================================================
# ROLLBACK FUNCTIONS
# ============================================================================

rollback() {
    local reason="${1:-Unknown reason}"
    
    log_error "INITIATING EMERGENCY ROLLBACK: ${reason}"
    
    if ${DRY_RUN}; then
        log_info "[DRY RUN] Would perform rollback"
        return 1
    fi

    log_info "Rolling back to 100% stable traffic"
    update_traffic_split 0

    log_warn "Canary deployment rolled back. Manual investigation required."
    return 1
}

# ============================================================================
# CONFIRMATION FUNCTIONS
# ============================================================================

confirm() {
    local prompt="$1"
    
    if ${SKIP_CONFIRM}; then
        log_info "${prompt} (auto-confirmed)"
        return 0
    fi

    read -p "${prompt} (yes/no): " -r
    if [[ ${REPLY} =~ ^[Yy]$ ]]; then
        return 0
    else
        return 1
    fi
}

# ============================================================================
# MAIN ROLLOUT LOGIC
# ============================================================================

validate_prerequisites() {
    log_info "Validating prerequisites..."

    if ! command -v curl &> /dev/null; then
        log_error "curl is required but not installed"
        return 1
    fi

    if ! command -v jq &> /dev/null; then
        log_warn "jq is not installed, metrics parsing may fail"
    fi

    if ! command -v bc &> /dev/null; then
        log_warn "bc is not installed, numeric comparisons may fail"
    fi

    log_success "Prerequisites check complete"
    return 0
}

display_configuration() {
    log_info "=== Canary Rollout Configuration ==="
    log_info "Canary percentages: ${CANARY_PERCENTAGES[*]}"
    log_info "Health check endpoint: ${HEALTH_CHECK_ENDPOINT}"
    log_info "Error rate threshold: ${ERROR_RATE_THRESHOLD}%"
    log_info "Wait time between stages: ${WAIT_TIME_BETWEEN_STAGES}s"
    log_info "Nginx config directory: ${NGINX_CONFIG_DIR}"
    log_info "Dry run mode: ${DRY_RUN}"
    log_info "Skip confirmations: ${SKIP_CONFIRM}"
    log_info "===================================="
}

rollout_stage() {
    local percentage=$1
    local stage_num=$2
    local total_stages=$3

    log_info "=========================================="
    log_info "Stage ${stage_num}/${total_stages}: Canary at ${percentage}%"
    log_info "=========================================="

    # Confirm before proceeding
    if ! confirm "Proceed with stage ${stage_num} (${percentage}% canary)?"; then
        log_error "Rollout cancelled by user at stage ${stage_num}"
        return 1
    fi

    # Update traffic split
    if ! update_traffic_split "${percentage}"; then
        return 1
    fi

    # Wait for metrics to be collected
    if ! wait_for_metrics "${percentage}"; then
        return 1
    fi

    # Check health
    log_info "Performing health checks..."
    if ! check_health "${HEALTH_CHECK_ENDPOINT}"; then
        return $(rollback "Health check failed at ${percentage}%")
    fi

    # Check error rate
    log_info "Checking error rate..."
    if ${DRY_RUN}; then
        log_info "[DRY RUN] Would query metrics for error rate"
    fi
    local error_rate=$(get_error_rate)
    
    if ! check_error_threshold "${error_rate}"; then
        return $(rollback "Error rate threshold exceeded at ${percentage}%")
    fi

    log_success "Stage ${stage_num} completed successfully"
    return 0
}

execute_rollout() {
    local total_stages=${#CANARY_PERCENTAGES[@]}
    local stage_num=0

    for percentage in "${CANARY_PERCENTAGES[@]}"; do
        ((stage_num++))

        if ! rollout_stage "${percentage}" "${stage_num}" "${total_stages}"; then
            log_error "Rollout failed at stage ${stage_num}"
            return 1
        fi

        # Don't wait after final stage
        if [ "${percentage}" != "100" ]; then
            log_info "Stage ${stage_num} complete. Proceeding to next stage..."
        fi
    done

    return 0
}

generate_summary() {
    local end_time=$(date +%s)
    local duration=$((end_time - START_TIME))
    local minutes=$((duration / 60))
    local seconds=$((duration % 60))

    log_info "=========================================="
    log_success "CANARY ROLLOUT COMPLETED SUCCESSFULLY"
    log_info "=========================================="
    log_info "Duration: ${minutes}m ${seconds}s"
    log_info "Final canary percentage: 100%"
    log_info "Log file: ${LOG_FILE}"
    log_info "=========================================="
}

# ============================================================================
# MAIN ENTRY POINT
# ============================================================================

main() {
    parse_arguments "$@"
    setup_logging
    display_configuration

    if ! validate_prerequisites; then
        log_error "Prerequisites validation failed"
        return 1
    fi

    if ! execute_rollout; then
        log_error "Canary rollout failed"
        return 1
    fi

    generate_summary
    return 0
}

# Run main function
main "$@"
exit $?
