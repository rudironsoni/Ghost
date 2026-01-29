#!/bin/bash

#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
echo "Testing Jobs API at $API_URL"

# Strategies to test
STRATEGIES=("GuestApi" "BrowserPage" "Hybrid")

# Check for jq availability
if command -v jq >/dev/null 2>&1; then
  HAS_JQ=1
else
  HAS_JQ=0
  echo "Note: 'jq' not found, falling back to simple grep/sed for parsing."
fi

for STRATEGY in "${STRATEGIES[@]}"; do
  echo "========================================"
  echo "Testing strategy: $STRATEGY"
  echo "Searching for 'Software Engineer' in 'Madrid'..."

  URL="$API_URL/api/linkedin/jobs/search?strategy=$STRATEGY"

  SEARCH_RESPONSE=$(curl -s -X POST "$URL" \
    -H "Content-Type: application/json" \
    -d '{ "query": "Software Engineer", "location": "Madrid", "maxResults": 10 }')

  echo "Response:"
  if [ "$HAS_JQ" -eq 1 ]; then
    echo "$SEARCH_RESPONSE" | jq .
  else
    # Pretty-print best-effort without jq
    echo "$SEARCH_RESPONSE"
  fi

  # Extract first Job ID. Prefer jq when available, fallback to grep/sed.
  if [ "$HAS_JQ" -eq 1 ]; then
    JOB_ID=$(echo "$SEARCH_RESPONSE" | jq -r 'if type=="array" then (.[0].id // empty) else (.results[0].id // .data[0].id // .id // empty) end')
  else
    JOB_ID=$(echo "$SEARCH_RESPONSE" | grep -o '"id":"[^"]*"' | head -n 1 | cut -d'"' -f4)
  fi

  if [ -z "$JOB_ID" ]; then
    echo "No jobs found or failed to parse ID for strategy $STRATEGY."
  else
    echo "Found Job ID: $JOB_ID"
    echo "----------------------------------------"
    echo "Fetching details for Job ID: $JOB_ID..."
    if [ "$HAS_JQ" -eq 1 ]; then
      curl -s -X GET "$API_URL/api/linkedin/jobs/$JOB_ID" | jq .
    else
      curl -s -X GET "$API_URL/api/linkedin/jobs/$JOB_ID"
    fi
    echo ""
  fi

  echo "========================================"
  # Be polite between requests
  sleep 1
done
