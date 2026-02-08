#!/bin/bash
###############################################################################
# Enterprise Ghost CMS Deployment Script
# Description: Production-grade deployment with health checks and rollback
# Usage: ./deploy.sh [environment] [version] [options]
###############################################################################

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
LOG_FILE="/var/log/ghost-deploy-$(date +%Y%m%d-%H%M%S).log"
DEPLOYMENT_TIMEOUT="${DEPLOYMENT_TIMEOUT:-900}"  # 15 minutes
HEALTH_CHECK_RETRIES="${HEALTH_CHECK_RETRIES:-30}"
HEALTH_CHECK_INTERVAL="${HEALTH_CHECK_INTERVAL:-10}"

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
    log_error "Script failed at line $line_number"
    log_error "Initiating automatic rollback..."
    rollback_deployment
    exit 1
}

trap 'error_handler ${LINENO}' ERR

# Usage information
usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Enterprise Ghost CMS Deployment Script

OPTIONS:
    -e, --environment ENV       Target environment (development|staging|production)
    -v, --version VERSION       Version/tag to deploy
    -s, --strategy STRATEGY     Deployment strategy (rolling|bluegreen|canary)
    -d, --dry-run              Perform dry run without actual deployment
    -n, --namespace NAMESPACE   Kubernetes namespace (default: ghost-ENV)
    -h, --help                 Display this help message

EXAMPLES:
    $0 -e production -v v1.2.3 -s bluegreen
    $0 --environment staging --version latest --dry-run

ENVIRONMENT VARIABLES:
    KUBECONFIG              Path to kubeconfig file
    DEPLOYMENT_TIMEOUT      Deployment timeout in seconds (default: 900)
    SLACK_WEBHOOK_URL       Slack webhook for notifications
    ROLLBACK_ON_FAILURE     Auto-rollback on failure (default: true)

EOF
    exit 0
}

# Parse arguments
parse_arguments() {
    ENVIRONMENT=""
    VERSION=""
    STRATEGY="rolling"
    DRY_RUN=false
    NAMESPACE=""

    while [[ $# -gt 0 ]]; do
        case $1 in
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -v|--version)
                VERSION="$2"
                shift 2
                ;;
            -s|--strategy)
                STRATEGY="$2"
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

    if [[ -z "$VERSION" ]]; then
        log_error "Version is required"
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

    # Validate strategy
    if [[ ! "$STRATEGY" =~ ^(rolling|bluegreen|canary)$ ]]; then
        log_error "Invalid deployment strategy: $STRATEGY"
        exit 1
    fi
}

# Pre-flight checks
preflight_checks() {
    log_info "Running pre-flight checks..."

    # Check required commands
    local required_commands=("kubectl" "helm" "jq" "aws" "curl")
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
        log_warning "Namespace $NAMESPACE does not exist, creating..."
        kubectl create namespace "$NAMESPACE"
    fi

    # Verify Helm chart exists
    if [[ ! -d "${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart" ]]; then
        log_error "Helm chart not found"
        exit 1
    fi

    # Check production approval
    if [[ "$ENVIRONMENT" == "production" ]] && [[ "${SKIP_APPROVAL:-false}" != "true" ]]; then
        log_warning "Deploying to PRODUCTION environment"
        read -p "Are you sure you want to continue? (yes/no): " -r
        if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
            log_info "Deployment cancelled by user"
            exit 0
        fi
    fi

    log_success "Pre-flight checks passed"
}

# Backup current deployment
backup_deployment() {
    log_info "Creating backup of current deployment..."

    local backup_dir="${PROJECT_ROOT}/backups/${ENVIRONMENT}/$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$backup_dir"

    # Backup Helm values
    if helm list -n "$NAMESPACE" | grep -q "ghost"; then
        helm get values ghost -n "$NAMESPACE" -o yaml > "${backup_dir}/values.yaml"
        helm get manifest ghost -n "$NAMESPACE" > "${backup_dir}/manifests.yaml"

        # Store current image version
        kubectl get deployment -n "$NAMESPACE" -l app=ghost -o jsonpath='{.items[0].spec.template.spec.containers[0].image}' > "${backup_dir}/current-image.txt"

        log_success "Backup created at: $backup_dir"
        echo "$backup_dir" > /tmp/ghost-last-backup
    else
        log_warning "No existing deployment found to backup"
    fi
}

# Database backup
backup_database() {
    log_info "Creating database backup..."

    local backup_name="ghost-db-backup-$(date +%Y%m%d-%H%M%S)"

    # Get MySQL credentials from secret
    local mysql_password=$(kubectl get secret ghost-mysql-secret -n "$NAMESPACE" -o jsonpath='{.data.mysql-password}' | base64 -d)
    local mysql_host=$(kubectl get service ghost-mysql -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}')

    # Create backup pod
    kubectl run mysql-backup \
        --image=mysql:8.0 \
        --rm -i --restart=Never \
        -n "$NAMESPACE" \
        --env="MYSQL_PWD=${mysql_password}" \
        -- mysqldump -h "${mysql_host}" -u ghost ghost_production > "/tmp/${backup_name}.sql" 2>> "$LOG_FILE" || true

    # Upload to S3 or backup storage
    if command -v aws &> /dev/null; then
        aws s3 cp "/tmp/${backup_name}.sql" "s3://ghost-backups/${ENVIRONMENT}/database/${backup_name}.sql"
        log_success "Database backup uploaded to S3"
    else
        log_warning "AWS CLI not found, database backup saved locally only"
    fi

    rm -f "/tmp/${backup_name}.sql"
}

# Deploy using Rolling Update strategy
deploy_rolling() {
    log_info "Executing rolling update deployment..."

    helm upgrade --install ghost \
        "${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart" \
        --namespace "$NAMESPACE" \
        --values "${PROJECT_ROOT}/infrastructure/environments/${ENVIRONMENT}/values.yaml" \
        --set image.tag="$VERSION" \
        --set image.repository="${IMAGE_REPOSITORY:-ghcr.io/rudironsoni/ghost}" \
        --wait \
        --timeout "${DEPLOYMENT_TIMEOUT}s" \
        --atomic \
        ${DRY_RUN:+--dry-run} \
        2>&1 | tee -a "$LOG_FILE"
}

# Deploy using Blue-Green strategy
deploy_bluegreen() {
    log_info "Executing blue-green deployment..."

    local current_color=$(kubectl get deployment -n "$NAMESPACE" -l app=ghost,active=true -o jsonpath='{.items[0].metadata.labels.version}' 2>/dev/null || echo "blue")
    local new_color=$([[ "$current_color" == "blue" ]] && echo "green" || echo "blue")

    log_info "Current active deployment: $current_color"
    log_info "Deploying new version as: $new_color"

    # Deploy new color
    helm upgrade --install "ghost-${new_color}" \
        "${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart" \
        --namespace "$NAMESPACE" \
        --values "${PROJECT_ROOT}/infrastructure/environments/${ENVIRONMENT}/values.yaml" \
        --set image.tag="$VERSION" \
        --set image.repository="${IMAGE_REPOSITORY:-ghcr.io/rudironsoni/ghost}" \
        --set deploymentName="ghost-${new_color}" \
        --set labels.version="$new_color" \
        --set labels.active="false" \
        --wait \
        --timeout "${DEPLOYMENT_TIMEOUT}s" \
        ${DRY_RUN:+--dry-run} \
        2>&1 | tee -a "$LOG_FILE"

    if [[ "$DRY_RUN" == "false" ]]; then
        # Health check on new deployment
        if health_check "$new_color"; then
            log_info "Switching traffic to $new_color deployment..."

            # Update service selector
            kubectl patch service ghost-service -n "$NAMESPACE" \
                -p "{\"spec\":{\"selector\":{\"version\":\"${new_color}\"}}}"

            # Mark new deployment as active
            kubectl label deployment "ghost-${new_color}" -n "$NAMESPACE" active=true --overwrite
            kubectl label deployment "ghost-${current_color}" -n "$NAMESPACE" active=false --overwrite 2>/dev/null || true

            log_success "Traffic switched to $new_color deployment"

            # Wait before cleanup
            log_info "Waiting 60 seconds before cleaning up old deployment..."
            sleep 60

            # Cleanup old deployment
            helm delete "ghost-${current_color}" -n "$NAMESPACE" 2>/dev/null || true
            kubectl delete deployment "ghost-${current_color}" -n "$NAMESPACE" --ignore-not-found=true

            log_success "Old $current_color deployment cleaned up"
        else
            log_error "Health check failed for $new_color deployment"
            return 1
        fi
    fi
}

# Deploy using Canary strategy
deploy_canary() {
    log_info "Executing canary deployment..."

    local canary_weight="${CANARY_WEIGHT:-10}"

    # Deploy canary
    helm upgrade --install ghost-canary \
        "${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart" \
        --namespace "$NAMESPACE" \
        --values "${PROJECT_ROOT}/infrastructure/environments/${ENVIRONMENT}/values.yaml" \
        --set image.tag="$VERSION" \
        --set image.repository="${IMAGE_REPOSITORY:-ghcr.io/rudironsoni/ghost}" \
        --set deploymentName="ghost-canary" \
        --set replicaCount=1 \
        --set labels.version="canary" \
        --wait \
        --timeout "${DEPLOYMENT_TIMEOUT}s" \
        ${DRY_RUN:+--dry-run} \
        2>&1 | tee -a "$LOG_FILE"

    if [[ "$DRY_RUN" == "false" ]]; then
        # Monitor canary
        log_info "Monitoring canary deployment for 5 minutes..."
        sleep 300

        if health_check "canary"; then
            log_info "Canary healthy, proceeding with full rollout..."
            deploy_rolling

            # Cleanup canary
            helm delete ghost-canary -n "$NAMESPACE"
            log_success "Canary deployment successful and cleaned up"
        else
            log_error "Canary deployment failed health checks"
            helm delete ghost-canary -n "$NAMESPACE"
            return 1
        fi
    fi
}

# Health check function
health_check() {
    local deployment_label="${1:-app=ghost}"
    log_info "Running health checks for deployment: $deployment_label"

    local retries=0
    local max_retries="$HEALTH_CHECK_RETRIES"

    while [[ $retries -lt $max_retries ]]; do
        # Check pod status
        local ready_pods=$(kubectl get pods -n "$NAMESPACE" -l "$deployment_label" -o jsonpath='{.items[*].status.conditions[?(@.type=="Ready")].status}' | grep -o "True" | wc -l)
        local total_pods=$(kubectl get pods -n "$NAMESPACE" -l "$deployment_label" --no-headers | wc -l)

        if [[ $ready_pods -eq $total_pods ]] && [[ $total_pods -gt 0 ]]; then
            log_info "All pods ready ($ready_pods/$total_pods)"

            # HTTP health check
            local service_ip=$(kubectl get service ghost-service -n "$NAMESPACE" -o jsonpath='{.spec.clusterIP}')
            if kubectl run health-check --image=curlimages/curl:latest --rm -i --restart=Never -n "$NAMESPACE" \
                -- curl -f -s "http://${service_ip}:2368/ghost/api/v3/admin/site/" &> /dev/null; then
                log_success "Health check passed"
                return 0
            else
                log_warning "HTTP health check failed, retrying..."
            fi
        else
            log_info "Pods not ready yet: $ready_pods/$total_pods ready"
        fi

        retries=$((retries + 1))
        sleep "$HEALTH_CHECK_INTERVAL"
    done

    log_error "Health check failed after $max_retries attempts"
    return 1
}

# Rollback function
rollback_deployment() {
    log_warning "Initiating deployment rollback..."

    if [[ -f /tmp/ghost-last-backup ]]; then
        local backup_dir=$(cat /tmp/ghost-last-backup)

        if [[ -f "${backup_dir}/values.yaml" ]]; then
            log_info "Restoring from backup: $backup_dir"

            helm upgrade --install ghost \
                "${PROJECT_ROOT}/infrastructure/automation/templates/helm-chart" \
                --namespace "$NAMESPACE" \
                --values "${backup_dir}/values.yaml" \
                --wait \
                --timeout 600s \
                --force

            log_success "Rollback completed successfully"
        else
            log_error "Backup files not found"
            helm rollback ghost -n "$NAMESPACE"
        fi
    else
        log_warning "No backup found, using Helm rollback"
        helm rollback ghost -n "$NAMESPACE"
    fi
}

# Send notification
send_notification() {
    local status="$1"
    local message="$2"

    if [[ -n "${SLACK_WEBHOOK_URL:-}" ]]; then
        local color=$([[ "$status" == "success" ]] && echo "good" || echo "danger")
        local emoji=$([[ "$status" == "success" ]] && echo ":rocket:" || echo ":x:")

        curl -X POST "$SLACK_WEBHOOK_URL" \
            -H 'Content-Type: application/json' \
            -d "{
                \"attachments\": [{
                    \"color\": \"$color\",
                    \"title\": \"${emoji} Ghost Deployment - ${ENVIRONMENT}\",
                    \"text\": \"$message\",
                    \"fields\": [
                        {\"title\": \"Environment\", \"value\": \"$ENVIRONMENT\", \"short\": true},
                        {\"title\": \"Version\", \"value\": \"$VERSION\", \"short\": true},
                        {\"title\": \"Strategy\", \"value\": \"$STRATEGY\", \"short\": true},
                        {\"title\": \"Timestamp\", \"value\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\", \"short\": true}
                    ]
                }]
            }" &> /dev/null || log_warning "Failed to send Slack notification"
    fi
}

# Main deployment logic
main() {
    log_info "=========================================="
    log_info "Ghost CMS Enterprise Deployment"
    log_info "=========================================="
    log_info "Environment: $ENVIRONMENT"
    log_info "Version: $VERSION"
    log_info "Strategy: $STRATEGY"
    log_info "Namespace: $NAMESPACE"
    log_info "Dry Run: $DRY_RUN"
    log_info "=========================================="

    # Run pre-flight checks
    preflight_checks

    # Create backups
    if [[ "$DRY_RUN" == "false" ]] && [[ "$ENVIRONMENT" != "development" ]]; then
        backup_deployment
        backup_database
    fi

    # Execute deployment based on strategy
    case "$STRATEGY" in
        rolling)
            deploy_rolling
            ;;
        bluegreen)
            deploy_bluegreen
            ;;
        canary)
            deploy_canary
            ;;
    esac

    # Post-deployment verification
    if [[ "$DRY_RUN" == "false" ]]; then
        log_info "Running post-deployment verification..."

        if health_check; then
            log_success "=========================================="
            log_success "Deployment completed successfully!"
            log_success "Environment: $ENVIRONMENT"
            log_success "Version: $VERSION"
            log_success "=========================================="

            send_notification "success" "Deployment completed successfully"
            exit 0
        else
            log_error "Post-deployment verification failed"

            if [[ "${ROLLBACK_ON_FAILURE:-true}" == "true" ]]; then
                rollback_deployment
            fi

            send_notification "failure" "Deployment failed and was rolled back"
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
