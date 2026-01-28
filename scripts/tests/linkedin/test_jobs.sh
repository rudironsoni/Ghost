#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
echo "Testing Jobs API at $API_URL"

# 1. Search Jobs
echo "----------------------------------------"
echo "Searching for 'Software Engineer' in 'Madrid'..."
URL="$API_URL/api/linkedin/jobs/search"
if [ ! -z "$STRATEGY" ]; then
  URL="$URL?strategy=$STRATEGY"
fi

SEARCH_RESPONSE=$(curl -s -X POST "$URL" \
  -H "Content-Type: application/json" \
  -d '{ "query": "Software Engineer", "location": "Madrid", "maxResults": 10 }')

echo "Response:"
echo "$SEARCH_RESPONSE" | jq .

# Extract first Job ID (using grep/sed for portability, assuming simple JSON structure)
# In a real script use jq: JOB_ID=$(echo $SEARCH_RESPONSE | jq -r '.[0].id')
JOB_ID=$(echo "$SEARCH_RESPONSE" | grep -o '"id":"[^"]*"' | head -n 1 | cut -d'"' -f4)

if [ -z "$JOB_ID" ]; then
  echo "No jobs found or failed to parse ID."
else
  echo "Found Job ID: $JOB_ID"

  # 2. Get Job Details
  echo "----------------------------------------"
  echo "Fetching details for Job ID: $JOB_ID..."
  curl -s -X GET "$API_URL/api/linkedin/jobs/$JOB_ID" | jq .
  echo ""
fi
echo "----------------------------------------"
