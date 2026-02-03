#!/bin/bash
# Ghost Platform - Health Check Script
# Comprehensive health validation for all infrastructure components
#
# Usage:
#   ./health-check.sh [options]
#
# Options:
#   --full              Full health check (default)
#   --quick             Quick check (services only)
#   --component NAME    Check specific component
#   --watch             Continuous monitoring mode
#   --format FORMAT     Output format: text, json, nagios
#   --help              Show this help

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
CHECK_INTERVAL=30

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Status tracking
declare -A CHECK_RESULTS
declare -A CHECK_MESSAGES
OVERALL_STATUS="HEALTHY"

# Parse arguments
FULL_CHECK=true
QUICK_CHECK=false
WATCH_MODE=false
OUTPUT_FORMAT="text"
TARGET_COMPONENT=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --full)
            FULL_CHECK=true
            QUICK_CHECK=false
            shift
            ;;
        --quick)
            QUICK_CHECK=true
            FULL_CHECK=false
            shift
            ;;
        --component)
            TARGET_COMPONENT="$2"
            shift 2
            ;;
        --watch)
            WATCH_MODE=true
            shift
            ;;
        --format)
            OUTPUT_FORMAT="$2"
            shift 2
            ;;
        --help)
            echo "Ghost Platform Health Check"
            echo ""
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --full              Full health check (default)"
            echo "  --quick             Quick check (services only)"
            echo "  --component NAME    Check specific component"
            echo "  --watch             Continuous monitoring mode"
            echo "  --format FORMAT     Output format: text, json, nagios"
            echo "  --help              Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Output functions
print_header() {
    if [ "$OUTPUT_FORMAT" = "text" ]; then
        echo -e "\n${BLUE}=== $1 ===${NC}"
    fi
}

print_check() {
    local name="$1"
    local status="$2"
    local message="${3:-}"
    
    CHECK_RESULTS[$name]="$status"
    CHECK_MESSAGES[$name]="$message"
    
    if [ "$status" = "FAIL" ]; then
        OVERALL_STATUS="UNHEALTHY"
    elif [ "$status" = "WARN" ] && [ "$OVERALL_STATUS" = "HEALTHY" ]; then
        OVERALL_STATUS="DEGRADED"
    fi
    
    if [ "$OUTPUT_FORMAT" = "text" ]; then
        local color="$GREEN"
        local symbol="✓"
        if [ "$status" = "FAIL" ]; then
            color="$RED"
            symbol="✗"
        elif [ "$status" = "WARN" ]; then
            color="$YELLOW"
            symbol="⚠"
        fi
        
        echo -e "${color}${symbol}${NC} $name"
        if [ -n "$message" ]; then
            echo "   $message"
        fi
    fi
}

# Check functions
check_docker() {
    print_header "Docker Infrastructure"
    
    if ! command -v docker &> /dev/null; then
        print_check "Docker Installed" "FAIL" "Docker not found"
        return 1
    fi
    print_check "Docker Installed" "OK"
    
    if ! docker info &> /dev/null; then
        print_check "Docker Daemon" "FAIL" "Docker daemon not running"
        return 1
    fi
    print_check "Docker Daemon" "OK"
    
    # Check disk usage
    local docker_df=$(docker system df --format '{{.Size}}' | head -1)
    print_check "Docker Disk Usage" "OK" "$docker_df"
}

check_container() {
    local name="$1"
    local required="${2:-true}"
    
    local status
    status=$(docker inspect --format='{{.State.Status}}' "$name" 2>/dev/null || echo "not_found")
    
    if [ "$status" = "not_found" ]; then
        if [ "$required" = true ]; then
            print_check "Container: $name" "FAIL" "Container not found"
        fi
        return 1
    fi
    
    if [ "$status" != "running" ]; then
        print_check "Container: $name" "FAIL" "Status: $status"
        return 1
    fi
    
    # Check health
    local health
    health=$(docker inspect --format='{{.State.Health.Status}}' "$name" 2>/dev/null || echo "unknown")
    
    if [ "$health" = "unhealthy" ]; then
        print_check "Container: $name" "FAIL" "Health check failing"
        return 1
    elif [ "$health" = "starting" ]; then
        print_check "Container: $name" "WARN" "Still starting"
    else
        print_check "Container: $name" "OK" "Status: running, Health: $health"
    fi
}

check_postgres() {
    print_header "PostgreSQL Database"
    
    if ! check_container "ghost-postgres"; then
        return 1
    fi
    
    # Check connectivity
    if docker exec ghost-postgres pg_isready -U ghost &> /dev/null; then
        print_check "PostgreSQL Connectivity" "OK"
    else
        print_check "PostgreSQL Connectivity" "FAIL" "Cannot connect"
        return 1
    fi
    
    # Check connections
    local connections
    connections=$(docker exec ghost-postgres psql -U ghost -t -c "SELECT count(*) FROM pg_stat_activity;" 2>/dev/null | tr -d ' ')
    if [ -n "$connections" ]; then
        if [ "$connections" -gt 80 ]; then
            print_check "PostgreSQL Connections" "WARN" "$connections/100 connections"
        else
            print_check "PostgreSQL Connections" "OK" "$connections active"
        fi
    fi
    
    # Check database size
    local db_size
    db_size=$(docker exec ghost-postgres psql -U ghost -t -c "SELECT pg_size_pretty(pg_database_size('ghost'));" 2>/dev/null | tr -d ' ')
    print_check "PostgreSQL Database Size" "OK" "$db_size"
    
    # Check replication lag (if applicable)
    # Skipped for single-node deployment
    
    if [ "$FULL_CHECK" = true ]; then
        # Check table bloat
        local bloat
        bloat=$(docker exec ghost-postgres psql -U ghost -t -c "
            SELECT count(*) FROM pg_stat_user_tables 
            WHERE n_dead_tup > 1000;
        " 2>/dev/null | tr -d ' ')
        
        if [ -n "$bloat" ] && [ "$bloat" -gt 0 ]; then
            print_check "PostgreSQL Table Maintenance" "WARN" "$bloat tables need vacuum"
        else
            print_check "PostgreSQL Table Maintenance" "OK"
        fi
    fi
}

check_redis() {
    print_header "Redis Cache"
    
    if ! check_container "ghost-redis"; then
        return 1
    fi
    
    # Check connectivity
    if docker exec ghost-redis redis-cli ping | grep -q "PONG"; then
        print_check "Redis Connectivity" "OK"
    else
        print_check "Redis Connectivity" "FAIL"
        return 1
    fi
    
    # Check memory
    local memory
    memory=$(docker exec ghost-redis redis-cli INFO memory | grep used_memory_human | cut -d: -f2 | tr -d '\r')
    print_check "Redis Memory Usage" "OK" "$memory"
    
    # Check hit rate
    local hits misses hit_rate
    hits=$(docker exec ghost-redis redis-cli INFO stats | grep keyspace_hits | cut -d: -f2 | tr -d '\r')
    misses=$(docker exec ghost-redis redis-cli INFO stats | grep keyspace_misses | cut -d: -f2 | tr -d '\r')
    
    if [ -n "$hits" ] && [ -n "$misses" ] && [ "$((hits + misses))" -gt 0 ]; then
        hit_rate=$(echo "scale=2; $hits * 100 / ($hits + $misses)" | bc)
        if (( $(echo "$hit_rate < 80" | bc -l) )); then
            print_check "Redis Hit Rate" "WARN" "${hit_rate}%"
        else
            print_check "Redis Hit Rate" "OK" "${hit_rate}%"
        fi
    fi
    
    # Check connected clients
    local clients
    clients=$(docker exec ghost-redis redis-cli INFO clients | grep connected_clients | cut -d: -f2 | tr -d '\r')
    print_check "Redis Connected Clients" "OK" "$clients"
}

check_rabbitmq() {
    print_header "RabbitMQ Message Broker"
    
    if ! check_container "ghost-rabbitmq"; then
        return 1
    fi
    
    # Check management API
    if curl -s http://localhost:15672/api/overview -u guest:guest &> /dev/null; then
        print_check "RabbitMQ Management API" "OK"
    else
        print_check "RabbitMQ Management API" "FAIL"
        return 1
    fi
    
    # Check queues
    local queue_info
    queue_info=$(curl -s http://localhost:15672/api/queues -u guest:guest 2>/dev/null)
    
    local queue_count
    queue_count=$(echo "$queue_info" | jq '. | length')
    print_check "RabbitMQ Queues" "OK" "$queue_count queues defined"
    
    # Check for high message counts
    local high_queues
    high_queues=$(echo "$queue_info" | jq '[.[] | select(.messages_ready > 1000)] | length')
    if [ "$high_queues" -gt 0 ]; then
        print_check "RabbitMQ Queue Depth" "WARN" "$high_queues queues have >1000 messages"
    else
        print_check "RabbitMQ Queue Depth" "OK"
    fi
    
    # Check consumers
    local consumers
    consumers=$(echo "$queue_info" | jq '[.[].consumers] | add // 0')
    print_check "RabbitMQ Consumers" "OK" "$consumers active"
}

check_application() {
    print_header "Ghost Application"
    
    if ! check_container "ghost-webapi"; then
        return 1
    fi
    
    # Check health endpoint
    local health_status
    health_status=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/health 2>/dev/null || echo "000")
    
    if [ "$health_status" = "200" ]; then
        print_check "Application Health Endpoint" "OK"
    else
        print_check "Application Health Endpoint" "FAIL" "HTTP $health_status"
        return 1
    fi
    
    # Check API response time
    local response_time
    response_time=$(curl -s -o /dev/null -w "%{time_total}" http://localhost:8080/health 2>/dev/null)
    response_time_ms=$(echo "$response_time * 1000" | bc | cut -d. -f1)
    
    if [ "$response_time_ms" -gt 500 ]; then
        print_check "API Response Time" "WARN" "${response_time_ms}ms"
    else
        print_check "API Response Time" "OK" "${response_time_ms}ms"
    fi
    
    if [ "$FULL_CHECK" = true ]; then
        # Check metrics endpoint
        local metrics_status
        metrics_status=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/metrics 2>/dev/null || echo "000")
        
        if [ "$metrics_status" = "200" ]; then
            print_check "Metrics Endpoint" "OK"
        else
            print_check "Metrics Endpoint" "WARN" "HTTP $metrics_status"
        fi
    fi
}

check_nginx() {
    print_header "Nginx Reverse Proxy"
    
    if ! check_container "ghost-nginx"; then
        return 1
    fi
    
    # Check nginx is responding
    if curl -s -o /dev/null -w "%{http_code}" http://localhost/health 2>/dev/null | grep -q "200"; then
        print_check "Nginx Health Check" "OK"
    else
        print_check "Nginx Health Check" "FAIL"
    fi
}

check_monitoring() {
    print_header "Monitoring Stack"
    
    # Prometheus
    if check_container "ghost-prometheus" false; then
        if curl -s -o /dev/null -w "%{http_code}" http://localhost:9090/-/healthy 2>/dev/null | grep -q "200"; then
            print_check "Prometheus" "OK"
        else
            print_check "Prometheus" "FAIL"
        fi
    fi
    
    # Grafana
    if check_container "ghost-grafana" false; then
        if curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/api/health 2>/dev/null | grep -q "200"; then
            print_check "Grafana" "OK"
        else
            print_check "Grafana" "FAIL"
        fi
    fi
}

check_system_resources() {
    print_header "System Resources"
    
    # CPU usage
    local cpu_usage
    cpu_usage=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1)
    if (( $(echo "$cpu_usage > 80" | bc -l) )); then
        print_check "CPU Usage" "WARN" "${cpu_usage}%"
    else
        print_check "CPU Usage" "OK" "${cpu_usage}%"
    fi
    
    # Memory usage
    local mem_usage
    mem_usage=$(free | grep Mem | awk '{printf "%.1f", $3/$2 * 100.0}')
    if (( $(echo "$mem_usage > 90" | bc -l) )); then
        print_check "Memory Usage" "FAIL" "${mem_usage}%"
    elif (( $(echo "$mem_usage > 80" | bc -l) )); then
        print_check "Memory Usage" "WARN" "${mem_usage}%"
    else
        print_check "Memory Usage" "OK" "${mem_usage}%"
    fi
    
    # Disk usage
    local disk_usage
    disk_usage=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
    if [ "$disk_usage" -gt 90 ]; then
        print_check "Disk Usage" "FAIL" "${disk_usage}%"
    elif [ "$disk_usage" -gt 80 ]; then
        print_check "Disk Usage" "WARN" "${disk_usage}%"
    else
        print_check "Disk Usage" "OK" "${disk_usage}%"
    fi
}

# Output results
output_json() {
    echo "{"
    echo "  \"timestamp\": \"$(date -Iseconds)\","
    echo "  \"status\": \"$OVERALL_STATUS\","
    echo "  \"checks\": {"
    
    local first=true
    for check in "${!CHECK_RESULTS[@]}"; do
        if [ "$first" = true ]; then
            first=false
        else
            echo ","
        fi
        echo -n "    \"$check\": {"
        echo -n "\"status\": \"${CHECK_RESULTS[$check]}\", "
        echo -n "\"message\": \"${CHECK_MESSAGES[$check]}\""
        echo -n "}"
    done
    
    echo ""
    echo "  }"
    echo "}"
}

output_nagios() {
    case "$OVERALL_STATUS" in
        HEALTHY)
            echo "OK - All checks passed | checks=${#CHECK_RESULTS[@]}"
            exit 0
            ;;
        DEGRADED)
            echo "WARNING - Some checks degraded | checks=${#CHECK_RESULTS[@]}"
            exit 1
            ;;
        UNHEALTHY)
            echo "CRITICAL - Some checks failed | checks=${#CHECK_RESULTS[@]}"
            exit 2
            ;;
    esac
}

# Main check execution
run_checks() {
    CHECK_RESULTS=()
    CHECK_MESSAGES=()
    OVERALL_STATUS="HEALTHY"
    
    check_docker
    
    if [ -n "$TARGET_COMPONENT" ]; then
        case "$TARGET_COMPONENT" in
            postgres)
                check_postgres
                ;;
            redis)
                check_redis
                ;;
            rabbitmq)
                check_rabbitmq
                ;;
            app|application|webapi)
                check_application
                ;;
            nginx)
                check_nginx
                ;;
            monitoring)
                check_monitoring
                ;;
            *)
                echo "Unknown component: $TARGET_COMPONENT"
                exit 1
                ;;
        esac
    else
        check_postgres
        check_redis
        check_rabbitmq
        check_application
        check_nginx
        
        if [ "$FULL_CHECK" = true ]; then
            check_monitoring
            check_system_resources
        fi
    fi
}

# Main execution
main() {
    if [ "$WATCH_MODE" = true ]; then
        while true; do
            clear
            echo "Ghost Platform Health Check - Watch Mode"
            echo "Press Ctrl+C to exit"
            echo ""
            
            run_checks
            
            if [ "$OUTPUT_FORMAT" = "text" ]; then
                echo ""
                echo "Next check in ${CHECK_INTERVAL}s..."
            fi
            
            sleep "$CHECK_INTERVAL"
        done
    else
        run_checks
        
        case "$OUTPUT_FORMAT" in
            json)
                output_json
                ;;
            nagios)
                output_nagios
                ;;
            *)
                echo ""
                echo "=============================================="
                if [ "$OVERALL_STATUS" = "HEALTHY" ]; then
                    echo -e "${GREEN}Overall Status: HEALTHY${NC}"
                elif [ "$OVERALL_STATUS" = "DEGRADED" ]; then
                    echo -e "${YELLOW}Overall Status: DEGRADED${NC}"
                else
                    echo -e "${RED}Overall Status: UNHEALTHY${NC}"
                fi
                echo "=============================================="
                ;;
        esac
        
        # Exit code
        if [ "$OVERALL_STATUS" = "UNHEALTHY" ]; then
            exit 2
        elif [ "$OVERALL_STATUS" = "DEGRADED" ]; then
            exit 1
        else
            exit 0
        fi
    fi
}

main "$@"