#!/bin/bash
###############################################################################
# Enterprise Ghost CMS Rollback Script
# Description: Production-grade rollback with validation and notification
# Usage: ./rollback.sh [environment] [options]
###############################################################################

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
LOG_FILE="/var/log/ghost-rollback-$(date +%Y%m%d-%H%M%S).log"
ROLLBACK_TIMEOUT="${ROLLBACK_TIMEOUT:-600}"  # 10 minutes

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $(date '+%Y-%m-%d %H:%M:%S') - $*" | tee -a "$LOG_FILE"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $(date '+%Y-%m-%d %H:%M:%S') - $*" | tee -a "$LOG_FILE"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $(date '+%Y-%m-%d %H:%M:%S') - $*" | tee -a "$LOG_FILE"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $(date '+%Y-%m-%d %H:%M:%S') - $*" | tee -a "$LOG_FILE"
}

# Error handler
error_handler() {
    local line_number=$1
    log_error "Rollback script failed at line $line_number"
    log_error "Manual intervention may be required!"
    send_notification "critical" "Rollback script failed - manual intervention required"
    exit 1
}

trap 'error_handler ${LINENO}' ERR

# Usage information
usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Enterprise Ghost CMS Rollback Script

OPTIONS:
    -e, --environment ENV       Target environment (development|staging|production)
    -r, --revision REVISION     Helm revision to rollback to (default: previous)
    -t, --target TARGET         Specific backup timestamp (YYYYMMDD-HHMMSS)
    -d, --dry-run              Perform dry run without actual rollback
    -n, --namespace NAMESPACE   Kubernetes namespace (default: ghost-ENV)
    -f, --force                Force rollback without confirmation
    -h, --help                 Display this help message

EXAMPLES:
    $0 -e production                    # Rollback to previous release
    $0 -e staging -r 5                  # Rollback to revision 5
    $0 -e production -t 20260203-120000 # Rollback to specific backup
    $0 -e production --dry-run          # Preview rollback

ENVIRONMENT VARIABLES:
    KUBECONFIG              Path to kubeconfig file
    ROLLBACK_TIMEOUT        Rollback timeout in seconds (default: 600)
    SLACK_WEBHOOK_URL       Slack webhook for notifications

EOF
    exit 0
}

# Parse arguments
parse_arguments() {
    ENVIRONMENT=""
    REVISION=""
    TARGET_BACKUP=""
    DRY_RUN=false
    NAMESPACE=""
    FORCE=false

    while [[ $# -gt 0 ]]; do
        case $1 in
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -r|--revision)
                REVISION="$2"
                shift 2
                ;;
            -t|--target)
                TARGET_BACKUP="$2"
                shift 2
                ;;
            -d|--dry-run)
                DRY_RUN=true
                shift
                ;;
            -n|--namespace)
                NAMESPACE="$2"
                shift 2
                ;;
            -f|--force)
                FORCE=true
                shift
                ;;
            -h|--help)
                usage
                ;;
            *)
                log_error "Unknown option: $1"
                usage
                ;;
        esac
    done

    # Validate required parameters
    if [[ -z "$ENVIRONMENT" ]]; then
        log_error "Environment is required"
        usage
    fi

    if [[ -z "$NAMESPACE" ]]; then
        NAMESPACE="ghost-${ENVIRONMENT}"
    fi

    # Validate environment
    if [[ ! "$ENVIRONMENT" =~ ^(development|staging|production)$ ]]; then
        log_error "Invalid environment: $ENVIRONMENT"
        exit 1
    fi
}

# Pre-flight checks
preflight_checks() {
    log_info "Running pre-flight checks for rollback..."

    # Check required commands
    local required_commands=("kubectl" "helm" "jq" "aws")
    for cmd in "${required_commands[@]}"; do
        if ! command -v "$cmd" &> /dev/null; then
            log_error "Required command not found: $cmd"
            exit 1
        fi
    done

    # Check kubectl connectivity
    if ! kubectl cluster-info &> /dev/null; then
        log_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi

    # Check namespace exists
    if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
        log_error "Namespace $NAMESPACE does not exist"
        exit 1
    fi

    # Check if deployment exists
    if ! helm list -n "$NAMESPACE" | grep -q "ghost"; then
        log_error "No Ghost deployment found in namespace $NAMESPACE"
        exit 1
    fi

    log_success "Pre-flight checks passed"
}

# Get rollback information
get_rollback_info() {
    log_info "Gathering rollback information..."

    # Get current deployment info
    CURRENT_REVISION=$(helm history ghost -n "$NAMESPACE" --max 1 -o json | jq -r '.[0].revision')
    CURRENT_STATUS=$(helm history ghost -n "$NAMESPACE" --max 1 -o json | jq -r '.[0].status')
    CURRENT_VERSION=$(kubectl get deployment -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[0].spec.template.spec.containers[0].image}')

    log_info "Current Status:"
    log_info "  - Revision: $CURRENT_REVISION"
    log_info "  - Status: $CURRENT_STATUS"
    log_info "  - Version: $CURRENT_VERSION"

    # Get Helm history
    log_info "Recent Helm releases:"
    helm history ghost -n "$NAMESPACE" --max 5

    # Determine rollback target
    if [[ -n "$REVISION" ]]; then
        TARGET_REVISION="$REVISION"
        log_info "Rolling back to specified revision: $TARGET_REVISION"
    elif [[ -n "$TARGET_BACKUP" ]]; then
        log_info "Rolling back to backup: $TARGET_BACKUP"
    else
        # Get previous successful deployment
        TARGET_REVISION=$(helm history ghost -n "$NAMESPACE" -o json | \
            jq -r '[.[] | select(.status == "deployed" or .status == "superseded")] | sort_by(.revision) | reverse | .[1].revision // empty')

        if [[ -z "$TARGET_REVISION" ]]; then
            log_error "No previous successful deployment found"
            exit 1
        fi

        log_info "Rolling back to previous revision: $TARGET_REVISION"
    fi
}

# Confirm rollback
confirm_rollback() {
    if [[ "$FORCE" == "true" ]] || [[ "$DRY_RUN" == "true" ]]; then
        return 0
    fi

    log_warning "=========================================="
    log_warning "ROLLBACK CONFIRMATION REQUIRED"
    log_warning "=========================================="
    log_warning "Environment: $ENVIRONMENT"
    log_warning "Current Revision: $CURRENT_REVISION"
    log_warning "Target Revision: ${TARGET_REVISION:-backup}"
    log_warning "=========================================="

    read -p "Are you sure you want to rollback? (yes/no): " -r
    if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
        log_info "Rollback cancelled by user"
        exit 0
    fi
}

# Create pre-rollback snapshot
create_snapshot() {
    log_info "Creating pre-rollback snapshot..."

    local snapshot_dir="${PROJECT_ROOT}/backups/${ENVIRONMENT}/pre-rollback-$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$snapshot_dir"

    # Save current state
    helm get values ghost -n "$NAMESPACE" -o yaml > "${snapshot_dir}/values.yaml"
    helm get manifest ghost -n "$NAMESPACE" > "${snapshot_dir}/manifests.yaml"
    kubectl get all -n "$NAMESPACE" -o yaml > "${snapshot_dir}/resources.yaml"

    # Save pod logs
    for pod in $(kubectl get pods -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[*].metadata.name}'); do
        kubectl logs "$pod" -n "$NAMESPACE" --all-containers=true > "${snapshot_dir}/logs-${pod}.txt" 2>/dev/null || true
    done

    log_success "Snapshot created at: $snapshot_dir"
    echo "$snapshot_dir" > /tmp/ghost-rollback-snapshot
}

# Perform Helm rollback
helm_rollback() {
    log_info "Performing Helm rollback to revision $TARGET_REVISION..."

    local helm_cmd="helm rollback ghost $TARGET_REVISION -n $NAMESPACE --wait --timeout ${ROLLBACK_TIMEOUT}s"

    if [[ "$DRY_RUN" == "true" ]]; then
        helm_cmd="$helm_cmd --dry-run"
    fi

    log_info "Executing: $helm_cmd"

    if eval "$helm_cmd" 2>&1 | tee -a "$LOG_FILE"; then
        log_success "Helm rollback completed"
    else
        log_error "Helm rollback failed"
        return 1
    fi
}

# Perform backup-based rollback
backup_rollback() {
    log_info "Performing backup-based rollback to $TARGET_BACKUP..."

    local backup_dir="${PROJECT_ROOT}/backups/${ENVIRONMENT}/${TARGET_BACKUP}"

    if [[ ! -d "$backup_dir" ]]; then
        log_error "Backup directory not found: $backup_dir"
        exit 1
    fi

    if [[ ! -f "${backup_dir}/values.yaml" ]]; then
        log_error "Backup values file not found"
        exit 1
    fi

    local helm_cmd="helm upgrade --install ghost \
        ${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart \
        --namespace $NAMESPACE \
        --values ${backup_dir}/values.yaml \
        --wait \
        --timeout ${ROLLBACK_TIMEOUT}s \
        --force"

    if [[ "$DRY_RUN" == "true" ]]; then
        helm_cmd="$helm_cmd --dry-run"
    fi

    log_info "Executing backup restore..."

    if eval "$helm_cmd" 2>&1 | tee -a "$LOG_FILE"; then
        log_success "Backup restore completed"
    else
        log_error "Backup restore failed"
        return 1
    fi
}

# Restore database backup
restore_database() {
    log_info "Checking for database backup to restore..."

    local db_backup_path=""

    if [[ -n "$TARGET_BACKUP" ]]; then
        # Find database backup matching timestamp
        if command -v aws &> /dev/null; then
            db_backup_path=$(aws s3 ls "s3://ghost-backups/${ENVIRONMENT}/database/" | \
                grep "$TARGET_BACKUP" | awk '{print $4}' | head -1)

            if [[ -n "$db_backup_path" ]]; then
                log_warning "Database backup found: $db_backup_path"
                read -p "Do you want to restore the database backup? (yes/no): " -r

                if [[ $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
                    log_info "Downloading database backup..."
                    aws s3 cp "s3://ghost-backups/${ENVIRONMENT}/database/${db_backup_path}" "/tmp/db-restore.sql"

                    # Get MySQL credentials
                    local mysql_password=$(kubectl get secret ghost-mysql-secret -n "$NAMESPACE" -o jsonpath='{.data.mysql-password}' | base64 -d)
                    local mysql_host=$(kubectl get service ghost-mysql -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}')

                    log_info "Restoring database..."
                    kubectl run mysql-restore \
                        --image=mysql:8.0 \
                        --rm -i --restart=Never \
                        -n "$NAMESPACE" \
                        --env="MYSQL_PWD=${mysql_password}" \
                        -- mysql -h "${mysql_host}" -u ghost ghost_production < "/tmp/db-restore.sql"

                    rm -f "/tmp/db-restore.sql"
                    log_success "Database restored successfully"
                else
                    log_info "Database restore skipped"
                fi
            fi
        fi
    else
        log_info "No database backup restore required for Helm rollback"
    fi
}

# Verify rollback
verify_rollback() {
    log_info "Verifying rollback..."

    # Wait for pods to be ready
    log_info "Waiting for pods to become ready..."
    if kubectl wait --for=condition=ready pod \
        -l app=ghost \
        -n "$NAMESPACE" \
        --timeout=300s 2>&1 | tee -a "$LOG_FILE"; then
        log_success "All pods are ready"
    else
        log_error "Pods failed to become ready"
        return 1
    fi

    # Check deployment status
    local ready_replicas=$(kubectl get deployment -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[0].status.readyReplicas}')
    local desired_replicas=$(kubectl get deployment -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[0].spec.replicas}')

    if [[ "$ready_replicas" == "$desired_replicas" ]]; then
        log_success "Deployment is healthy: $ready_replicas/$desired_replicas replicas ready"
    else
        log_error "Deployment is unhealthy: $ready_replicas/$desired_replicas replicas ready"
        return 1
    fi

    # HTTP health check
    log_info "Running HTTP health check..."
    local service_ip=$(kubectl get service ghost-service -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}')

    if kubectl run health-check --image=curlimages/curl:latest --rm -i --restart=Never -n "$NAMESPACE" \
        -- curl -f -s "http://${service_ip}:2368/ghost/api/v3/admin/site/" &> /dev/null; then
        log_success "HTTP health check passed"
    else
        log_error "HTTP health check failed"
        return 1
    fi

    # Get new version info
    local new_version=$(kubectl get deployment -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[0].spec.template.spec.containers[0].image}')
    log_info "Rolled back to version: $new_version"

    log_success "Rollback verification completed successfully"
}

# Send notification
send_notification() {
    local status="$1"
    local message="$2"

    if [[ -n "${SLACK_WEBHOOK_URL:-}" ]]; then
        local color="warning"
        local emoji=":back:"

        if [[ "$status" == "critical" ]]; then
            color="danger"
            emoji=":rotating_light:"
        elif [[ "$status" == "success" ]]; then
            color="good"
            emoji=":white_check_mark:"
        fi

        curl -X POST "$SLACK_WEBHOOK_URL" \
            -H 'Content-Type: application/json' \
            -d "{
                \"attachments\": [{
                    \"color\": \"$color\",
                    \"title\": \"${emoji} Ghost Rollback - ${ENVIRONMENT}\",
                    \"text\": \"$message\",
                    \"fields\": [
                        {\"title\": \"Environment\", \"value\": \"$ENVIRONMENT\", \"short\": true},
                        {\"title\": \"From Revision\", \"value\": \"${CURRENT_REVISION:-N/A}\", \"short\": true},
                        {\"title\": \"To Revision\", \"value\": \"${TARGET_REVISION:-backup}\", \"short\": true},
                        {\"title\": \"Timestamp\", \"value\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\", \"short\": true}
                    ]
                }]
            }" &> /dev/null || log_warning "Failed to send Slack notification"
    fi
}

# Cleanup old rollback snapshots
cleanup_old_snapshots() {
    log_info "Cleaning up old rollback snapshots..."

    local backup_base_dir="${PROJECT_ROOT}/backups/${ENVIRONMENT}"

    if [[ -d "$backup_base_dir" ]]; then
        # Keep only last 10 snapshots
        find "$backup_base_dir" -maxdepth 1 -type d -name "pre-rollback-*" | \
            sort -r | tail -n +11 | xargs rm -rf 2>/dev/null || true

        log_success "Snapshot cleanup completed"
    fi
}

# Main rollback logic
main() {
    log_info "=========================================="
    log_info "Ghost CMS Enterprise Rollback"
    log_info "=========================================="
    log_info "Environment: $ENVIRONMENT"
    log_info "Namespace: $NAMESPACE"
    log_info "Dry Run: $DRY_RUN"
    log_info "=========================================="

    # Run pre-flight checks
    preflight_checks

    # Get rollback information
    get_rollback_info

    # Confirm rollback
    confirm_rollback

    # Create snapshot
    if [[ "$DRY_RUN" == "false" ]]; then
        create_snapshot
    fi

    # Send notification
    send_notification "warning" "Rollback initiated"

    # Perform rollback
    if [[ -n "$TARGET_BACKUP" ]]; then
        backup_rollback
        restore_database
    else
        helm_rollback
    fi

    # Verify rollback
    if [[ "$DRY_RUN" == "false" ]]; then
        if verify_rollback; then
            log_success "=========================================="
            log_success "Rollback completed successfully!"
            log_success "Environment: $ENVIRONMENT"
            log_success "Revision: ${TARGET_REVISION:-backup}"
            log_success "=========================================="

            send_notification "success" "Rollback completed successfully"

            cleanup_old_snapshots
            exit 0
        else
            log_error "Rollback verification failed"
            log_error "Manual intervention required!"

            send_notification "critical" "Rollback verification failed - manual intervention required"
            exit 1
        fi
    else
        log_info "Dry run completed successfully"
        exit 0
    fi
}

# Parse arguments and run
parse_arguments "$@"
main
