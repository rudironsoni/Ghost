#!/bin/bash
echo "Searching Indeed for 'Software Engineer' in Barcelona (limit 5)"
curl -s -X POST \
  -H "Content-Type: application/json" \
  -d '{"query": "Software Engineer", "location": "Barcelona", "limit": 5, "sources": ["Indeed"]}' \
  http://localhost:5000/api/jobs/search
