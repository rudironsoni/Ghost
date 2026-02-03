# Makefile for Ghost Blog Production Infrastructure

.PHONY: help init plan apply destroy validate fmt clean docs cost output

# Default target
.DEFAULT_GOAL := help

# Variables
ENVIRONMENT := production
PLAN_FILE := $(ENVIRONMENT).tfplan
AWS_REGION := us-east-1

# Colors for output
BLUE := \033[0;34m
GREEN := \033[0;32m
RED := \033[0;31m
NC := \033[0m # No Color

help: ## Show this help message
	@echo "$(BLUE)Ghost Blog Production Infrastructure$(NC)"
	@echo ""
	@echo "$(GREEN)Available targets:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(BLUE)%-20s$(NC) %s\n", $$1, $$2}'
	@echo ""

init: ## Initialize Terraform
	@echo "$(GREEN)Initializing Terraform...$(NC)"
	terraform init -upgrade

validate: ## Validate Terraform configuration
	@echo "$(GREEN)Validating Terraform configuration...$(NC)"
	terraform validate
	@echo "$(GREEN)Checking formatting...$(NC)"
	terraform fmt -check -recursive

fmt: ## Format Terraform files
	@echo "$(GREEN)Formatting Terraform files...$(NC)"
	terraform fmt -recursive

plan: validate ## Create Terraform execution plan
	@echo "$(GREEN)Creating execution plan...$(NC)"
	terraform plan -out=$(PLAN_FILE)
	@echo "$(BLUE)Review the plan above before applying!$(NC)"

apply: ## Apply Terraform changes (requires plan)
	@if [ ! -f $(PLAN_FILE) ]; then \
		echo "$(RED)Error: Plan file not found. Run 'make plan' first.$(NC)"; \
		exit 1; \
	fi
	@echo "$(GREEN)Applying Terraform changes...$(NC)"
	terraform apply $(PLAN_FILE)
	@rm -f $(PLAN_FILE)
	@echo "$(GREEN)✓ Deployment complete!$(NC)"

apply-auto-approve: validate ## Apply Terraform changes without plan (dangerous!)
	@echo "$(RED)WARNING: This will apply changes without review!$(NC)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		terraform apply -auto-approve; \
	fi

destroy-plan: ## Create destruction plan
	@echo "$(RED)Creating destruction plan...$(NC)"
	terraform plan -destroy -out=$(PLAN_FILE)

destroy: ## Destroy infrastructure (requires confirmation)
	@echo "$(RED)WARNING: This will destroy ALL infrastructure!$(NC)"
	@echo "$(RED)This includes databases and all data!$(NC)"
	@read -p "Type 'destroy-production' to confirm: " confirmation; \
	if [ "$$confirmation" = "destroy-production" ]; then \
		terraform destroy; \
	else \
		echo "$(GREEN)Destruction cancelled.$(NC)"; \
	fi

output: ## Show Terraform outputs
	@echo "$(GREEN)Terraform Outputs:$(NC)"
	@terraform output

output-json: ## Show Terraform outputs in JSON format
	@terraform output -json

show: ## Show current Terraform state
	@terraform show

refresh: ## Refresh Terraform state
	@echo "$(GREEN)Refreshing Terraform state...$(NC)"
	terraform refresh

state-list: ## List resources in Terraform state
	@terraform state list

state-show: ## Show a specific resource (usage: make state-show RESOURCE=module.vpc)
	@if [ -z "$(RESOURCE)" ]; then \
		echo "$(RED)Error: RESOURCE variable required$(NC)"; \
		echo "Usage: make state-show RESOURCE=module.vpc"; \
		exit 1; \
	fi
	@terraform state show $(RESOURCE)

import: ## Import existing resource (usage: make import RESOURCE=module.vpc ADDRESS=vpc-xxx)
	@if [ -z "$(RESOURCE)" ] || [ -z "$(ADDRESS)" ]; then \
		echo "$(RED)Error: RESOURCE and ADDRESS variables required$(NC)"; \
		echo "Usage: make import RESOURCE=module.vpc.aws_vpc.main ADDRESS=vpc-xxx"; \
		exit 1; \
	fi
	@terraform import $(RESOURCE) $(ADDRESS)

graph: ## Generate dependency graph
	@echo "$(GREEN)Generating dependency graph...$(NC)"
	@terraform graph | dot -Tpng > graph.png
	@echo "$(GREEN)Graph saved to graph.png$(NC)"

cost: ## Estimate infrastructure costs (requires infracost)
	@echo "$(GREEN)Estimating costs...$(NC)"
	@if command -v infracost >/dev/null 2>&1; then \
		infracost breakdown --path .; \
	else \
		echo "$(RED)Error: infracost not installed$(NC)"; \
		echo "Install: https://www.infracost.io/docs/"; \
	fi

security-scan: ## Run security scan with tfsec
	@echo "$(GREEN)Running security scan...$(NC)"
	@if command -v tfsec >/dev/null 2>&1; then \
		tfsec .; \
	else \
		echo "$(RED)Error: tfsec not installed$(NC)"; \
		echo "Install: brew install tfsec"; \
	fi

compliance-scan: ## Run compliance scan with checkov
	@echo "$(GREEN)Running compliance scan...$(NC)"
	@if command -v checkov >/dev/null 2>&1; then \
		checkov -d . --framework terraform; \
	else \
		echo "$(RED)Error: checkov not installed$(NC)"; \
		echo "Install: pip install checkov"; \
	fi

docs: ## Generate Terraform documentation
	@echo "$(GREEN)Generating documentation...$(NC)"
	@if command -v terraform-docs >/dev/null 2>&1; then \
		terraform-docs markdown table . > TERRAFORM_DOCS.md; \
		echo "$(GREEN)Documentation saved to TERRAFORM_DOCS.md$(NC)"; \
	else \
		echo "$(RED)Error: terraform-docs not installed$(NC)"; \
		echo "Install: brew install terraform-docs"; \
	fi

clean: ## Clean temporary files
	@echo "$(GREEN)Cleaning temporary files...$(NC)"
	@rm -f $(PLAN_FILE)
	@rm -f graph.png
	@rm -f errored.tfstate
	@echo "$(GREEN)✓ Clean complete$(NC)"

lock: ## Update provider lock file
	@echo "$(GREEN)Updating provider lock file...$(NC)"
	terraform providers lock \
		-platform=linux_amd64 \
		-platform=darwin_amd64 \
		-platform=darwin_arm64 \
		-platform=windows_amd64

unlock: ## Force unlock state (usage: make unlock LOCK_ID=xxx)
	@if [ -z "$(LOCK_ID)" ]; then \
		echo "$(RED)Error: LOCK_ID variable required$(NC)"; \
		echo "Usage: make unlock LOCK_ID=xxxxx"; \
		exit 1; \
	fi
	terraform force-unlock $(LOCK_ID)

configure-kubectl: ## Configure kubectl for EKS cluster
	@echo "$(GREEN)Configuring kubectl...$(NC)"
	@CLUSTER_NAME=$$(terraform output -raw eks_cluster_id 2>/dev/null); \
	if [ -n "$$CLUSTER_NAME" ]; then \
		aws eks update-kubeconfig --region $(AWS_REGION) --name $$CLUSTER_NAME; \
		echo "$(GREEN)✓ kubectl configured$(NC)"; \
	else \
		echo "$(RED)Error: EKS cluster not found$(NC)"; \
	fi

test-connectivity: configure-kubectl ## Test connectivity to deployed resources
	@echo "$(GREEN)Testing connectivity...$(NC)"
	@echo "1. Kubernetes Nodes:"
	@kubectl get nodes
	@echo ""
	@echo "2. Kubernetes Pods:"
	@kubectl get pods --all-namespaces
	@echo ""
	@echo "$(GREEN)✓ Connectivity test complete$(NC)"

backup-state: ## Backup Terraform state
	@echo "$(GREEN)Backing up Terraform state...$(NC)"
	@mkdir -p backups
	@terraform state pull > backups/terraform.tfstate.$$(date +%Y%m%d-%H%M%S).backup
	@echo "$(GREEN)✓ State backed up to backups/$(NC)"

check-drift: ## Check for configuration drift
	@echo "$(GREEN)Checking for configuration drift...$(NC)"
	@terraform plan -detailed-exitcode || ( \
		if [ $$? -eq 2 ]; then \
			echo "$(RED)⚠ Configuration drift detected!$(NC)"; \
		fi \
	)

version: ## Show tool versions
	@echo "$(BLUE)Tool Versions:$(NC)"
	@echo "Terraform: $$(terraform version -json | jq -r '.terraform_version')"
	@echo "AWS CLI: $$(aws --version)"
	@echo "kubectl: $$(kubectl version --client --short 2>/dev/null || echo 'Not installed')"

pre-commit: fmt validate security-scan ## Run pre-commit checks
	@echo "$(GREEN)✓ Pre-commit checks passed$(NC)"

deploy: init plan apply configure-kubectl test-connectivity ## Full deployment workflow
	@echo "$(GREEN)✓ Full deployment complete!$(NC)"
	@echo ""
	@echo "$(BLUE)Next steps:$(NC)"
	@echo "1. Configure DNS to point to ALB"
	@echo "2. Deploy Ghost application"
	@echo "3. Set up monitoring dashboards"
	@echo "4. Configure backups"

ci: init validate fmt cost security-scan ## CI pipeline checks
	@echo "$(GREEN)✓ CI checks passed$(NC)"
