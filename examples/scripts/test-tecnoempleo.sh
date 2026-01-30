#!/bin/bash

# Ghost Web API - Tecnoempleo Platform Test
# Tests the Tecnoempleo platform specifically

API_URL="http://localhost:5000"

# Function to make API calls with platform filtering
call_api() {
    local endpoint="$1"
    local data="$2"
    
    echo "=== Testing Tecnoempleo Platform ==="
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

# Test Tecnoempleo-specific searches
call_api "/api/jobs/search" '
{
    "query": "desarrollador",
    "location": "Madrid",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

call_api "/api/jobs/search" '
{
    "query": "ingeniero informático",
    "location": "Barcelona",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

call_api "/api/jobs/search" '
{
    "query": "consultor",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

# Test with Spanish job titles common on Tecnoempleo
call_api "/api/jobs/search" '
{
    "query": "programador .net",
    "location": "Bilbao",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

call_api "/api/jobs/search" '
{
    "query": "analista programador",
    "location": "Zaragoza",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

call_api "/api/jobs/search" '
{
    "query": "técnico sistemas",
    "location": "Málaga",
    "maxResults": 5,
    "platforms": ["Tecnoempleo"]
}'

echo "Tecnoempleo platform testing completed!"