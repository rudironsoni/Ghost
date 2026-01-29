#!/bin/bash

# Define the project path
PROJECT_FILE="src/Ghost.WebApi/Ghost.WebApi.csproj"
PORT=5000

echo "--- Starting Verification ---"

# 1. Build the project to ensure we have the latest binary
echo "Building Ghost.WebApi..."
dotnet build "$PROJECT_FILE" > /dev/null
if [ $? -ne 0 ]; then
    echo "Build failed."
    exit 1
fi

# 2. Find the binary (prefer Debug, then Release)
BINARY=$(find artifacts/bin/Ghost.WebApi -name "Ghost.WebApi" -type f | grep "net9.0" | grep "Debug" | head -n 1)
if [ -z "$BINARY" ]; then
    BINARY=$(find artifacts/bin/Ghost.WebApi -name "Ghost.WebApi" -type f | grep "net9.0" | head -n 1)
fi

if [ -z "$BINARY" ]; then
    echo "Error: Could not find compiled binary."
    exit 1
fi

echo "Using binary: $BINARY"

# 3. Pre-check cleanup
if lsof -i :$PORT > /dev/null; then
    echo "⚠️ Port $PORT is busy. Killing occupant..."
    lsof -t -i :$PORT | xargs kill -9
    sleep 2
fi

# 4. Start Ghost.WebApi in background
echo "Starting Ghost.WebApi..."
./$BINARY --urls=http://localhost:$PORT > ghost.log 2>&1 &
APP_PID=$!
echo "Ghost PID: $APP_PID"

# 5. Wait for startup
echo "Waiting for application to start..."
sleep 5

# 6. Verify Port Binding
if lsof -i :$PORT > /dev/null; then
    echo "✅ SUCCESS: Port $PORT is bound."
else
    echo "❌ FAILURE: Port $PORT is NOT bound. Check ghost.log:"
    cat ghost.log
    kill $APP_PID 2>/dev/null
    exit 1
fi

# 7. Kill the application
echo "Sending SIGTERM to application..."
kill $APP_PID
wait $APP_PID 2>/dev/null

# 8. Wait for cleanup
echo "Waiting for cleanup..."
sleep 3

# 9. Verify Port Release
if lsof -i :$PORT > /dev/null; then
    echo "❌ FAILURE: Port $PORT is STILL bound after shutdown."
    lsof -i :$PORT
else
    echo "✅ SUCCESS: Port $PORT is released."
fi

# 10. Check for orphaned Playwright processes
echo "Checking for orphaned Playwright processes..."
# Filter for playwright processes running from the artifacts directory (our app)
# We exclude 'grep' itself.
ORPHANS=$(ps aux | grep "playwright" | grep "Ghost" | grep -v grep | wc -l)

if [ "$ORPHANS" -eq "0" ]; then
    echo "✅ SUCCESS: No orphaned Playwright processes found."
else
    echo "❌ FAILURE: Found $ORPHANS orphaned Playwright processes."
    ps aux | grep "playwright" | grep "Ghost" | grep -v grep
fi

echo "--- Verification Complete ---"
