#!/bin/bash
echo "Searching LinkedIn for 'Backend Engineer' in Madrid (limit 5)"
curl -s -X POST \
  -H "Content-Type: application/json" \
  -d '{"query": "Backend Engineer", "location": "Madrid", "limit": 5, "sources": ["LinkedIn"]}' \
  http://localhost:5000/api/jobs/search
