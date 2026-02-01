#!/bin/bash

# =============================================================================
# Ghost Job Search - Full Test Script
# =============================================================================

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
SEARCH_URL="$API_URL/api/jobs/search"
HEALTH_URL="$API_URL/health"
JOBS_HEALTH_URL="$API_URL/api/jobs/health"
API_DIR="src/Ghost.WebApi"
LOG_FILE="ghost_api.log"
API_PID=""

# Test parameters
QUERY="Software Engineer"
LOCATION="San Francisco"
MAX_RESULTS=5

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# =============================================================================
# Functions
# =============================================================================

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

check_jq() {
    if command -v jq >/dev/null 2>&1; then
        return 0
    else
        print_error "jq not found. Install jq for JSON parsing."
        echo "  Ubuntu/Debian: sudo apt-get install jq"
        echo "  macOS: brew install jq"
        return 1
    fi
}

start_api() {
    print_header "Starting Ghost.WebAPI"

    # Check if .env file exists
    if [ -f ".env" ]; then
        print_success "Found .env configuration file"
    else
        print_warning ".env file not found. Using default configuration."
        echo "  Create .env for custom platform configuration."
    fi

    # Start the API in background
    print_success "Starting API at $API_URL"
    dotnet run --project "$API_DIR" > "$LOG_FILE" 2>&1 &
    API_PID=$!

    echo "API PID: $API_PID"
    echo "Log file: $LOG_FILE"

    # Wait for API to be ready
    print_header "Waiting for API to start..."

    MAX_WAIT=30
    WAIT_COUNT=0
    while [ $WAIT_COUNT -lt $MAX_WAIT ]; do
        if curl -s "$HEALTH_URL" >/dev/null 2>&1; then
            print_success "API is ready (took ${WAIT_COUNT}s)"
            return 0
        fi
        sleep 1
        WAIT_COUNT=$((WAIT_COUNT + 1))
        echo -n "."
    done

    echo ""
    print_error "API failed to start within ${MAX_WAIT}s"
    return 1
}

stop_api() {
    print_header "Stopping Ghost.WebAPI"

    if [ -n "$API_PID" ]; then
        echo "Shutting down API (PID: $API_PID)..."
        kill $API_PID 2>/dev/null
        wait $API_PID 2>/dev/null
        print_success "API stopped"
    fi

    # Check for any remaining dotnet processes
    if pgrep -f "dotnet.*Ghost.WebApi" >/dev/null 2>&1; then
        print_warning "Cleaning up remaining dotnet processes..."
        pkill -f "dotnet.*Ghost.WebApi" 2>/dev/null
    fi
}

test_health() {
    print_header "Health Check"

    echo "Checking health endpoint: $HEALTH_URL"
    HEALTH_RESPONSE=$(curl -s "$HEALTH_URL")

    if [ -z "$HEALTH_RESPONSE" ]; then
        print_error "Health check failed - no response"
        return 1
    fi

    echo "$HEALTH_RESPONSE" | jq . 2>/dev/null || echo "$HEALTH_RESPONSE"
    echo ""

    echo "Checking jobs health endpoint: $JOBS_HEALTH_URL"
    JOBS_HEALTH_RESPONSE=$(curl -s "$JOBS_HEALTH_URL")

    if [ -z "$JOBS_HEALTH_RESPONSE" ]; then
        print_error "Jobs health check failed - no response"
        return 1
    fi

    echo "$JOBS_HEALTH_RESPONSE" | jq . 2>/dev/null || echo "$JOBS_HEALTH_RESPONSE"
    echo ""
}

test_platform() {
    PLATFORM=$1
    SOURCE_FIELD=$2  # "Sources" or "platforms" depending on API

    echo ""
    print_header "Testing Platform: $PLATFORM"

    # Build payload
    PAYLOAD=$(cat <<EOF
{
  "Query": "$QUERY",
  "Location": "$LOCATION",
  "MaxResults": $MAX_RESULTS,
  "$SOURCE_FIELD": ["$PLATFORM"]
}
EOF
)

    echo "Request:"
    echo "  URL: $SEARCH_URL"
    echo "  Query: $QUERY"
    echo "  Location: $LOCATION"
    echo "  MaxResults: $MAX_RESULTS"
    echo "  $SOURCE_FIELD: [$PLATFORM]"
    echo ""

    # Make request
    RESPONSE=$(curl -s -X POST "$SEARCH_URL" \
        -H "Content-Type: application/json" \
        -d "$PAYLOAD" \
        -w "\n%{http_code}")

    # Extract HTTP code and body
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    RESPONSE_BODY=$(echo "$RESPONSE" | head -n -1)

    echo "HTTP Status: $HTTP_CODE"

    if [ "$HTTP_CODE" != "200" ]; then
        print_error "HTTP $HTTP_CODE - Request failed"
        echo "Response: $RESPONSE_BODY"
        return 1
    fi

    # Check if response is valid JSON
    if ! echo "$RESPONSE_BODY" | jq . >/dev/null 2>&1; then
        print_error "Invalid JSON response"
        echo "Response: $RESPONSE_BODY"
        return 1
    fi

    # Parse response
    JOB_COUNT=$(echo "$RESPONSE_BODY" | jq 'length' 2>/dev/null || echo 0)

    echo ""
    echo "Jobs Found: $JOB_COUNT"

    if [ "$JOB_COUNT" -gt 0 ]; then
        print_success "Successfully retrieved $JOB_COUNT jobs"

        # Display job listings
        echo ""
        echo "Job Listings:"
        echo "$RESPONSE_BODY" | jq -r '
            .[] |
            "\(.title)",
            "  Company: \(.company // "N/A")",
            "  Location: \(.location // "N/A")",
            "  Salary: \(.salaryRaw // .salary // "N/A")",
            "  Source: \(.source // "N/A")",
            "  URL: \(.url // "N/A")",
            ""
        '
    else
        print_warning "No jobs found"
        echo ""
        echo "Full Response:"
        echo "$RESPONSE_BODY" | jq .
    fi

    echo ""
    echo "---"
}

test_all_platforms() {
    print_header "Testing All Platforms"

    echo "Testing platforms with 'Sources' parameter:"
    test_platform "LinkedIn" "Sources"
    test_platform "Indeed" "Sources"
    test_platform "Google" "Sources"
    test_platform "Glassdoor" "Sources"

    echo ""
    echo "Testing platforms with 'platforms' parameter (alternative API):"
    test_platform "LinkedIn" "platforms"
    test_platform "Indeed" "platforms"
    test_platform "Google" "platforms"
    test_platform "Glassdoor" "platforms"
}

test_aggregated_search() {
    print_header "Aggregated Search (All Platforms)"

    PAYLOAD=$(cat <<EOF
{
  "Query": "$QUERY",
  "Location": "$LOCATION",
  "MaxResults": $MAX_RESULTS
}
EOF
)

    echo "Request:"
    echo "  URL: $SEARCH_URL"
    echo "  Query: $QUERY"
    echo "  Location: $LOCATION"
    echo "  MaxResults: $MAX_RESULTS"
    echo "  (No platform filter - searches all enabled sources)"
    echo ""

    RESPONSE=$(curl -s -X POST "$SEARCH_URL" \
        -H "Content-Type: application/json" \
        -d "$PAYLOAD" \
        -w "\n%{http_code}")

    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    RESPONSE_BODY=$(echo "$RESPONSE" | head -n -1)

    echo "HTTP Status: $HTTP_CODE"

    if [ "$HTTP_CODE" != "200" ]; then
        print_error "HTTP $HTTP_CODE - Request failed"
        echo "Response: $RESPONSE_BODY"
        return 1
    fi

    echo ""
    print_success "Aggregated search completed"
    echo ""

    # Group results by platform
    if check_jq; then
        echo "Results by Platform:"
        echo ""

        echo "$RESPONSE_BODY" | jq -r '
            group_by(.source) | .[] |
            "Platform: \(.[0].source // "Unknown")",
            "  Count: \(length)",
            "  First result: \(.[0].title // "N/A") @ \(.[0].company // "N/A")",
            ""
        '

        echo ""
        echo "All Results:"
        echo "$RESPONSE_BODY" | jq -r '
            .[] |
            "[\(.source // "Unknown")] \(.title) @ \(.company) - \(.location) (\(.salaryRaw // "N/A"))"
        '
    else
        echo "$RESPONSE_BODY"
    fi
}

# =============================================================================
# Main Script
# =============================================================================

main() {
    # Trap signals to cleanup on exit
    trap stop_api EXIT INT TERM

    print_header "Ghost Job Search - Full Test"
    echo ""
    echo "Configuration:"
    echo "  API URL: $API_URL"
    echo "  Query: $QUERY"
    echo "  Location: $LOCATION"
    echo "  Max Results: $MAX_RESULTS"
    echo ""

    # Check for jq
    check_jq || exit 1

    # Navigate to repo root
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
    cd "$REPO_ROOT" || exit 1

    print_success "Working directory: $REPO_ROOT"

    # Start API
    if ! start_api; then
        print_error "Failed to start API"
        exit 1
    fi

    # Additional wait to ensure full initialization
    sleep 2

    # Run health checks
    test_health

    # Test individual platforms
    test_all_platforms

    # Test aggregated search
    test_aggregated_search

    # Final summary
    print_header "Test Summary"
    print_success "All tests completed"
    echo ""
    echo "API Log: $LOG_FILE"
    echo "To view logs: cat $LOG_FILE | tail -100"
    echo ""

    # Cleanup happens automatically via trap
}

# Run main function
main "$@"
