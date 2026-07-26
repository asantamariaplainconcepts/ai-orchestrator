terraform {
  required_version = "~> 1.15"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Configured at init time from the bootstrap script's output, so no account name or key is
  # committed. See infra/bootstrap.sh.
  backend "azurerm" {}
}

provider "azurerm" {
  subscription_id = var.subscription_id

  features {
    key_vault {
      # Dev vaults are disposable: destroy should not leave a soft-deleted name blocking the
      # next apply. Production would keep purge protection on.
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      # Refuse to delete a resource group that still contains resources Terraform does not
      # know about — a silent data loss the default behaviour permits.
      prevent_deletion_if_contains_resources = true
    }
  }
}

data "azurerm_client_config" "current" {}

# The subscription guard (design D3). The expected subscription is identified by the SHA-256 of
# its id, so the repository can refuse the wrong target without ever carrying the id itself.
# A mismatch fails at plan time, before a single resource is touched.
locals {
  actual_subscription_hash = sha256(data.azurerm_client_config.current.subscription_id)
}

resource "terraform_data" "subscription_guard" {
  lifecycle {
    precondition {
      condition = local.actual_subscription_hash == var.expected_subscription_hash
      error_message = join(" ", [
        "Refusing to plan: the resolved Azure subscription is not the expected one.",
        "Run `az account set --subscription <id>` and check `az account show`.",
        "If you are deliberately targeting a new subscription, update",
        "expected_subscription_hash in variables.tf with the SHA-256 of its id."
      ])
    }
  }
}

locals {
  # aio-<env>-<resource>, per the grill decision. Kept short deliberately: Key Vault names cap
  # at 24 characters and storage at 24 lowercase alphanumerics.
  prefix = "aio-${var.environment}"

  tags = {
    product     = "ai-orchestrator"
    environment = var.environment
    managed_by  = "terraform"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags

  depends_on = [terraform_data.subscription_guard]
}
