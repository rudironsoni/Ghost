#!/bin/bash
# Post-test cleanup: Always run after tests, regardless of exit code
set +e

echo "Post-test cleanup..."

# Kill any remaining browser processes
pkill -9 -f "chromium" 2>/dev/null || true
pkill -9 -f "chrome" 2>/dev/null || true
pkill -9 -f "playwright" 2>/dev/null || true

# Kill any dotnet test processes that might be stuck
pkill -9 -f "dotnet test" 2>/dev/null || true

echo "Post-test cleanup complete"
