#!/bin/bash
set -e

echo "Stopping Redis..."
docker compose -f "$(dirname "$0")/docker-compose.yml" down

echo "✅ Redis stopped"
