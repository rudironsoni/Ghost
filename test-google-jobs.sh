#!/bin/bash

echo "=== Testing Google Jobs Direct Scraping (NO SerpAPI) ==="
echo ""
echo "Starting Ghost WebAPI server..."

# Start the server in background
cd /home/rrj/src/github/rudironsoni/Ghost/src/Ghost.WebApi
dotnet run --configuration Release --no-build > /tmp/ghost-webapi.log 2>&1 &
SERVER_PID=$!

echo "Server PID: $SERVER_PID"
echo "Waiting for server to start (15 seconds)..."
sleep 15

# Check if server is running
if ! ps -p $SERVER_PID > /dev/null 2>&1; then
    echo "ERROR: Server failed to start!"
    echo "Last 50 lines of log:"
    tail -50 /tmp/ghost-webapi.log
    exit 1
fi

echo ""
echo "Server started successfully!"
echo ""
echo "Testing Google Jobs search endpoint..."
echo ""

# Test the jobs endpoint
curl -X POST http://localhost:5000/api/jobs/search \
  -H "Content-Type: application/json" \
  -d '{"query":"software engineer","location":"San Francisco","maxResults":5,"sources":["Google"]}' \
  -s -w "\n\nHTTP Status: %{http_code}\n" | jq '.' 2>/dev/null || cat

echo ""
echo ""
echo "Server log (last 100 lines):"
tail -100 /tmp/ghost-webapi.log

# Cleanup
echo ""
echo "Stopping server (PID: $SERVER_PID)..."
kill $SERVER_PID 2>/dev/null || true
sleep 2
kill -9 $SERVER_PID 2>/dev/null || true

echo ""
echo "=== Test Complete ==="
