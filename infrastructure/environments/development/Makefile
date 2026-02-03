# Ghost Platform Development Environment - Makefile
# Convenience commands for Terraform operations

.PHONY: help init plan apply destroy console output ssh connect clean format validate

# Default target
.DEFAULT_GOAL := help

# Colors
BLUE := \033[0;34m
GREEN := \033[0;32m
YELLOW := \033[0;33m
RED := \033[0;31m
NC := \033[0m # No Color

help: ## Show this help message
	@echo "$(BLUE)Ghost Platform - Development Environment$(NC)"
	@echo ""
	@echo "$(GREEN)Available targets:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(YELLOW)%-20s$(NC) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(GREEN)Quick Start:$(NC)"
	@echo "  1. make init    - Initialize Terraform"
	@echo "  2. make plan    - Review infrastructure plan"
	@echo "  3. make apply   - Deploy infrastructure"
	@echo "  4. make ssh     - Connect to k3s node"
	@echo ""

init: ## Initialize Terraform (run this first)
	@echo "$(BLUE)Initializing Terraform...$(NC)"
	terraform init
	@echo "$(GREEN)✓ Terraform initialized$(NC)"

plan: ## Show Terraform execution plan
	@echo "$(BLUE)Generating Terraform plan...$(NC)"
	terraform plan
	@echo "$(GREEN)✓ Plan generated$(NC)"

apply: ## Apply Terraform configuration (deploy infrastructure)
	@echo "$(BLUE)Applying Terraform configuration...$(NC)"
	terraform apply
	@echo "$(GREEN)✓ Infrastructure deployed$(NC)"
	@echo ""
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. make ssh         - Connect to k3s node"
	@echo "  2. make get-config  - Get kubeconfig"
	@echo "  3. make outputs     - View all outputs"

apply-auto: ## Apply without confirmation (use with caution!)
	@echo "$(BLUE)Auto-applying Terraform configuration...$(NC)"
	terraform apply -auto-approve

destroy: ## Destroy all infrastructure
	@echo "$(RED)⚠️  This will destroy ALL infrastructure!$(NC)"
	@echo "$(YELLOW)Press Ctrl+C to cancel, or wait 5 seconds to continue...$(NC)"
	@sleep 5
	terraform destroy
	@echo "$(GREEN)✓ Infrastructure destroyed$(NC)"

destroy-auto: ## Destroy without confirmation (dangerous!)
	@echo "$(RED)Auto-destroying infrastructure...$(NC)"
	terraform destroy -auto-approve

console: ## Open Terraform console for testing
	@echo "$(BLUE)Opening Terraform console...$(NC)"
	terraform console

output: ## Show all Terraform outputs
	@echo "$(BLUE)Terraform Outputs:$(NC)"
	@terraform output

outputs: output ## Alias for output

output-json: ## Show outputs in JSON format
	@terraform output -json

connection-info: ## Show connection information for all services
	@echo "$(BLUE)Connection Information:$(NC)"
	@terraform output -json connection_info | jq

ssh: ## SSH into the k3s node
	@echo "$(BLUE)Connecting to k3s node...$(NC)"
	@bash -c "$$(terraform output -raw ssh_command)"

connect: ssh ## Alias for ssh

get-ip: ## Get public IP of k3s node
	@terraform output -raw k3s_public_ip

get-config: ## Get kubeconfig and set up local access
	@echo "$(BLUE)Getting kubeconfig...$(NC)"
	@mkdir -p .kube
	@ssh -o StrictHostKeyChecking=no -i .ssh/ghost-development-key.pem ec2-user@$$(terraform output -raw k3s_public_ip) "sudo cat /etc/rancher/k3s/k3s.yaml" > .kube/config.tmp
	@sed "s/127.0.0.1/$$(terraform output -raw k3s_public_ip)/" .kube/config.tmp > kubeconfig.yaml
	@rm .kube/config.tmp
	@chmod 600 kubeconfig.yaml
	@echo "$(GREEN)✓ Kubeconfig saved to kubeconfig.yaml$(NC)"
	@echo ""
	@echo "$(YELLOW)Set environment variable:$(NC)"
	@echo "  export KUBECONFIG=$$(pwd)/kubeconfig.yaml"
	@echo ""
	@echo "$(YELLOW)Test connection:$(NC)"
	@echo "  kubectl get nodes"

kubeconfig: get-config ## Alias for get-config

test-connection: ## Test connectivity to all services
	@echo "$(BLUE)Testing service connectivity...$(NC)"
	@echo ""
	@echo "$(YELLOW)k3s Node:$(NC)"
	@ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 -i .ssh/ghost-development-key.pem ec2-user@$$(terraform output -raw k3s_public_ip) "echo '✓ SSH connection successful'" || echo "$(RED)✗ SSH connection failed$(NC)"
	@echo ""
	@echo "$(YELLOW)Database:$(NC)"
	@ssh -o StrictHostKeyChecking=no -i .ssh/ghost-development-key.pem ec2-user@$$(terraform output -raw k3s_public_ip) "pg_isready -h $$(terraform output -raw db_address) -p 5432 -U ghostadmin && echo '✓ Database connection successful' || echo '$(RED)✗ Database connection failed$(NC)'"
	@echo ""
	@echo "$(YELLOW)Kubernetes:$(NC)"
	@ssh -o StrictHostKeyChecking=no -i .ssh/ghost-development-key.pem ec2-user@$$(terraform output -raw k3s_public_ip) "kubectl get nodes > /dev/null 2>&1 && echo '✓ Kubernetes API responding' || echo '$(RED)✗ Kubernetes API not responding$(NC)'"

health-check: test-connection ## Alias for test-connection

status: ## Show status of deployed infrastructure
	@echo "$(BLUE)Infrastructure Status:$(NC)"
	@echo ""
	@echo "$(YELLOW)k3s Instance:$(NC)"
	@aws ec2 describe-instances --instance-ids $$(terraform output -raw k3s_instance_id) --query 'Reservations[0].Instances[0].State.Name' --output text
	@echo ""
	@echo "$(YELLOW)RDS Database:$(NC)"
	@aws rds describe-db-instances --db-instance-identifier $$(terraform output -raw db_instance_id) --query 'DBInstances[0].DBInstanceStatus' --output text
	@echo ""
	@echo "$(YELLOW)Redis Cluster:$(NC)"
	@aws elasticache describe-cache-clusters --cache-cluster-id $$(terraform output -raw redis_cluster_id) --query 'CacheClusters[0].CacheClusterStatus' --output text

start: ## Start the k3s instance (if auto-shutdown is enabled)
	@echo "$(BLUE)Starting k3s instance...$(NC)"
	@aws ec2 start-instances --instance-ids $$(terraform output -raw k3s_instance_id)
	@echo "$(GREEN)✓ Instance starting...$(NC)"
	@echo "$(YELLOW)Wait a few minutes for services to be ready$(NC)"

stop: ## Stop the k3s instance (cost saving)
	@echo "$(BLUE)Stopping k3s instance...$(NC)"
	@aws ec2 stop-instances --instance-ids $$(terraform output -raw k3s_instance_id)
	@echo "$(GREEN)✓ Instance stopping...$(NC)"

restart: ## Restart the k3s instance
	@echo "$(BLUE)Restarting k3s instance...$(NC)"
	@aws ec2 reboot-instances --instance-ids $$(terraform output -raw k3s_instance_id)
	@echo "$(GREEN)✓ Instance restarting...$(NC)"

costs: ## Show estimated monthly costs
	@echo "$(BLUE)Estimated Monthly Costs:$(NC)"
	@terraform output estimated_monthly_cost

cost: costs ## Alias for costs

logs: ## Show CloudWatch logs for k3s
	@echo "$(BLUE)Fetching CloudWatch logs...$(NC)"
	@aws logs tail /aws/ec2/ghost-development/k3s --follow

getting-started: ## Show getting started instructions
	@terraform output getting_started

guide: getting-started ## Alias for getting-started

format: ## Format Terraform files
	@echo "$(BLUE)Formatting Terraform files...$(NC)"
	terraform fmt -recursive
	@echo "$(GREEN)✓ Files formatted$(NC)"

fmt: format ## Alias for format

validate: ## Validate Terraform configuration
	@echo "$(BLUE)Validating Terraform configuration...$(NC)"
	terraform validate
	@echo "$(GREEN)✓ Configuration valid$(NC)"

clean: ## Clean temporary files and caches
	@echo "$(BLUE)Cleaning temporary files...$(NC)"
	@rm -rf .terraform/
	@rm -f *.tfstate*
	@rm -f .terraform.lock.hcl
	@rm -f kubeconfig.yaml
	@rm -rf .kube/
	@rm -f outputs.json
	@echo "$(GREEN)✓ Cleaned$(NC)"
	@echo "$(YELLOW)Note: Run 'make init' before next terraform operation$(NC)"

refresh: ## Refresh Terraform state
	@echo "$(BLUE)Refreshing Terraform state...$(NC)"
	terraform refresh

state-list: ## List all resources in Terraform state
	@terraform state list

state-show: ## Show a specific resource (usage: make state-show RESOURCE=aws_instance.k3s)
	@terraform state show $(RESOURCE)

graph: ## Generate dependency graph (requires graphviz)
	@echo "$(BLUE)Generating dependency graph...$(NC)"
	@terraform graph | dot -Tpng > graph.png
	@echo "$(GREEN)✓ Graph saved to graph.png$(NC)"

upgrade: ## Upgrade Terraform providers
	@echo "$(BLUE)Upgrading providers...$(NC)"
	terraform init -upgrade
	@echo "$(GREEN)✓ Providers upgraded$(NC)"

backup-state: ## Backup Terraform state
	@echo "$(BLUE)Backing up Terraform state...$(NC)"
	@mkdir -p backups
	@cp terraform.tfstate backups/terraform.tfstate.$$(date +%Y%m%d_%H%M%S)
	@echo "$(GREEN)✓ State backed up$(NC)"

deploy-platform: ## Deploy Ghost platform to k3s (after infrastructure is ready)
	@echo "$(BLUE)Deploying Ghost platform...$(NC)"
	@export KUBECONFIG=$$(pwd)/kubeconfig.yaml && \
		kubectl apply -k ../../platform/base && \
		kubectl apply -k ../../platform/services
	@echo "$(GREEN)✓ Platform deployed$(NC)"

check-deps: ## Check if required tools are installed
	@echo "$(BLUE)Checking dependencies...$(NC)"
	@command -v terraform >/dev/null 2>&1 && echo "$(GREEN)✓ terraform$(NC)" || echo "$(RED)✗ terraform not found$(NC)"
	@command -v aws >/dev/null 2>&1 && echo "$(GREEN)✓ aws cli$(NC)" || echo "$(RED)✗ aws cli not found$(NC)"
	@command -v kubectl >/dev/null 2>&1 && echo "$(GREEN)✓ kubectl$(NC)" || echo "$(RED)✗ kubectl not found$(NC)"
	@command -v jq >/dev/null 2>&1 && echo "$(GREEN)✓ jq$(NC)" || echo "$(RED)✗ jq not found$(NC)"
	@command -v ssh >/dev/null 2>&1 && echo "$(GREEN)✓ ssh$(NC)" || echo "$(RED)✗ ssh not found$(NC)"

.PHONY: all
all: check-deps init plan ## Check dependencies, initialize, and plan
