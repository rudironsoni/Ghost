# Vault Policy for Ghost Application
# This policy grants Ghost app the necessary permissions to read secrets

# KV v2 secrets engine - Ghost application secrets
path "secret/data/ghost/*" {
  capabilities = ["read", "list"]
}

path "secret/metadata/ghost/*" {
  capabilities = ["list"]
}

# Database dynamic credentials - MySQL
path "database/creds/ghost-app" {
  capabilities = ["read"]
}

# Database static credentials - readonly user
path "database/static-creds/ghost-readonly" {
  capabilities = ["read"]
}

# Transit encryption for PII data
path "transit/encrypt/ghost-pii" {
  capabilities = ["update"]
}

path "transit/decrypt/ghost-pii" {
  capabilities = ["update"]
}

# Transit for content encryption
path "transit/encrypt/ghost-content" {
  capabilities = ["update"]
}

path "transit/decrypt/ghost-content" {
  capabilities = ["update"]
}

# PKI for internal certificates
path "pki/issue/ghost-internal" {
  capabilities = ["create", "update"]
}

# SMTP credentials
path "secret/data/ghost/smtp" {
  capabilities = ["read"]
}

# Storage credentials (S3/GCS)
path "secret/data/ghost/storage" {
  capabilities = ["read"]
}

# Email service credentials
path "secret/data/ghost/email" {
  capabilities = ["read"]
}

# Payment gateway credentials
path "secret/data/ghost/payments" {
  capabilities = ["read"]
}

# OAuth/SSO credentials
path "secret/data/ghost/oauth" {
  capabilities = ["read"]
}

# API keys for integrations
path "secret/data/ghost/integrations/*" {
  capabilities = ["read", "list"]
}

# Allow renewing own token
path "auth/token/renew-self" {
  capabilities = ["update"]
}

# Allow looking up own token
path "auth/token/lookup-self" {
  capabilities = ["read"]
}

# Kubernetes auth
path "auth/kubernetes/login" {
  capabilities = ["create", "update"]
}

# AppRole auth (for CI/CD)
path "auth/approle/login" {
  capabilities = ["create", "update"]
}

# Read Ghost AppRole role ID
path "auth/approle/role/ghost/role-id" {
  capabilities = ["read"]
}
