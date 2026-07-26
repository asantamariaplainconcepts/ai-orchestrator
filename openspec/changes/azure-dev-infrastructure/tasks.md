# Tasks — azure-dev-infrastructure

Precondition first, then state, then resources, then the app path onto them — each step
exercised for real before the next (ADR-0001), asserting artifacts, not green pipelines
(ADR-0004).

## 0. Verified precondition

- [x] 0.1 The owner's az login reaches the target subscription: `az account show` names it.
      **Owner action** — at grill time the subscription was invisible to the cached CLI login
      (guest tenants behind MFA). **Done 2026-07-26:** the CLI now names the subscription and
      ARM calls succeed (`az group list` returns existing groups).
- [x] 0.2 Register the resource providers the environment needs. Checking rather than assuming
      found four of five **NotRegistered** on this subscription — `Microsoft.App`,
      `Microsoft.DBforPostgreSQL`, `Microsoft.ContainerRegistry`,
      `Microsoft.OperationalInsights` (only `Microsoft.KeyVault` was registered). Terraform
      fails partway through the first apply without them, which is the worst moment to discover
      it. Registration is idempotent and takes minutes; verify by reading `registrationState`
      back as `Registered`, not by the command exiting 0 (ADR-0004). **Done 2026-07-26:** all
      five now read `Registered`.

## 1. State backend

- [x] 1.1 `infra/bootstrap.sh`: idempotent az CLI creation of `rg-aio-tfstate` + storage account
      + `tfstate` container in northeurope; prints backend config. Running twice changes nothing.
- [x] 1.2 `.gitignore`: `*.tfstate*`, `*.tfvars`, `.terraform/` — verified by attempting to
      `git add` a dummy tfvars and seeing it refused.
- [x] 1.3 Verify: `terraform init` against the backend succeeds; the state blob exists in the
      portal (read back, not assumed).

## 2. The dev environment (Terraform)

- [x] 2.1 `infra/dev/`: provider pinned, subscription guard (fails plan on any subscription other
      than the expected one — expressed without committing the id), resource group, Log
      Analytics.
- [x] 2.2 Container registry (Basic), Container Apps environment (Consumption) wired to Log
      Analytics.
- [x] 2.3 PostgreSQL Flexible Server (B_Standard_B1ms/32 GB), `aiorchestratordb` database,
      firewall stance recorded in-code (dev: Azure-services access for ACA; no public 0.0.0.0).
- [x] 2.4 Key Vault (standard, RBAC mode). Terraform-generated database credentials stored as
      vault secrets; nothing sensitive in non-sensitive outputs (`terraform output` inspected).
- [x] 2.5 Portal container app: system-assigned identity, AcrPull + Key Vault Secrets User role
      assignments, env carries the vault URI and non-secrets only. Migration job (manual
      trigger) from the MigrationService image with the same access.
- [x] 2.6 Apply, by the human, and verify by artifact: resources exist (`az resource list` on
      the RG), the app's env inspected shows no secret values, the vault holds the generated
      secrets.

## 3. The application path

- [x] 3.1 `KeyVaultSecretResolver` in BuildingBlocks beside the configuration resolver;
      host composition selects by `Secrets:KeyVaultUri` presence via
      `Aspire.Azure.Security.KeyVault`. No module edits — prove with the ArchTest/analyzer suite
      unchanged.
- [x] 3.2 Unit-test the selection rule; functional tier keeps the configuration resolver (a test
      must not require a cloud — ADR-0002).
- [x] 3.3 Dockerfiles/publish for Server and MigrationService images; build and push to ACR.
- [x] 3.4 `infra/deploy.sh`: push image → run migration job → wait for exit 0 → update app
      revision. A failed job leaves the previous revision serving (verify by making it fail
      once, on purpose).
- [x] 3.5 **End-to-end artifact check** — done 2026-07-26: `POST /api/projects` on the deployed
      portal returned **201** with the created entity and a following `GET` read it back, proving
      managed identity → Key Vault → connection string → Postgres → API end to end. Reaching it
      took three deploys; the migration gate stopped the two failed ones before the portal moved,
      which is the safety property working rather than failing. Original text: the deployed portal serves the SPA over its ACA URL,
      `POST /api/projects` returns 201 (the unfakeable check), and a configured Connector
      resolves its PAT from the real vault.

## 4. CI

- [x] 4.1 `terraform` lane: fmt-check + validate on PRs touching `infra/`; fails loudly when it
      cannot run — never a silent skip.

## 5. Close-out

- [x] 5.1 `ARCHITECTURE.md`: deployment topology section; the deploy sequence and who runs it.
- [x] 5.2 Full verify sweep; CI green.
