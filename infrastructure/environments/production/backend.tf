# Terraform Backend Configuration
# This file configures remote state storage and locking

################################################################################
# Option 1: Terraform Cloud Backend (Recommended)
################################################################################

# Terraform Cloud provides:
# - Remote state storage with encryption
# - State locking
# - Version control
# - Team collaboration
# - Audit logging
# - Policy as Code (Sentinel)
# - Cost estimation
# - Private registry

terraform {
  backend "remote" {
    hostname     = "app.terraform.io"
    organization = "YOUR_ORGANIZATION_NAME" # Change this

    workspaces {
      name = "ghost-blog-production"
    }
  }
}

# Setup instructions for Terraform Cloud:
# 1. Create account at https://app.terraform.io
# 2. Create organization
# 3. Create workspace named "ghost-blog-production"
# 4. Set workspace to "CLI-driven workflow"
# 5. Configure environment variables:
#    - AWS_ACCESS_KEY_ID (sensitive)
#    - AWS_SECRET_ACCESS_KEY (sensitive)
#    - TF_VAR_* for any sensitive variables
# 6. Run: terraform login
# 7. Run: terraform init

################################################################################
# Option 2: AWS S3 Backend with DynamoDB Locking (Alternative)
################################################################################

# Uncomment this section if you prefer S3 backend over Terraform Cloud
# Comment out the "remote" backend above if using this option

# terraform {
#   backend "s3" {
#     # S3 bucket for state storage
#     bucket = "ghost-blog-terraform-state-production"
#     key    = "production/terraform.tfstate"
#     region = "us-east-1"
#
#     # DynamoDB table for state locking
#     dynamodb_table = "ghost-blog-terraform-locks"
#
#     # Encryption at rest
#     encrypt = true
#     kms_key_id = "arn:aws:kms:us-east-1:ACCOUNT_ID:key/KEY_ID"
#
#     # Versioning (enabled on bucket)
#     versioning = true
#
#     # Server-side encryption
#     server_side_encryption_configuration {
#       rule {
#         apply_server_side_encryption_by_default {
#           sse_algorithm     = "aws:kms"
#           kms_master_key_id = "arn:aws:kms:us-east-1:ACCOUNT_ID:key/KEY_ID"
#         }
#       }
#     }
#
#     # Access logging
#     logging {
#       target_bucket = "ghost-blog-terraform-logs"
#       target_prefix = "state-access-logs/"
#     }
#   }
# }

# Prerequisites for S3 backend:
# 1. Create S3 bucket with versioning enabled:
#    aws s3api create-bucket \
#      --bucket ghost-blog-terraform-state-production \
#      --region us-east-1
#
#    aws s3api put-bucket-versioning \
#      --bucket ghost-blog-terraform-state-production \
#      --versioning-configuration Status=Enabled
#
# 2. Create DynamoDB table for locking:
#    aws dynamodb create-table \
#      --table-name ghost-blog-terraform-locks \
#      --attribute-definitions AttributeName=LockID,AttributeType=S \
#      --key-schema AttributeName=LockID,KeyType=HASH \
#      --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 \
#      --region us-east-1
#
# 3. Apply bucket policy for encryption and access control
# 4. Enable server-side encryption on bucket
# 5. Run: terraform init -migrate-state (if migrating from local state)

################################################################################
# Backend Configuration Variables
################################################################################

# For Terraform Cloud, set these in the workspace:
# - Environment Variables (UI)
# - Terraform Variables (UI)
# - Variable Sets (for shared variables across workspaces)

# For S3 backend, you can use a backend config file:
# Create a file named backend-config.hcl:
#
# bucket         = "ghost-blog-terraform-state-production"
# key            = "production/terraform.tfstate"
# region         = "us-east-1"
# dynamodb_table = "ghost-blog-terraform-locks"
# encrypt        = true
#
# Then run: terraform init -backend-config=backend-config.hcl

################################################################################
# State Locking
################################################################################

# State locking prevents concurrent operations and state corruption
# Both Terraform Cloud and S3+DynamoDB provide state locking

# If a lock is stuck (e.g., after a crash), you can force-unlock:
# terraform force-unlock LOCK_ID

################################################################################
# State Security Best Practices
################################################################################

# 1. Never commit state files to version control
#    - Add *.tfstate* to .gitignore
#
# 2. Use encryption at rest and in transit
#    - Terraform Cloud: Encrypted by default
#    - S3: Enable server-side encryption with KMS
#
# 3. Restrict access to state
#    - Use IAM policies to limit who can read/write state
#    - Terraform Cloud: Use team-based access control
#
# 4. Enable versioning
#    - Allows rollback to previous state versions
#    - S3: Enable bucket versioning
#    - Terraform Cloud: Built-in versioning
#
# 5. Audit access
#    - Enable CloudTrail for S3 backend
#    - Terraform Cloud provides audit logs
#
# 6. Regular backups
#    - S3: Cross-region replication
#    - Terraform Cloud: Automatic backups
#
# 7. Sensitive data
#    - Mark outputs as sensitive
#    - Use Terraform Cloud for encrypted variable storage
#    - Use AWS Secrets Manager for application secrets

################################################################################
# Workspace Configuration
################################################################################

# Terraform workspaces can be used for environment separation
# However, for production, we recommend separate backend configurations
# per environment rather than workspaces

# For multi-environment setup:
# - Development: workspace = "ghost-blog-development"
# - Staging: workspace = "ghost-blog-staging"
# - Production: workspace = "ghost-blog-production"

################################################################################
# State Import/Export
################################################################################

# To import existing resources:
# terraform import module.vpc.aws_vpc.main vpc-xxxxxxxxx

# To export state for backup:
# terraform state pull > terraform.tfstate.backup

# To inspect state:
# terraform state list
# terraform state show module.eks.aws_eks_cluster.main

################################################################################
# Migration from Local to Remote Backend
################################################################################

# If migrating from local state:
# 1. Add backend configuration to this file
# 2. Run: terraform init -migrate-state
# 3. Confirm migration when prompted
# 4. Verify: terraform state list
# 5. Delete local state files after verification

################################################################################
# Disaster Recovery for State
################################################################################

# In case of state loss or corruption:
#
# 1. Restore from backup:
#    - S3: Use versioning to restore previous version
#    - Terraform Cloud: Use version history
#
# 2. Rebuild state from existing infrastructure:
#    - Create empty state
#    - Import all resources: terraform import
#    - Verify: terraform plan (should show no changes)
#
# 3. Use state snapshots:
#    - Regular exports: terraform state pull
#    - Store in separate secure location
