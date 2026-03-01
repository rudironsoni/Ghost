#!/bin/bash
set -e

echo "Starting Redis using Docker Compose..."
docker compose -f "$(dirname "$0")/docker-compose.yml" up -d

echo "Waiting for Redis to be healthy..."
timeout=30
elapsed=0
while [ $elapsed -lt $timeout ]; do
    if docker exec ghost-redis redis-cli ping > /dev/null 2>&1; then
        echo "✅ Redis is ready!"
        echo ""
        echo "Connection details:"
        echo "  Host: localhost"
        echo "  Port: 6379"
        echo ""
        echo "Test connection:"
        echo "  docker exec ghost-redis redis-cli ping"
        exit 0
    fi
    sleep 1
    elapsed=$((elapsed + 1))
done

echo "❌ Redis failed to start within ${timeout} seconds"
exit 1
