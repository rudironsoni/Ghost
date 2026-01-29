#!/bin/bash
API_URL="http://localhost:5000"
echo "Testing Hybrid Strategy..."

# Search with Hybrid strategy
RESPONSE=$(curl -s -X POST "$API_URL/api/linkedin/jobs/search?strategy=Hybrid" \
  -H "Content-Type: application/json" \
  -d '{ "query": "Software Engineer", "location": "Madrid", "maxResults": 3 }')

echo "Search Response (Hybrid):"
echo "$RESPONSE" | head -c 2000

# Cleanup check: ensure it's valid JSON array
if echo "$RESPONSE" | grep -q "\[.*\]"; then
    COUNT=$(echo "$RESPONSE" | grep -o "\"id\"" | wc -l)
    echo ""
    echo "Found $COUNT jobs."
else
    echo ""
    echo "Invalid response."
fi
