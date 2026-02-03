# Backend configuration for Terraform state management
# Choose one of the following options:

# Option 1: Terraform Cloud (Recommended for teams)
terraform {
  cloud {
    organization = "ghost-platform"
    
    workspaces {
      name = "ghost-development"
    }
  }
}

# Option 2: AWS S3 Backend (Uncomment to use)
# terraform {
#   backend "s3" {
#     bucket         = "ghost-terraform-state"
#     key            = "environments/development/terraform.tfstate"
#     region         = "us-east-1"
#     encrypt        = true
#     dynamodb_table = "ghost-terraform-locks"
#     
#     # Enable versioning on the S3 bucket for state history
#     # Create DynamoDB table for state locking:
#     # aws dynamodb create-table \
#     #   --table-name ghost-terraform-locks \
#     #   --attribute-definitions AttributeName=LockID,AttributeType=S \
#     #   --key-schema AttributeName=LockID,KeyType=HASH \
#     #   --billing-mode PAY_PER_REQUEST
#   }
# }

# Option 3: GCP GCS Backend (Uncomment to use)
# terraform {
#   backend "gcs" {
#     bucket  = "ghost-terraform-state"
#     prefix  = "environments/development"
#   }
# }

# Option 4: Azure Blob Storage Backend (Uncomment to use)
# terraform {
#   backend "azurerm" {
#     resource_group_name  = "ghost-terraform-state"
#     storage_account_name = "ghosttfstate"
#     container_name       = "tfstate"
#     key                  = "development.terraform.tfstate"
#   }
# }

# Note: Comment out the 'cloud' block above before using S3/GCS/Azure backends
