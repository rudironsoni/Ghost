#!/bin/bash
echo "Searching Google for 'DevOps' in Spain (limit 5)"
curl -s -X POST \
  -H "Content-Type: application/json" \
  -d '{"query": "DevOps", "location": "Spain", "limit": 5, "sources": ["Google"]}' \
  http://localhost:5000/api/jobs/search
