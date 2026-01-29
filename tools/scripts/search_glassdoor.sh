#!/bin/bash
echo "Searching Glassdoor for 'Data Engineer' (Remote) (limit 5)"
curl -s -X POST \
  -H "Content-Type: application/json" \
  -d '{"query": "Data Engineer", "location": "Remote", "limit": 5, "sources": ["Glassdoor"]}' \
  http://localhost:5000/api/jobs/search
