# Proposal: azure-dev-infrastructure

## Why

Issue #8. Everything so far runs only on a laptop. Two standing promises now have nowhere to be
true: BR-010's "secrets are names, resolved at use" is real only against a real vault (the
`ISecretResolver` seam shipped with dev user-secrets behind it), and the migration change
declared "in production the same executable runs as a deploy job (#8)" — a claim that stays
fiction until an environment exists to run it. Deferred deliberately in Phase 1 until the loop
was proven locally; the loop is proven, the milestone's spine starts here.

Grill decisions (recorded on the issue): **northeurope**; remote state in a bootstrap storage
account; **the human applies locally** — CI runs fmt/validate/plan only, no CI identity can
change the subscription in this change; `aio-<env>-<resource>` naming. The subscription id was
supplied privately and never enters this public repo (BR-010 posture): it reaches Terraform via
gitignored `terraform.tfvars` / `ARM_SUBSCRIPTION_ID` only.

## What changes

- **`infra/` at the repo root** — Terraform for the dev environment, one `dev` root module:
  resource group, Log Analytics workspace, Container Apps environment, the portal container app,
  a container registry, PostgreSQL Flexible Server (+ the `aiorchestratordb` database), and
  Key Vault. The portal app runs with a **system-assigned managed identity** granted Key Vault
  get/list secrets and ACR pull — no credential is ever written into app configuration.
- **The MigrationService becomes the deploy step it promised to be:** a Container Apps **Job**
  from the same image family, run before rollout; the schema is never changed by the Server
  starting, in any environment (same invariant as the AppHost graph, now in Azure).
- **The Key Vault resolver behind the existing seam (design D3):** the host's composition root
  registers Aspire's Key Vault client integration (`Aspire.Azure.Security.KeyVault`) and a
  `KeyVaultSecretResolver : ISecretResolver` when a vault URI is configured; without one, the
  configuration-backed resolver keeps serving dev and tests. No module changes; no call site
  changes — that was the seam's whole promise, and this change is what proves it.
- **Bootstrap script** for the state backend: idempotent creation of the state resource group +
  storage account + container, runnable by the human once.
- **CI:** a `terraform` lane that runs fmt/validate (and plan when credentials are absent it
  skips honestly rather than pretending) — apply is never CI's in this change.

## Verified preconditions (ADR-0001 — exercised, not assumed)

- The target subscription is reachable: `az account show` names it. At grill time it was **not**
  visible to the cached CLI login (guest tenants behind MFA); the owner re-authenticates before
  implementation starts. Implementation refuses to apply against any other subscription.

## Impact

- New top-level `infra/` directory; `.gitignore` gains Terraform state/vars patterns.
- `AiOrchestrator.Server`: one composition-root change (Key Vault resolver registration).
- CI: one new lint-style lane; no existing lane changes.
- Specs: new `portal-infrastructure` capability; `backend-architecture` delta for the resolver
  composition rule.
- **Cost note:** dev SKUs at the bottom of each range (Consumption ACA, burstable B1ms Postgres,
  basic ACR, standard KV) — the design records each choice so raising them later is deliberate.
