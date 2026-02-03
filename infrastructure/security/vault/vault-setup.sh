#!/bin/bash
# Vault Initialization and Setup Script for Ghost Enterprise
# This script initializes Vault, configures auth methods, secret engines, and policies

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
VAULT_NAMESPACE="${VAULT_NAMESPACE:-vault}"
VAULT_SERVICE="vault"
VAULT_PORT="8200"
VAULT_ADDR="https://${VAULT_SERVICE}.${VAULT_NAMESPACE}.svc.cluster.local:${VAULT_PORT}"

# Files
INIT_OUTPUT="vault-init-output.json"
ROOT_TOKEN_FILE="vault-root-token.txt"
UNSEAL_KEYS_FILE="vault-unseal-keys.txt"

echo -e "${GREEN}=== Ghost Vault Setup Script ===${NC}"
echo "Vault Address: ${VAULT_ADDR}"
echo ""

# Function to check if Vault is initialized
check_vault_status() {
    echo -e "${YELLOW}Checking Vault status...${NC}"
    
    if ! kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault status > /dev/null 2>&1; then
        echo -e "${YELLOW}Vault is sealed or not initialized${NC}"
        return 1
    fi
    
    echo -e "${GREEN}Vault is accessible${NC}"
    return 0
}

# Function to initialize Vault
initialize_vault() {
    echo -e "${YELLOW}Initializing Vault...${NC}"
    
    # Initialize with 5 key shares, 3 required to unseal
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault operator init \
        -key-shares=5 \
        -key-threshold=3 \
        -format=json > "${INIT_OUTPUT}"
    
    # Extract root token
    jq -r '.root_token' "${INIT_OUTPUT}" > "${ROOT_TOKEN_FILE}"
    
    # Extract unseal keys
    jq -r '.unseal_keys_b64[]' "${INIT_OUTPUT}" > "${UNSEAL_KEYS_FILE}"
    
    echo -e "${GREEN}Vault initialized successfully${NC}"
    echo -e "${RED}IMPORTANT: Store ${ROOT_TOKEN_FILE} and ${UNSEAL_KEYS_FILE} in a secure location!${NC}"
}

# Function to unseal Vault
unseal_vault() {
    local pod=$1
    echo -e "${YELLOW}Unsealing vault-${pod}...${NC}"
    
    # Read first 3 unseal keys
    UNSEAL_KEY_1=$(sed -n '1p' "${UNSEAL_KEYS_FILE}")
    UNSEAL_KEY_2=$(sed -n '2p' "${UNSEAL_KEYS_FILE}")
    UNSEAL_KEY_3=$(sed -n '3p' "${UNSEAL_KEYS_FILE}")
    
    kubectl exec -n "${VAULT_NAMESPACE}" "vault-${pod}" -- vault operator unseal "${UNSEAL_KEY_1}"
    kubectl exec -n "${VAULT_NAMESPACE}" "vault-${pod}" -- vault operator unseal "${UNSEAL_KEY_2}"
    kubectl exec -n "${VAULT_NAMESPACE}" "vault-${pod}" -- vault operator unseal "${UNSEAL_KEY_3}"
    
    echo -e "${GREEN}vault-${pod} unsealed${NC}"
}

# Function to setup Kubernetes auth
setup_kubernetes_auth() {
    echo -e "${YELLOW}Setting up Kubernetes authentication...${NC}"
    
    ROOT_TOKEN=$(cat "${ROOT_TOKEN_FILE}")
    
    # Enable Kubernetes auth
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault auth enable kubernetes || true
    
    # Configure Kubernetes auth
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write auth/kubernetes/config \
        kubernetes_host="https://kubernetes.default.svc:443"
    
    # Create role for Ghost application
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write auth/kubernetes/role/ghost \
        bound_service_account_names=ghost \
        bound_service_account_namespaces=ghost-production,ghost-staging \
        policies=ghost-app \
        ttl=24h
    
    echo -e "${GREEN}Kubernetes auth configured${NC}"
}

# Function to setup AppRole auth for CI/CD
setup_approle_auth() {
    echo -e "${YELLOW}Setting up AppRole authentication...${NC}"
    
    # Enable AppRole auth
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault auth enable approle || true
    
    # Create AppRole for Ghost CI/CD
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write auth/approle/role/ghost \
        token_policies="ghost-app" \
        token_ttl=1h \
        token_max_ttl=4h \
        secret_id_ttl=24h
    
    # Get Role ID and Secret ID
    ROLE_ID=$(kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault read -field=role_id auth/approle/role/ghost/role-id)
    SECRET_ID=$(kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write -field=secret_id -f auth/approle/role/ghost/secret-id)
    
    echo "Role ID: ${ROLE_ID}"
    echo "Secret ID: ${SECRET_ID}"
    echo -e "${GREEN}AppRole auth configured${NC}"
    echo -e "${RED}Store Role ID and Secret ID securely for CI/CD${NC}"
}

# Function to enable secret engines
enable_secret_engines() {
    echo -e "${YELLOW}Enabling secret engines...${NC}"
    
    # Enable KV v2 at secret/
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault secrets enable -version=2 -path=secret kv || true
    
    # Enable database secrets engine
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault secrets enable database || true
    
    # Enable transit secrets engine
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault secrets enable transit || true
    
    # Enable PKI secrets engine
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault secrets enable pki || true
    
    # Configure PKI
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault secrets tune -max-lease-ttl=87600h pki
    
    echo -e "${GREEN}Secret engines enabled${NC}"
}

# Function to create transit encryption keys
create_transit_keys() {
    echo -e "${YELLOW}Creating transit encryption keys...${NC}"
    
    # Create key for PII encryption
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write -f transit/keys/ghost-pii \
        type=aes256-gcm96 \
        deletion_allowed=false \
        exportable=false
    
    # Create key for content encryption
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault write -f transit/keys/ghost-content \
        type=aes256-gcm96 \
        deletion_allowed=false \
        exportable=false
    
    echo -e "${GREEN}Transit keys created${NC}"
}

# Function to configure database secrets engine
configure_database_engine() {
    echo -e "${YELLOW}Configuring database secrets engine...${NC}"
    
    # This is a template - replace with actual values
    cat <<EOF
To configure the database secrets engine, run:

kubectl exec -n ${VAULT_NAMESPACE} vault-0 -- vault write database/config/ghost-mysql \\
    plugin_name=mysql-database-plugin \\
    connection_url="{{username}}:{{password}}@tcp(mysql.ghost.svc.cluster.local:3306)/" \\
    allowed_roles="ghost-app,ghost-readonly" \\
    username="vault-admin" \\
    password="REPLACE_WITH_ACTUAL_PASSWORD"

kubectl exec -n ${VAULT_NAMESPACE} vault-0 -- vault write database/roles/ghost-app \\
    db_name=ghost-mysql \\
    creation_statements="CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT SELECT, INSERT, UPDATE, DELETE ON ghost.* TO '{{name}}'@'%';" \\
    default_ttl="1h" \\
    max_ttl="24h"

kubectl exec -n ${VAULT_NAMESPACE} vault-0 -- vault write database/roles/ghost-readonly \\
    db_name=ghost-mysql \\
    creation_statements="CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT SELECT ON ghost.* TO '{{name}}'@'%';" \\
    default_ttl="1h" \\
    max_ttl="24h"
EOF
    
    echo -e "${YELLOW}Database configuration template displayed above${NC}"
}

# Function to create and apply policies
apply_policies() {
    echo -e "${YELLOW}Applying Vault policies...${NC}"
    
    # Create Ghost app policy
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault policy write ghost-app - <<EOF
$(cat vault-policy-ghost.hcl)
EOF
    
    echo -e "${GREEN}Policies applied${NC}"
}

# Function to create sample secrets
create_sample_secrets() {
    echo -e "${YELLOW}Creating sample secrets structure...${NC}"
    
    # Create secret paths (without actual secrets)
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault kv put secret/ghost/database url="REPLACE_ME"
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault kv put secret/ghost/smtp host="REPLACE_ME" port="587" user="REPLACE_ME"
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault kv put secret/ghost/storage type="s3" bucket="REPLACE_ME"
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault kv put secret/ghost/email api_key="REPLACE_ME"
    
    echo -e "${GREEN}Sample secret structure created${NC}"
    echo -e "${YELLOW}Replace placeholder values with actual secrets${NC}"
}

# Function to enable audit logging
enable_audit_logging() {
    echo -e "${YELLOW}Enabling audit logging...${NC}"
    
    kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault audit enable file \
        file_path=/vault/audit/vault-audit.log
    
    echo -e "${GREEN}Audit logging enabled${NC}"
}

# Main execution
main() {
    echo -e "${GREEN}Starting Vault setup...${NC}"
    
    # Check if already initialized
    if [ ! -f "${INIT_OUTPUT}" ]; then
        initialize_vault
        
        # Unseal all Vault pods
        for i in 0 1 2; do
            unseal_vault $i
        done
        
        # Wait for Vault to be ready
        echo -e "${YELLOW}Waiting for Vault to be ready...${NC}"
        sleep 10
        
        # Login with root token
        ROOT_TOKEN=$(cat "${ROOT_TOKEN_FILE}")
        kubectl exec -n "${VAULT_NAMESPACE}" vault-0 -- vault login "${ROOT_TOKEN}"
        
        # Configure Vault
        enable_secret_engines
        create_transit_keys
        apply_policies
        setup_kubernetes_auth
        setup_approle_auth
        configure_database_engine
        create_sample_secrets
        enable_audit_logging
        
        echo -e "${GREEN}=== Vault setup complete! ===${NC}"
        echo -e "${RED}SECURITY REMINDER:${NC}"
        echo -e "1. Store ${ROOT_TOKEN_FILE} in a secure location"
        echo -e "2. Store ${UNSEAL_KEYS_FILE} in separate secure locations"
        echo -e "3. Update database configuration with actual credentials"
        echo -e "4. Replace sample secret values with real secrets"
        echo -e "5. Revoke root token after setup: vault token revoke <root-token>"
    else
        echo -e "${YELLOW}Vault appears to be already initialized${NC}"
        echo -e "${YELLOW}If you need to reconfigure, delete ${INIT_OUTPUT} and run again${NC}"
    fi
}

# Run main function
main
