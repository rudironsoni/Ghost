#!/bin/bash
# Ghost Platform - Infrastructure Smoke Tests
# Simple bash-based validation for Docker Compose infrastructure
# Tech Stack: Bash (not Python!) - This is a .NET project

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_FILE="${PROJECT_DIR}/docker/docker-compose.yml"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

TESTS_PASSED=0
TESTS_FAILED=0

pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((TESTS_PASSED++)) || true
}

fail() {
    echo -e "${RED}✗${NC} $1"
    ((TESTS_FAILED++)) || true
}

warn() {
    echo -e "${YELLOW}⚠${NC} $1"
}

echo "=== Ghost Platform Infrastructure Smoke Tests ==="
echo ""

# Test 1: Docker Compose file exists
echo "Test 1: Docker Compose file exists"
if [ -f "$COMPOSE_FILE" ]; then
    pass "docker-compose.yml found"
else
    fail "docker-compose.yml not found at $COMPOSE_FILE"
fi

# Test 2: YAML is valid
echo "Test 2: Docker Compose YAML is valid"
if docker-compose -f "$COMPOSE_FILE" config > /dev/null 2>&1; then
    pass "YAML syntax is valid"
else
    fail "YAML syntax error"
fi

# Test 3: Required services are defined
echo "Test 3: Required services are defined"
REQUIRED_SERVICES=("postgres" "redis" "rabbitmq" "ghost-webapi" "nginx")
for service in "${REQUIRED_SERVICES[@]}"; do
    if docker-compose -f "$COMPOSE_FILE" config | grep -q "${service}:"; then
        pass "Service '$service' is defined"
    else
        fail "Service '$service' is NOT defined"
    fi
done

# Test 4: Environment file example exists
echo "Test 4: Environment file example exists"
if [ -f "${PROJECT_DIR}/docker/.env.example" ]; then
    pass ".env.example exists"
else
    fail ".env.example missing"
fi

echo ""

# Test 5: Scripts are executable
echo "Test 5: Infrastructure scripts are executable"
SCRIPTS=("backup.sh" "restore.sh" "health-check.sh")
for script in "${SCRIPTS[@]}"; do
    SCRIPT_PATH="${PROJECT_DIR}/scripts/${script}"
    if [ -x "$SCRIPT_PATH" ]; then
        pass "Script '$script' is executable"
    else
        fail "Script '$script' is NOT executable (run: chmod +x $SCRIPT_PATH)"
    fi
done

echo ""

# Test 6: Check no Python files exist
echo "Test 6: Tech stack compliance (no Python files)"
PYTHON_COUNT=$(find "${PROJECT_DIR}" -name "*.py" -type f 2>/dev/null | wc -l)
if [ $PYTHON_COUNT -eq 0 ]; then
    pass "No Python files found (.NET project standards)"
else
    fail "Found $PYTHON_COUNT Python files (violates .NET standards)"
fi

echo ""

# Test 7: Documentation exists
echo "Test 7: Documentation files exist"
DOC_FILES=("README.md" "docs/DEPLOYMENT.md" "docs/OPERATIONS.md")
for doc in "${DOC_FILES[@]}"; do
    if [ -f "${PROJECT_DIR}/${doc}" ]; then
        pass "Document '$doc' exists"
    else
        fail "Document '$doc' missing"
    fi
done

echo ""

# Test 8: Terraform files exist
echo "Test 8: Terraform configuration exists"
if [ -f "${PROJECT_DIR}/terraform/hetzner/main.tf" ]; then
    pass "Terraform Hetzner module exists"
else
    fail "Terraform Hetzner module missing"
fi

echo ""

# Test 9: Ansible files exist
echo "Test 9: Ansible configuration exists"
if [ -f "${PROJECT_DIR}/ansible/setup.yml" ]; then
    pass "Ansible setup.yml exists"
else
    fail "Ansible setup.yml missing"
fi

echo ""

# Summary
echo "=============================================="
echo "Tests Passed: $TESTS_PASSED"
echo "Tests Failed: $TESTS_FAILED"
echo "=============================================="

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}All smoke tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed!${NC}"
    exit 1
fi
    pass ".env.example exists"
else
    fail ".env.example missing"
fi

# Test 5: Scripts are executable
echo "Test 5: Infrastructure scripts are executable"
SCRIPTS=("backup.sh" "restore.sh" "health-check.sh")
for script in "${SCRIPTS[@]}"; do
    SCRIPT_PATH="${PROJECT_DIR}/scripts/${script}"
    if [ -x "$SCRIPT_PATH" ]; then
        pass "Script '$script' is executable"
    else
        fail "Script '$script' is NOT executable"
    fi
done

# Summary
echo ""
echo "=============================================="
echo "Tests Passed: $TESTS_PASSED"
echo "Tests Failed: $TESTS_FAILED"
echo "=============================================="

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}All smoke tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed!${NC}"
    exit 1
fi