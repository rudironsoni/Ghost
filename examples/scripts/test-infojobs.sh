#!/bin/bash

# Ghost Web API - InfoJobs Platform Test
# Tests the InfoJobs platform specifically

API_URL="http://localhost:5000"

# Function to make API calls with platform filtering
call_api() {
    local endpoint="$1"
    local data="$2"

    echo "=== Testing InfoJobs Platform ==="
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

# Test InfoJobs-specific searches
call_api "/api/jobs/search" '
{
    "query": "desarrollador",
    "location": "Madrid",
    "maxResults": 5,
    "platforms": ["InfoJobs"]
}'

call_api "/api/jobs/search" '
{
    "query": "ingeniero software",
    "location": "Barcelona",
    "maxResults": 5,
    "platforms": ["InfoJobs"]
}'

call_api "/api/jobs/search" '
{
    "query": "analista",
    "maxResults": 5,
    "platforms": ["InfoJobs"]
}'

# Test with Spanish job titles
call_api "/api/jobs/search" '
{
    "query": "programador java",
    "location": "Valencia",
    "maxResults": 5,
    "platforms": ["InfoJobs"]
}'

call_api "/api/jobs/search" '
{
    "query": "desarrollador web",
    "location": "Sevilla",
    "maxResults": 5,
    "platforms": ["InfoJobs"]
}'

echo "InfoJobs platform testing completed!"
