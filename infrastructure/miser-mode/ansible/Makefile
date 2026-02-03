# Ghost Platform Ultra Miser Mode - Ansible Makefile
# Convenience commands for common operations

.PHONY: help install setup deploy update health backup clean

# Default inventory file
INVENTORY ?= inventory/hosts.yml

# Ansible command with common options
ANSIBLE_PLAYBOOK = ansible-playbook -i $(INVENTORY)

help: ## Show this help message
	@echo "Ghost Platform Ultra Miser Mode - Ansible Commands"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-15s %s\n", $$1, $$2}'
	@echo ""
	@echo "Examples:"
	@echo "  make install          # Install Ansible dependencies"
	@echo "  make setup            # Complete server setup"
	@echo "  make deploy           # Deploy Ghost Platform"
	@echo "  make health           # Check system health"
	@echo ""

install: ## Install Ansible and required collections
	@echo "Installing Ansible dependencies..."
	pip install --upgrade ansible
	ansible-galaxy collection install -r requirements.yml
	@echo "✓ Dependencies installed"

check-inventory: ## Verify inventory configuration
	@echo "Checking inventory..."
	@test -f $(INVENTORY) || (echo "ERROR: Inventory file $(INVENTORY) not found!" && exit 1)
	ansible-inventory -i $(INVENTORY) --list
	@echo "✓ Inventory is valid"

ping: ## Test connectivity to servers
	@echo "Testing connectivity..."
	ansible -i $(INVENTORY) ghost_servers -m ping
	@echo "✓ All servers reachable"

setup: check-inventory ## Run full server setup
	@echo "Running server setup..."
	$(ANSIBLE_PLAYBOOK) setup.yml
	@echo "✓ Server setup complete"

setup-check: check-inventory ## Run setup in check mode (dry-run)
	@echo "Running setup check (dry-run)..."
	$(ANSIBLE_PLAYBOOK) setup.yml --check --diff

deploy: check-inventory ## Deploy Ghost Platform
	@echo "Deploying Ghost Platform..."
	$(ANSIBLE_PLAYBOOK) deploy.yml
	@echo "✓ Deployment complete"

deploy-check: check-inventory ## Deploy in check mode (dry-run)
	@echo "Running deployment check (dry-run)..."
	$(ANSIBLE_PLAYBOOK) deploy.yml --check --diff

update: check-inventory ## Update Ghost Platform to latest version
	@echo "Updating Ghost Platform..."
	$(ANSIBLE_PLAYBOOK) update.yml
	@echo "✓ Update complete"

health: check-inventory ## Run health checks
	@echo "Running health checks..."
	$(ANSIBLE_PLAYBOOK) health-check.yml

backup: check-inventory ## Trigger manual backup
	@echo "Running backup..."
	$(ANSIBLE_PLAYBOOK) backup.yml
	@echo "✓ Backup complete"

backup-db: check-inventory ## Backup database only
	@echo "Running database backup..."
	$(ANSIBLE_PLAYBOOK) backup.yml --tags postgres,redis
	@echo "✓ Database backup complete"

setup-common: check-inventory ## Run common role only
	$(ANSIBLE_PLAYBOOK) setup.yml --tags common

setup-docker: check-inventory ## Run docker role only
	$(ANSIBLE_PLAYBOOK) setup.yml --tags docker

setup-security: check-inventory ## Run security role only
	$(ANSIBLE_PLAYBOOK) setup.yml --tags security

setup-monitoring: check-inventory ## Run monitoring role only
	$(ANSIBLE_PLAYBOOK) setup.yml --tags monitoring

setup-backup: check-inventory ## Run backup role only
	$(ANSIBLE_PLAYBOOK) setup.yml --tags backup

logs: ## View Ansible logs (requires target server)
	@echo "Recent Ansible runs:"
	@ls -lt ~/.ansible/tmp/ 2>/dev/null | head -10 || echo "No logs found"

clean: ## Clean temporary files
	@echo "Cleaning temporary files..."
	rm -rf /tmp/ansible_facts/
	rm -rf ~/.ansible/tmp/
	find . -type f -name "*.retry" -delete
	@echo "✓ Cleanup complete"

validate: check-inventory ## Validate playbook syntax
	@echo "Validating playbooks..."
	$(ANSIBLE_PLAYBOOK) setup.yml --syntax-check
	$(ANSIBLE_PLAYBOOK) deploy.yml --syntax-check
	$(ANSIBLE_PLAYBOOK) update.yml --syntax-check
	$(ANSIBLE_PLAYBOOK) health-check.yml --syntax-check
	$(ANSIBLE_PLAYBOOK) backup.yml --syntax-check
	@echo "✓ All playbooks are valid"

inventory-list: check-inventory ## List inventory hosts
	ansible-inventory -i $(INVENTORY) --graph

facts: check-inventory ## Gather and display facts from servers
	ansible -i $(INVENTORY) ghost_servers -m setup

version: ## Display Ansible version
	@ansible --version

.DEFAULT_GOAL := help
