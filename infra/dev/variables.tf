variable "subscription_id" {
  description = <<-EOT
    The target Azure subscription. Supplied through the gitignored terraform.tfvars or
    ARM_SUBSCRIPTION_ID — never committed, because this repository is public (design D3).
  EOT
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F-]{36}$", var.subscription_id))
    error_message = "subscription_id must be a GUID."
  }
}

variable "expected_subscription_hash" {
  description = <<-EOT
    SHA-256 of the subscription id this environment belongs to. The guard in main.tf compares
    the resolved subscription against it, so a wrong `az account set` fails at plan time rather
    than creating resources in someone else's subscription. The hash is safe to commit; the id
    is not.
  EOT
  type        = string
  default     = "8540b85423bbab52e34ab92af6031d6b36ec56f3d9f6aa9d47b4004c3fe8a4f7"
}

variable "environment" {
  description = "Environment infix in every resource name (aio-<env>-<resource>)."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region. northeurope per the grill decision on issue #8."
  type        = string
  default     = "northeurope"
}

variable "postgres_sku" {
  description = <<-EOT
    Flexible Server SKU. B_Standard_B1ms is the smallest burstable tier: dev data is disposable
    and the cost record in design.md exists so raising this is a deliberate act.
  EOT
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_storage_mb" {
  description = "Flexible Server storage. 32768 is the minimum the service accepts."
  type        = number
  default     = 32768
}

variable "postgres_admin_username" {
  description = "Administrator login. The password is generated and stored in Key Vault, never here."
  type        = string
  default     = "aioadmin"
}

variable "portal_image" {
  description = <<-EOT
    Fully qualified image for the portal container app. Defaults to the Microsoft hello-world
    image so the very first apply produces a running, verifiable environment before any of our
    images exist — the app revision is replaced by deploy.sh once the real image is pushed.
  EOT
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "dispatch_image" {
  description = "Fully qualified image for the dispatch worker job. Same bootstrapping rationale as portal_image."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "migration_image" {
  description = "Fully qualified image for the migration job. Same bootstrapping rationale as portal_image."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}
