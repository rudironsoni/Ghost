#!/bin/bash
echo "Searching all sources for '.NET Developer' in Madrid, Spain (limit 5)"
curl -s -X POST \
  -H "Content-Type: application/json" \
  -d '{"query": ".NET Developer", "location": "Madrid, Spain", "limit": 5}' \
  http://localhost:5000/api/jobs/search
