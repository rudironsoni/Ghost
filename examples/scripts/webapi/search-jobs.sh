#!/bin/bash

# Ghost Web API - Job Search Examples
# This script demonstrates how to search for jobs using curl and jq

API_URL="http://localhost:5000"

# Function to make API calls with pretty JSON output
call_api() {
    local endpoint="$1"
    local data="$2"

    echo "=== Calling: $endpoint ==="
    echo "Request:"
    echo "$data" | jq .
    echo ""

    echo "Response:"
    curl -s -X POST "$API_URL$endpoint" \
        -H "Content-Type: application/json" \
        -d "$data" | jq .
    echo ""
    echo "========================================"
    echo ""
}

# Example 1: Search for developer jobs in Madrid
call_api "/api/jobs/search" '
{
    "query": "desarrollador",
    "location": "Madrid",
    "maxResults": 10
}'

# Example 2: Search for Python jobs in Barcelona
call_api "/api/jobs/search" '
{
    "query": "python",
    "location": "Barcelona",
    "maxResults": 5
}'

# Example 3: Search for remote jobs
call_api "/api/jobs/search" '
{
    "query": "remoto",
    "maxResults": 8
}'

# Example 4: Search for Java developer jobs
call_api "/api/jobs/search" '
{
    "query": "java desarrollador",
    "maxResults": 6
}'

# Example 5: Search with multiple platforms (demonstrates aggregation)
call_api "/api/jobs/search" '
{
    "query": "desarrollador web",
    "location": "España",
    "maxResults": 15,
    "platforms": ["InfoJobs", "Tecnoempleo"]
}'

# Example 6: Search with date range
call_api "/api/jobs/search" '
{
    "query": "backend",
    "location": "Madrid",
    "maxResults": 10,
    "postedAfter": "2024-01-01T00:00:00Z"
}'

# Example 7: Error case - invalid request (missing query)
echo "=== Testing Error Case: Missing Query ==="
echo "Request:"
echo '{"location": "Madrid", "maxResults": 5}' | jq .
echo ""
echo "Response:"
curl -s -X POST "$API_URL/api/jobs/search" \
    -H "Content-Type: application/json" \
    -d '{"location": "Madrid", "maxResults": 5}' | jq .
echo ""
echo "========================================"
echo ""

# Health check
call_api "/health" '{}'