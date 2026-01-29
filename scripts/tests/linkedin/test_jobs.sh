#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
echo "Testing Jobs API at $API_URL"

# Strategies to test
# We test all three to ensure parity
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

  # We use maxResults=3 to speed up the test and reduce rate limiting
  URL="$API_URL/api/linkedin/jobs/search?strategy=$STRATEGY"

  SEARCH_RESPONSE=$(curl -s -X POST "$URL" \
    -H "Content-Type: application/json" \
    -d '{ "query": "Software Engineer", "location": "Madrid", "maxResults": 3 }')

  echo "Response (Summary):"
  # Show only the first 500 chars to verify we got content without spamming logs
  echo "$SEARCH_RESPONSE" | head -c 500
  echo "..."
  echo ""

  # Verify we got a JSON array
  if echo "$SEARCH_RESPONSE" | grep -q "\["; then
      COUNT=$(echo "$SEARCH_RESPONSE" | grep -o "\"id\"" | wc -l)
      echo "Found $COUNT jobs via $STRATEGY."
      
      # Basic validation check
      if [ "$COUNT" -eq 0 ]; then
         echo "⚠️ WARNING: $STRATEGY returned 0 jobs."
      else
         echo "✅ SUCCESS: $STRATEGY returned results."
      fi
  else
      echo "❌ FAILURE: $STRATEGY returned invalid response."
  fi

  echo "========================================"
  # Be polite between requests to avoid rate limits
  sleep 2
done
