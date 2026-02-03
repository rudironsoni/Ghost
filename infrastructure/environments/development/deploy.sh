#!/usr/bin/env bash

# ============================================================================
# Ghost Platform - Development Environment Deployment Script
# ============================================================================
# This script automates the deployment of the Ghost Platform development
# environment on AWS.
#
# Usage:
#   ./deploy.sh          - Interactive deployment
#   ./deploy.sh --auto   - Automated deployment (no prompts)
#   ./deploy.sh --plan   - Plan only (no apply)
#   ./deploy.sh --destroy - Destroy infrastructure
# ============================================================================

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT="development"
PROJECT="ghost"

# Parse arguments
AUTO_APPROVE=false
PLAN_ONLY=false
DESTROY=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --auto)
      AUTO_APPROVE=true
      shift
      ;;
    --plan)
      PLAN_ONLY=true
      shift
      ;;
    --destroy)
      DESTROY=true
      shift
      ;;
    -h|--help)
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --auto     Auto-approve (no prompts)"
      echo "  --plan     Plan only (no apply)"
      echo "  --destroy  Destroy infrastructure"
      echo "  -h, --help Show this help message"
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}"
      exit 1
      ;;
  esac
done

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

print_header() {
  echo ""
  echo -e "${BLUE}================================${NC}"
  echo -e "${BLUE}$1${NC}"
  echo -e "${BLUE}================================${NC}"
  echo ""
}

print_success() {
  echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
  echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
  echo -e "${RED}✗ $1${NC}"
}

print_info() {
  echo -e "${BLUE}ℹ $1${NC}"
}

check_dependencies() {
  print_header "Checking Dependencies"
  
  local deps=("terraform" "aws" "jq" "ssh")
  local missing=()
  
  for dep in "${deps[@]}"; do
    if command -v "$dep" &> /dev/null; then
      print_success "$dep is installed"
    else
      print_error "$dep is NOT installed"
      missing+=("$dep")
    fi
  done
  
  if [ ${#missing[@]} -ne 0 ]; then
    echo ""
    print_error "Missing dependencies: ${missing[*]}"
    echo ""
    echo "Please install the missing dependencies:"
    echo "  - Terraform: https://www.terraform.io/downloads"
    echo "  - AWS CLI: https://aws.amazon.com/cli/"
    echo "  - jq: https://stedolan.github.io/jq/"
    exit 1
  fi
  
  # Check AWS credentials
  if aws sts get-caller-identity &> /dev/null; then
    print_success "AWS credentials configured"
    AWS_ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
    AWS_USER=$(aws sts get-caller-identity --query Arn --output text | cut -d'/' -f2)
    print_info "AWS Account: $AWS_ACCOUNT"
    print_info "AWS User: $AWS_USER"
  else
    print_error "AWS credentials not configured"
    echo ""
    echo "Please configure AWS credentials:"
    echo "  aws configure"
    exit 1
  fi
}

terraform_init() {
  print_header "Initializing Terraform"
  
  if [ -d ".terraform" ]; then
    print_info "Terraform already initialized"
  else
    terraform init
    print_success "Terraform initialized"
  fi
}

terraform_plan() {
  print_header "Planning Infrastructure Changes"
  
  terraform plan -out=tfplan
  print_success "Plan generated"
}

terraform_apply() {
  print_header "Applying Infrastructure Changes"
  
  if [ "$AUTO_APPROVE" = true ]; then
    terraform apply -auto-approve tfplan
  else
    echo ""
    echo -e "${YELLOW}Review the plan above. Do you want to apply these changes?${NC}"
    read -p "Type 'yes' to continue: " confirm
    
    if [ "$confirm" != "yes" ]; then
      print_warning "Deployment cancelled"
      exit 0
    fi
    
    terraform apply tfplan
  fi
  
  # Clean up plan file
  rm -f tfplan
  
  print_success "Infrastructure deployed"
}

terraform_destroy() {
  print_header "Destroying Infrastructure"
  
  echo ""
  echo -e "${RED}⚠️  WARNING: This will destroy ALL infrastructure!${NC}"
  echo -e "${RED}   - EC2 instances${NC}"
  echo -e "${RED}   - RDS database (no backup in dev)${NC}"
  echo -e "${RED}   - ElastiCache cluster${NC}"
  echo -e "${RED}   - All networking components${NC}"
  echo ""
  
  if [ "$AUTO_APPROVE" = true ]; then
    print_warning "Auto-destroying in 5 seconds... Press Ctrl+C to cancel"
    sleep 5
    terraform destroy -auto-approve
  else
    read -p "Type 'yes' to DESTROY everything: " confirm
    
    if [ "$confirm" != "yes" ]; then
      print_warning "Destruction cancelled"
      exit 0
    fi
    
    terraform destroy
  fi
  
  print_success "Infrastructure destroyed"
  exit 0
}

save_outputs() {
  print_header "Saving Outputs"
  
  # Save all outputs to JSON
  terraform output -json > outputs.json
  chmod 600 outputs.json
  print_success "Outputs saved to outputs.json"
  
  # Extract key information
  K3S_IP=$(terraform output -raw k3s_public_ip)
  SSH_KEY=$(terraform output -raw ssh_private_key_path | grep -o '[^:]*$' | xargs)
  
  print_info "k3s Public IP: $K3S_IP"
  print_info "SSH Key: $SSH_KEY"
}

wait_for_instance() {
  print_header "Waiting for k3s Instance"
  
  INSTANCE_ID=$(terraform output -raw k3s_instance_id)
  print_info "Instance ID: $INSTANCE_ID"
  
  print_info "Waiting for instance to be running..."
  aws ec2 wait instance-running --instance-ids "$INSTANCE_ID"
  print_success "Instance is running"
  
  print_info "Waiting for status checks..."
  aws ec2 wait instance-status-ok --instance-ids "$INSTANCE_ID"
  print_success "Instance is healthy"
  
  # Wait a bit more for cloud-init to complete
  print_info "Waiting for cloud-init to complete (60 seconds)..."
  sleep 60
  print_success "Instance should be ready"
}

test_connectivity() {
  print_header "Testing Connectivity"
  
  K3S_IP=$(terraform output -raw k3s_public_ip)
  SSH_KEY=$(terraform output -raw ssh_private_key_path | grep -o '[^:]*$' | xargs)
  
  # Test SSH
  print_info "Testing SSH connection..."
  if ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 -i "$SSH_KEY" ec2-user@"$K3S_IP" "echo 'SSH OK'" &> /dev/null; then
    print_success "SSH connection successful"
  else
    print_error "SSH connection failed"
    return 1
  fi
  
  # Test k3s
  print_info "Testing k3s..."
  if ssh -o StrictHostKeyChecking=no -i "$SSH_KEY" ec2-user@"$K3S_IP" "kubectl get nodes" &> /dev/null; then
    print_success "k3s is running"
  else
    print_warning "k3s might still be initializing"
  fi
  
  # Test database
  print_info "Testing database connection..."
  DB_HOST=$(terraform output -raw db_address)
  if ssh -o StrictHostKeyChecking=no -i "$SSH_KEY" ec2-user@"$K3S_IP" "pg_isready -h $DB_HOST -p 5432 -U ghostadmin" &> /dev/null; then
    print_success "Database is reachable"
  else
    print_warning "Database connection failed"
  fi
}

get_kubeconfig() {
  print_header "Getting Kubeconfig"
  
  K3S_IP=$(terraform output -raw k3s_public_ip)
  SSH_KEY=$(terraform output -raw ssh_private_key_path | grep -o '[^:]*$' | xargs)
  
  mkdir -p .kube
  
  print_info "Fetching kubeconfig from k3s node..."
  ssh -o StrictHostKeyChecking=no -i "$SSH_KEY" ec2-user@"$K3S_IP" \
    "sudo cat /etc/rancher/k3s/k3s.yaml" > .kube/config.tmp
  
  # Replace 127.0.0.1 with public IP
  sed "s/127.0.0.1/$K3S_IP/" .kube/config.tmp > kubeconfig.yaml
  rm .kube/config.tmp
  chmod 600 kubeconfig.yaml
  
  print_success "Kubeconfig saved to kubeconfig.yaml"
  echo ""
  print_info "To use kubectl:"
  echo -e "  ${GREEN}export KUBECONFIG=\$(pwd)/kubeconfig.yaml${NC}"
  echo -e "  ${GREEN}kubectl get nodes${NC}"
}

show_next_steps() {
  print_header "Deployment Complete! 🎉"
  
  K3S_IP=$(terraform output -raw k3s_public_ip)
  SSH_KEY=$(terraform output -raw ssh_private_key_path | grep -o '[^:]*$' | xargs)
  
  echo ""
  echo -e "${GREEN}Your Ghost Platform development environment is ready!${NC}"
  echo ""
  echo -e "${YELLOW}Connection Information:${NC}"
  echo -e "  k3s IP:  ${GREEN}$K3S_IP${NC}"
  echo -e "  SSH Key: ${GREEN}$SSH_KEY${NC}"
  echo ""
  echo -e "${YELLOW}Quick Commands:${NC}"
  echo -e "  1. Connect to k3s:"
  echo -e "     ${GREEN}ssh -i $SSH_KEY ec2-user@$K3S_IP${NC}"
  echo ""
  echo -e "  2. Use kubectl locally:"
  echo -e "     ${GREEN}export KUBECONFIG=\$(pwd)/kubeconfig.yaml${NC}"
  echo -e "     ${GREEN}kubectl get nodes${NC}"
  echo ""
  echo -e "  3. View outputs:"
  echo -e "     ${GREEN}terraform output${NC}"
  echo -e "     ${GREEN}cat outputs.json | jq${NC}"
  echo ""
  echo -e "  4. Deploy Ghost Platform:"
  echo -e "     ${GREEN}kubectl apply -k ../../platform/base${NC}"
  echo -e "     ${GREEN}kubectl apply -k ../../platform/services${NC}"
  echo ""
  echo -e "${YELLOW}Cost Optimization:${NC}"
  COST=$(terraform output -json estimated_monthly_cost | jq -r '.value' | head -n 1)
  echo -e "  Estimated: ${GREEN}\$40-60/month${NC}"
  echo -e "  Auto-shutdown: ${GREEN}ENABLED${NC} (saves ~40%)"
  echo ""
  echo -e "${YELLOW}Management:${NC}"
  echo -e "  Start:   ${GREEN}make start${NC}"
  echo -e "  Stop:    ${GREEN}make stop${NC}"
  echo -e "  Destroy: ${GREEN}make destroy${NC}"
  echo ""
  echo -e "${BLUE}Documentation: infrastructure/environments/development/README.md${NC}"
  echo ""
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
  clear
  
  echo ""
  echo -e "${BLUE}╔═══════════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${BLUE}║                                                                   ║${NC}"
  echo -e "${BLUE}║       👻 Ghost Platform - Development Environment Deploy         ║${NC}"
  echo -e "${BLUE}║                                                                   ║${NC}"
  echo -e "${BLUE}╚═══════════════════════════════════════════════════════════════════╝${NC}"
  echo ""
  
  # Handle destroy
  if [ "$DESTROY" = true ]; then
    terraform_destroy
  fi
  
  # Check dependencies
  check_dependencies
  
  # Initialize Terraform
  terraform_init
  
  # Plan changes
  terraform_plan
  
  # Stop here if plan-only
  if [ "$PLAN_ONLY" = true ]; then
    print_info "Plan-only mode. Exiting without apply."
    exit 0
  fi
  
  # Apply changes
  terraform_apply
  
  # Save outputs
  save_outputs
  
  # Wait for instance to be ready
  wait_for_instance
  
  # Test connectivity
  test_connectivity || true  # Don't fail if tests fail
  
  # Get kubeconfig
  get_kubeconfig || print_warning "Could not fetch kubeconfig (try again later)"
  
  # Show next steps
  show_next_steps
}

# Run main function
main "$@"
