# Infrastructure

Terraform for the Azure **dev** environment, plus the two scripts that bootstrap and release it.

## Once per subscription

```bash
./infra/bootstrap.sh
```

Creates the remote-state backend (resource group, storage account, container) and prints the
`terraform init` command. Idempotent — running it again changes nothing.

Then create `infra/dev/terraform.tfvars` (gitignored):

```hcl
subscription_id = "<your subscription id>"
```

## Apply

```bash
cd infra/dev
terraform init <backend-config flags printed by bootstrap.sh>
terraform plan
terraform apply
```

The plan fails immediately if `az account` resolves to a subscription other than the expected
one — the guard compares a SHA-256, so this repository never carries the id itself. Pointing the
environment at a different subscription means updating `expected_subscription_hash`.

Requires the five providers to be registered (`Microsoft.App`, `Microsoft.DBforPostgreSQL`,
`Microsoft.KeyVault`, `Microsoft.ContainerRegistry`, `Microsoft.OperationalInsights`). Check with
`az provider show -n <name> --query registrationState` — an unregistered provider fails the apply
partway through, which is a bad moment to find out.

## Release

```bash
./infra/deploy.sh          # TAG defaults to the short commit sha
```

Pushes both images, runs the migration job, waits for it to succeed, then moves the portal
revision. **A failed migration stops the deploy with the previous revision still serving.**

Verify a release by its artifact, not by the script's exit code (ADR-0004):

```bash
curl -s -X POST "$(terraform -chdir=infra/dev output -raw portal_url)/api/projects" \
  -H 'Content-Type: application/json' -d '{"name":"deploy check"}'
```

A `201` with the created entity proves the app, the database and the vault are all wired; a bare
`200` from any URL proves nothing.

## When a deploy fails

`deploy.sh` stops before touching the portal if migrations fail — the previous revision keeps
serving. That is the designed behaviour, and it has been exercised: the first real deploy failed
on a bad base image and the site was never updated.

```bash
az containerapp job logs show -n caj-aio-dev-migrations -g rg-aio-dev \
  --container migrations --execution <name-printed-by-deploy>
```

## Dispatch

The dispatch queue, its worker job and their identity live in `dispatch.tf`. The job is
KEDA-scaled on queue length and scales to zero.

Enqueue a message by hand to exercise it:

```bash
az storage message put \
  --queue-name "$(terraform -chdir=infra/dev output -raw dispatch_queue_name)" \
  --account-name "$(terraform -chdir=infra/dev output -raw dispatch_queue_account)" \
  --auth-mode login \
  --content "$(printf '{"v":1,"runId":"%s"}' "$(uuidgen | tr 'A-Z' 'a-z')")"
```

**Send the JSON as-is — do not base64 it.** The .NET client's message encoding is `None`, so it
reads the stored text verbatim. A pre-encoded message is claimed, found unparseable, and dropped
(by design), which looks from the outside like the scaler working and the worker doing nothing.
Read the message back with `az storage message peek` if in doubt: what you see is what the worker
sees.

Then read back the execution — a job that ran is the artifact, not the enqueue's exit code:

```bash
az containerapp job execution list -n "$(terraform -chdir=infra/dev output -raw dispatch_job_name)" -g rg-aio-dev -o table
```

**KEDA is only verifiable here.** Azurite exercises the queue contract locally, but nothing local
runs the scaler — a green functional suite says nothing about whether the scale rule fires.

## What CI does and does not do

Two lanes, deliberately separate:

- **[terraform.yml](../.github/workflows/terraform.yml)** runs on every PR: `terraform fmt -check`,
  `terraform validate`, `shellcheck`. It has **no Azure credentials** and requests none, so the
  worst a pull request from anywhere can do to the subscription is nothing.
- **[deploy.yml](../.github/workflows/deploy.yml)** runs on merge to `main` and on dispatch. It
  *does* hold a credential — federated, short-lived, and scoped to the `dev` Environment — and it
  cannot use it until a reviewer approves the run with the plan already printed (DEC-046).

The human decision that design D7 protected is intact; what changed is its form. It used to be a
command typed at a terminal, which meant no terminal, no deploy. It is now a click on a plan,
which is available from a phone.

## Setting up the deploy credential

One-time, and **entirely in the browser** — no CLI. Everything below is in the Azure portal and
GitHub's settings.

**1. Azure — app registration.** Microsoft Entra ID → App registrations → New registration. Name
it `github-ai-orchestrator-dev`, single tenant, no redirect URI. Keep the **Application (client)
ID** and **Directory (tenant) ID**.

**2. Azure — federated credential.** In that registration: Certificates & secrets → Federated
credentials → Add → *GitHub Actions deploying Azure resources*.

| Field | Value |
|---|---|
| Organization | `asantamariaplainconcepts` |
| Repository | `ai-orchestrator` |
| Entity type | **Environment** |
| Environment name | `dev` |

Entity type matters. Scoping to the environment rather than to the branch is what makes the
approval gate load-bearing: a token is only ever issued for a run that has already been approved.
**No client secret is created** — that is the point of OIDC.

**3. Azure — roles.** The registration's service principal needs, on `rg-aio-dev`:
*Contributor* (Terraform manages the resources) and *User Access Administrator* (Terraform creates
role assignments, and granting roles is itself a permission). On the state storage account:
*Storage Blob Data Contributor*. Subscription → Access control (IAM) → Add role assignment for
each.

Scope them to the resource group, never the subscription. A deploy identity that can reach
anything is one compromised workflow away from being able to change anything.

**4. GitHub — Environment.** Settings → Environments → New environment → `dev`. Add yourself under
**Required reviewers**. This is the gate; without it the credential is usable unattended.

**5. GitHub — secrets and variables.** On that environment (or the repository):

| Kind | Name | Value |
|---|---|---|
| Secret | `AZURE_CLIENT_ID` | Application (client) ID from step 1 |
| Secret | `AZURE_TENANT_ID` | Directory (tenant) ID from step 1 |
| Secret | `ARM_SUBSCRIPTION_ID` | the subscription id |
| Secret | `TF_STATE_STORAGE_ACCOUNT` | `staiotfstate<hash>`, printed by `bootstrap.sh` |
| Variable | `TF_STATE_RESOURCE_GROUP` | `rg-aio-tfstate` |
| Variable | `TF_STATE_CONTAINER` | `tfstate` |
| Variable | `TF_STATE_KEY` | `dev.tfstate` |

The subscription id is a secret here for one reason beyond policy: **this repository is public, so
Actions logs are public**, and GitHub redacts secret values wherever they appear in a log. The
storage account name embeds a hash of the subscription id, so it gets the same treatment.

**6. Run it.** Actions → Deploy (dev) → Run workflow. The `plan` job finishes unattended; the
`deploy` job waits for your approval with the plan in the run summary.

**This pipeline has never run** (ADR-0005). Its YAML parses, `shellcheck` and `terraform fmt`
pass, and every step is one that has worked by hand — but the federated credential does not exist
yet, so nothing has exercised the token exchange, the role assignments, or `az acr login` from a
runner identity. Treat the first run as the test it is. The two things most likely to be wrong
are the role scope in step 3 (`Contributor` on the resource group should cover ACR push, and if
it does not the fix is to add *AcrPush* on the registry) and a provider that was registered
interactively on a laptop but never in this subscription's own configuration.

## Cost

Dev SKUs sit at the bottom of each range (Consumption Container Apps scaling to zero, B1ms
Postgres, Basic ACR, standard Key Vault). The Postgres server is the only resource that bills
continuously. `terraform destroy` removes everything; the state backend survives, being outside
this root module.
