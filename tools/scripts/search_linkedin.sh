#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
URL="$API_URL/api/jobs/search"
echo "Searching LinkedIn for 'Backend Engineer' in Madrid (MaxResults 5) against $URL"

# Check for jq availability
if command -v jq >/dev/null 2>&1; then
  HAS_JQ=1
else
  HAS_JQ=0
  echo "Note: 'jq' not found, falling back to simple output parsing."
fi

STRATEGIES=("GuestApi" "BrowserPage" "Hybrid")

for STRATEGY in "${STRATEGIES[@]}"; do
  echo "=== Testing Strategy: $STRATEGY ==="

  PAYLOAD="{\"Query\": \"Backend Engineer\", \"Location\": \"Madrid\", \"MaxResults\": 5, \"Sources\": [\"LinkedIn\"], \"Strategy\": \"$STRATEGY\"}"

  RESPONSE=$(curl -s -X POST "$URL" -H "Content-Type: application/json" -d "$PAYLOAD")

  echo "Response (summary):"
  if [ "$HAS_JQ" -eq 1 ]; then
    echo "$RESPONSE" | jq -r '.[] | "[\(.source // \"Unknown\")] \(.title) @ \(.company) - \(.location) (\(.salaryRaw // \"N/A\"))"' || echo "$RESPONSE" | head -c 500
  else
    echo "$RESPONSE" | head -c 500
  fi
  echo "..."

  # Validate JSON looks like an array
  if echo "$RESPONSE" | grep -q "\["; then
    if [ "$HAS_JQ" -eq 1 ]; then
      COUNT=$(echo "$RESPONSE" | jq 'length' 2>/dev/null || echo 0)
    else
      COUNT=$(echo "$RESPONSE" | grep -o '"id"' | wc -l)
    fi

    echo "Found $COUNT jobs from LinkedIn."
    if [ "$COUNT" -eq 0 ]; then
      echo "⚠️ WARNING: returned 0 jobs."
    else
      echo "✅ SUCCESS: returned results."
    fi
  else
    echo "❌ FAILURE: invalid or non-array response received."
  fi

  sleep 2
done
