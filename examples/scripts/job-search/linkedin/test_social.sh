#!/bin/bash

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
PROFILE_ID="williamhgates" # Example public profile

echo "Testing Social API at $API_URL"

# 1. Get Social Profile
echo "----------------------------------------"
echo "Fetching Social Profile for: $PROFILE_ID"
echo "Expectation: 'See more' sections in About/Experience should be expanded."

curl -s -X GET "$API_URL/api/linkedin/social/profile/$PROFILE_ID"

echo ""
echo "----------------------------------------"
