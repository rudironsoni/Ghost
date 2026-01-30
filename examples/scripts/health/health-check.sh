#!/bin/bash

# Ghost Web API - Health Check Script
# Tests API availability and platform status

API_URL="http://localhost:5000"

# Function to check API health
check_health() {
    echo "=== Health Check ==="
    echo "Testing API availability..."
    
    response=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/health")
    
    if [ "$response" -eq 200 ]; then
        echo "✅ API is healthy (HTTP $response)"
        echo ""
        echo "Detailed health status:"
        curl -s "$API_URL/health" | jq .
    else
        echo "❌ API is unavailable (HTTP $response)"
        echo "Make sure the Ghost Web API is running on $API_URL"
    fi
    
    echo ""
    echo "========================================"
    echo ""
}

# Function to check platform status
check_platforms() {
    echo "=== Platform Status Check ==="
    echo "Checking which platforms are available..."
    echo ""
    
    # Test InfoJobs
    echo "InfoJobs Platform:"
    response=$(curl -s -X POST "$API_URL/api/jobs/search" \
        -H "Content-Type: application/json" \
        -d '{"query":"test","maxResults":1,"platforms":["InfoJobs"]}' | jq -r '.platforms[0] // "unknown"')
    
    if [ "$response" = "InfoJobs" ]; then
        echo "✅ InfoJobs platform is available"
    else
        echo "❌ InfoJobs platform may not be configured properly"
    fi
    
    echo ""
    
    # Test Tecnoempleo
    echo "Tecnoempleo Platform:"
    response=$(curl -s -X POST "$API_URL/api/jobs/search" \
        -H "Content-Type: application/json" \
        -d '{"query":"test","maxResults":1,"platforms":["Tecnoempleo"]}' | jq -r '.platforms[0] // "unknown"')
    
    if [ "$response" = "Tecnoempleo" ]; then
        echo "✅ Tecnoempleo platform is available"
    else
        echo "❌ Tecnoempleo platform may not be configured properly"
    fi
    
    echo ""
    echo "========================================"
    echo ""
}

# Function to check configuration
check_config() {
    echo "=== Configuration Check ==="
    echo "Checking environment variables..."
    echo ""
    
    # Check if .env file exists
    if [ -f ".env" ]; then
        echo "✅ .env file found"
        echo ""
        echo "Environment variables set:"
        grep -E "^(INFOJOBS|TECNOEMPLEO)" .env || echo "No Spanish platform credentials found"
    else
        echo "⚠️  No .env file found. Using default configuration."
        echo "   Create .env file from .env.example for production use."
    fi
    
    echo ""
    echo "========================================"
    echo ""
}

# Function to test basic API functionality
test_basic_functionality() {
    echo "=== Basic Functionality Test ==="
    echo "Testing basic job search..."
    echo ""
    
    response=$(curl -s -X POST "$API_URL/api/jobs/search" \
        -H "Content-Type: application/json" \
        -d '{"query":"test","maxResults":1}')
    
    status=$(echo "$response" | jq -r '.status // "unknown"')
    
    if [ "$status" = "success" ]; then
        echo "✅ Basic job search functionality working"
    else
        echo "❌ Basic job search may have issues"
        echo "Response: $response"
    fi
    
    echo ""
    echo "========================================"
    echo ""
}

# Run all checks
check_health
check_platforms
check_config
test_basic_functionality

echo "Health check completed!"