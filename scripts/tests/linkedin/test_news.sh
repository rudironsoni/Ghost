#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
QUERY="Artificial Intelligence"

echo "Testing News API at $API_URL"

# 1. Search News
echo "----------------------------------------"
echo "Searching News for: '$QUERY'"
echo "Expectation: Content search results (posts/articles), not just feed."

curl -s -X POST "$API_URL/api/linkedin/news/search" \
  -H "Content-Type: application/json" \
  -d "{ \"query\": \"$QUERY\", \"maxResults\": 5 }"

echo ""
echo "----------------------------------------"
