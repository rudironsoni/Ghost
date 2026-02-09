#!/bin/bash
# Pre-test cleanup: Kill orphaned processes from previous runs
set -e

echo "Cleaning up orphaned test processes..."

# Kill zombie dotnet test processes
pkill -9 -f "dotnet test" 2>/dev/null || true

# Kill orphaned browser processes
pkill -9 -f "chromium" 2>/dev/null || true
pkill -9 -f "chrome" 2>/dev/null || true
pkill -9 -f "playwright" 2>/dev/null || true

# Wait for processes to actually die
sleep 2

# Verify cleanup
ZOMBIES=$(ps aux | grep -E "<defunct>" | grep -E "dotnet|chromium|chrome" | wc -l)
if [ "$ZOMBIES" -gt 0 ]; then
    echo "Warning: $ZOMBIES zombie processes still present"
    # Force kill parent processes of zombies
    ps aux | grep -E "<defunct>" | grep -E "dotnet|chromium|chrome" | awk '{print $3}' | xargs -r kill -9 2>/dev/null || true
fi

echo "Cleanup complete"
