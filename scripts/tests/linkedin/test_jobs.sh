#!/bin/bash

# Configuration
API_URL="http://localhost:5000"
echo "Testing Jobs API at $API_URL"

# 1. Search Jobs
echo "----------------------------------------"
echo "Searching for 'Software Engineer' in 'Remote'..."
SEARCH_RESPONSE=$(curl -s -X POST "$API_URL/api/linkedin/jobs/search" \
  -H "Content-Type: application/json" \
  -d '{ "query": "Software Engineer", "location": "Remote", "maxResults": 3 }')

echo "Response:"
echo "$SEARCH_RESPONSE" | head -n 20

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
  curl -s -X GET "$API_URL/api/linkedin/jobs/$JOB_ID"
  echo ""
fi
echo "----------------------------------------"
