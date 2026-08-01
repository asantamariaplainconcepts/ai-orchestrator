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

variable "entra_tenant_id" {
  description = <<-EOT
    Tenant of the sign-in app registration (#12). Empty means sign-in stays off and the portal
    keeps the warned unauthenticated stopgap — presence is the switch, matching the Server's own
    composition rule (DEC-058). Not a secret: it identifies the app, it does not authenticate it.
  EOT
  type        = string
  default     = ""
}

variable "entra_client_id" {
  description = "Client id of the sign-in app registration. Same presence rule and same non-secrecy as entra_tenant_id."
  type        = string
  default     = ""
}

variable "bootstrap_admins" {
  description = <<-EOT
    Provider object ids holding Admin on every project (#13, design D4). Comma-separated, because an
    environment variable is what a container app env can carry and a repository variable is what
    Terraform reads it from.

    Empty is a real and honest state, not a safe default: nobody can configure anything, and the
    portal says so at startup. It is set here because #13 retires the interim rule that every
    signed-in user held Admin — deploying without naming somebody locks the owner out of their own
    portal, and only a deploy can let them back in.

    Not a secret. An object id identifies a person in a tenant; it authenticates nobody. It stays out
    of git regardless, for the same reason the Entra ids do.
  EOT
  type        = string
  default     = ""
}

variable "session_image" {
  description = <<-EOT
    The conversation session's image (#166). A placeholder until the first deploy pushes a real tag,
    exactly as the jobs use one: deploy.sh rolls the image, and pinning a tag here would make
    Terraform and the deploy script disagree about which one is current.
  EOT
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "operator_object_ids" {
  description = <<-EOT
    Provider object ids that may exercise the dispatch queue by hand (#195). Comma-separated, the
    same shape and for the same reason as `bootstrap_admins`: a repository variable is what
    Terraform reads a list of people from.

    Empty is the default and a real state, not a placeholder: nobody holds Storage Queue Data
    Contributor, which is correct until somebody needs to enqueue a test message. It is deliberately
    separate from `bootstrap_admins` — administering the portal and reaching Azure's data plane are
    different powers, and one list for both would make adding an administrator grant the other
    silently.

    Not a secret. An object id identifies a person in a tenant; it authenticates nobody.
  EOT
  type        = string
  default     = ""
}
