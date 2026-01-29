#!/bin/bash

# Define cleanup function
cleanup() {
    echo "Stopping WebApi..."
    if [ ! -z "$PID" ]; then
        kill $PID
    fi
}

# Trap exit/int signals
trap cleanup EXIT INT

# Export Environment Variables
export Ghost__Extensions__Indeed__Enabled=true
export Ghost__Extensions__Indeed__Country=ES
export Ghost__Extensions__Glassdoor__Enabled=true
export Ghost__Extensions__Glassdoor__Country=ES
export Ghost__Extensions__Google__GoogleJobs__Enabled=true
export Ghost__Extensions__Google__GoogleJobs__Country=ES
export Ghost__Extensions__LinkedIn__Enabled=true
export Ghost__Extensions__LinkedIn__Country=ES
export ASPNETCORE_ENVIRONMENT=Development

# Start WebApi
echo "Starting Ghost.WebApi..."
dotnet run --project src/Ghost.WebApi > api.log 2>&1 &
PID=$!

echo "Waiting for API to be ready..."
MAX_RETRIES=30
COUNT=0
while ! curl -s http://localhost:5000/health > /dev/null; do
    sleep 2
    COUNT=$((COUNT+1))
    if [ $COUNT -ge $MAX_RETRIES ]; then
        echo "API failed to start. Check api.log:"
        cat api.log
        exit 1
    fi
    # Check if process is still running
    if ! ps -p $PID > /dev/null; then
        echo "API process died. Check api.log:"
        cat api.log
        exit 1
    fi
done

echo "API is ready!"

# Run Search Scripts
echo "Running Search Scripts..."

echo ">>> LINKEDIN <<<"
./tools/scripts/search_linkedin.sh

echo ">>> INDEED <<<"
./tools/scripts/search_indeed.sh

echo ">>> GOOGLE <<<"
./tools/scripts/search_google.sh

echo ">>> GLASSDOOR <<<"
./tools/scripts/search_glassdoor.sh

echo ">>> AGGREGATED (ALL) <<<"
./tools/scripts/search_all.sh

echo "Tests complete."
