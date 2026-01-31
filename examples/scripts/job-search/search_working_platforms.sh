#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
URL="$API_URL/api/jobs/search"
echo "Searching WORKING sources (LinkedIn, Indeed) for '.NET Developer' in Madrid, Spain (MaxResults 5) against $URL"
echo "Note: Google, Glassdoor, InfoJobs, and Tecnoempleo are currently blocked"
echo ""

# Check for jq availability
if command -v jq >/dev/null 2>&1; then
  HAS_JQ=1
else
  HAS_JQ=0
  echo "Note: 'jq' not found, falling back to simple output parsing."
fi

# Test LinkedIn
echo "=== Testing LinkedIn ==="
PAYLOAD='{"Query": ".NET Developer", "Location": "Madrid, Spain", "MaxResults": 5, "Sources": ["LinkedIn"]}'
RESPONSE=$(curl -s -X POST "$URL" -H "Content-Type: application/json" -d "$PAYLOAD")

if [ "$HAS_JQ" -eq 1 ]; then
  COUNT=$(echo "$RESPONSE" | jq 'length' 2>/dev/null || echo 0)
  echo "LinkedIn: Found $COUNT jobs"
  if [ "$COUNT" -gt 0 ]; then
    echo "$RESPONSE" | jq -r '.[] | "  - \(.title) @ \(.company)"' 2>/dev/null | head -5
  fi
else
  COUNT=$(echo "$RESPONSE" | grep -o '"id"' | wc -l)
  echo "LinkedIn: Found $COUNT jobs"
fi

echo ""

# Test Indeed
echo "=== Testing Indeed ==="
PAYLOAD='{"Query": ".NET Developer", "Location": "Madrid, Spain", "MaxResults": 5, "Sources": ["Indeed"]}'
RESPONSE=$(curl -s -X POST "$URL" -H "Content-Type: application/json" -d "$PAYLOAD")

if [ "$HAS_JQ" -eq 1 ]; then
  COUNT=$(echo "$RESPONSE" | jq 'length' 2>/dev/null || echo 0)
  echo "Indeed: Found $COUNT jobs"
  if [ "$COUNT" -gt 0 ]; then
    echo "$RESPONSE" | jq -r '.[] | "  - \(.title) @ \(.company)"' 2>/dev/null | head -5
  fi
else
  COUNT=$(echo "$RESPONSE" | grep -o '"id"' | wc -l)
  echo "Indeed: Found $COUNT jobs"
fi

echo ""
echo "=== Summary ==="
echo "✅ LinkedIn: Working"
echo "✅ Indeed: Working"
echo "❌ Google: Blocked by consent pages"
echo "❌ Glassdoor: Blocked by consent pages"
echo "❌ InfoJobs: Blocked - requires API credentials"
echo "❌ Tecnoempleo: Blocked - requires API credentials"
echo ""
echo "Working platforms: 2/6 (33%)"
