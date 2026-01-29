#!/bin/bash
API_URL="http://localhost:5000"
echo "Testing BrowserPage Strategy..."

# Search with BrowserPage strategy
RESPONSE=$(curl -s -X POST "$API_URL/api/linkedin/jobs/search?strategy=BrowserPage" \
  -H "Content-Type: application/json" \
  -d '{ "query": "Software Engineer", "location": "Madrid", "maxResults": 1 }')

echo "Search Response:"
echo "$RESPONSE" | head -c 1000 # Show first 1000 chars

# Extract ID (using simple grep if jq not installed, but assuming jq for robustness if available)
if command -v jq >/dev/null 2>&1; then
  JOB_ID=$(echo "$RESPONSE" | jq -r '.[0].id // empty')
else
  JOB_ID=$(echo "$RESPONSE" | grep -o '"id":"[^"]*"' | head -n 1 | cut -d'"' -f4)
fi

if [ -z "$JOB_ID" ]; then
  echo "No job found."
  exit 1
fi

echo "Fetching details for Job ID: $JOB_ID with BrowserPage..."
DETAILS=$(curl -s -X GET "$API_URL/api/linkedin/jobs/$JOB_ID?strategy=BrowserPage")

echo "Details:"
echo "$DETAILS"
