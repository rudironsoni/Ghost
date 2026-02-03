#!/bin/bash
# Ghost Platform - Infrastructure Validation Script
# Validates all infrastructure components are present and configured correctly

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

ERRORS=0
WARNINGS=0

echo "=============================================="
echo "Ghost Platform Infrastructure Validation"
echo "=============================================="
echo ""

# Test 1: Directory Structure
echo -e "${BLUE}Checking directory structure...${NC}"
REQUIRED_DIRS=(
    "docker"
    "terraform"
    "ansible"
    "scripts"
    "docs"
)

for dir in "${REQUIRED_DIRS[@]}"; do
    if [ -d "${SCRIPT_DIR}/${dir}" ]; then
        echo -e "${GREEN}✓${NC} Directory: $dir"
    else
        echo -e "${RED}✗${NC} Missing directory: $dir"
        ((ERRORS++))
    fi
done
echo ""

# Test 2: Docker Compose Files
echo -e "${BLUE}Checking Docker Compose configuration...${NC}"
REQUIRED_DOCKER_FILES=(
    "docker/docker-compose.yml"
    "docker/.env.example"
    "docker/nginx/nginx.conf"
    "docker/rabbitmq/rabbitmq.conf"
    "docker/rabbitmq/definitions.json"
    "docker/init-scripts/01-init-db.sql"
)

for file in "${REQUIRED_DOCKER_FILES[@]}"; do
    if [ -f "${SCRIPT_DIR}/${file}" ]; then
        echo -e "${GREEN}✓${NC} File: $file"
    else
        echo -e "${RED}✗${NC} Missing file: $file"
        ((ERRORS++))
    fi
done

# Validate YAML syntax
if [ -f "${SCRIPT_DIR}/docker/docker-compose.yml" ]; then
    if docker-compose -f "${SCRIPT_DIR}/docker/docker-compose.yml" config > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} docker-compose.yml YAML syntax valid"
    else
        echo -e "${RED}✗${NC} docker-compose.yml YAML syntax invalid"
        ((ERRORS++))
    fi
fi
echo ""

# Test 3: Scripts
echo -e "${BLUE}Checking infrastructure scripts...${NC}"
REQUIRED_SCRIPTS=(
    "scripts/backup.sh"
    "scripts/restore.sh"
    "scripts/health-check.sh"
)

for script in "${REQUIRED_SCRIPTS[@]}"; do
    if [ -f "${SCRIPT_DIR}/${script}" ]; then
        if [ -x "${SCRIPT_DIR}/${script}" ]; then
            echo -e "${GREEN}✓${NC} Script executable: $script"
        else
            echo -e "${YELLOW}⚠${NC} Script not executable: $script"
            ((WARNINGS++))
        fi
    else
        echo -e "${RED}✗${NC} Missing script: $script"
        ((ERRORS++))
    fi
done
echo ""

# Test 4: Terraform
echo -e "${BLUE}Checking Terraform configuration...${NC}"
TERRAFORM_FILES=(
    "terraform/main.tf"
    "terraform/variables.tf"
    "terraform/outputs.tf"
    "terraform/versions.tf"
)

for file in "${TERRAFORM_FILES[@]}"; do
    if [ -f "${SCRIPT_DIR}/${file}" ]; then
        echo -e "${GREEN}✓${NC} File: $file"
    else
        echo -e "${YELLOW}⚠${NC} Missing file: $file"
        ((WARNINGS++))
    fi
done

# Validate Terraform syntax if available
if command -v terraform &> /dev/null; then
    cd "${SCRIPT_DIR}/terraform" && terraform validate > /dev/null 2>&1 && \
        echo -e "${GREEN}✓${NC} Terraform syntax valid" || \
        echo -e "${YELLOW}⚠${NC} Terraform validation failed"
fi
echo ""

# Test 5: Ansible
echo -e "${BLUE}Checking Ansible configuration...${NC}"
ANSIBLE_FILES=(
    "ansible/ansible.cfg"
    "ansible/setup.yml"
    "ansible/deploy.yml"
    "ansible/group_vars/all.yml"
)

for file in "${ANSIBLE_FILES[@]}"; do
    if [ -f "${SCRIPT_DIR}/${file}" ]; then
        echo -e "${GREEN}✓${NC} File: $file"
    else
        echo -e "${YELLOW}⚠${NC} Missing file: $file"
        ((WARNINGS++))
    fi
done
echo ""

# Test 6: Monitoring
echo -e "${BLUE}Checking monitoring configuration...${NC}"
MONITORING_FILES=(
    "docker/monitoring/prometheus/prometheus.yml"
    "docker/monitoring/grafana/provisioning/datasources/prometheus.yml"
)

for file in "${MONITORING_FILES[@]}"; do
    if [ -f "${SCRIPT_DIR}/${file}" ]; then
        echo -e "${GREEN}✓${NC} File: $file"
    else
        echo -e "${YELLOW}⚠${NC} Missing file: $file"
        ((WARNINGS++))
    fi
done
echo ""

# Test 7: Documentation
echo -e "${BLUE}Checking documentation...${NC}"
DOC_FILES=(
    "docs/DEPLOYMENT.md"
    "docs/OPERATIONS.md"
    "MIGRATION.md"
)

for file in "${DOC_FILES[@]}"; do
    if [ -f "${SCRIPT_DIR}/${file}" ]; then
        echo -e "${GREEN}✓${NC} Document: $file"
    else
        echo -e "${YELLOW}⚠${NC} Missing document: $file"
        ((WARNINGS++))
    fi
done
echo ""

# Test 8: No Python files (per project standards)
echo -e "${BLUE}Checking for tech stack violations...${NC}"
PYTHON_COUNT=$(find "${SCRIPT_DIR}" -name "*.py" -type f 2>/dev/null | wc -l)
if [ $PYTHON_COUNT -eq 0 ]; then
    echo -e "${GREEN}✓${NC} No Python files found (correct tech stack)"
else
    echo -e "${RED}✗${NC} Found $PYTHON_COUNT Python files (violates .NET project standards)"
    ((ERRORS++))
fi
echo ""

# Summary
echo "=============================================="
echo "Validation Summary"
echo "=============================================="
echo -e "Errors: ${RED}$ERRORS${NC}"
echo -e "Warnings: ${YELLOW}$WARNINGS${NC}"
echo ""

if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}✓ Infrastructure validation PASSED${NC}"
    exit 0
else
    echo -e "${RED}✗ Infrastructure validation FAILED${NC}"
    echo "Please fix the errors above before deployment."
    exit 1
fi