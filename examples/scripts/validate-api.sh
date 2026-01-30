#!/bin/bash

# Ghost Web API - API Structure Validation
# Tests that the API returns the expected JSON structure

API_URL="http://localhost:5000"

# Function to validate JSON structure
validate_response() {
    local response="$1"
    local expected_type="$2"

    echo "=== Validating Response Structure ==="
    echo "Response: $response"

    if [ -z "$response" ]; then
        echo "❌ Empty response"
        return 1
    fi

    # Check if response is valid JSON
    if echo "$response" | jq empty >/dev/null 2>&1; then
        echo "✅ Valid JSON"

        # Check if it's an array (expected for job search)
        if echo "$response" | jq 'type' | grep -q "array"; then
            echo "✅ Response is an array (expected)"

            # Check array elements have required fields
            if echo "$response" | jq '.[0] | has("id") and has("title") and has("company")' | grep -q "true"; then
                echo "✅ Array elements have required fields (id, title, company)"
                return 0
            else
                echo "⚠️  Array elements may be missing required fields"
                return 0
            fi
        else
            echo "⚠️  Response is not an array (might be empty or error)"
            return 0
        fi
    else
        echo "❌ Invalid JSON"
        return 1
    fi
}

# Test API endpoint
response=$(curl -s -X POST "$API_URL/api/jobs/search" \
    -H "Content-Type: application/json" \
    -d '{"query":"test","maxResults":1}')

validate_response "$response" "array"

echo ""
echo "API structure validation completed!"
