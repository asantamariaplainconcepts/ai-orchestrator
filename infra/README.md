# Infrastructure

Terraform for the Azure **dev** environment, plus the three scripts that bootstrap, entitle and
release it. Each one is idempotent, confirms before it acts, and refuses the wrong subscription.

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
  *does* hold a credential — federated, short-lived, and scoped to the `dev` Environment — and
  GitHub will not mint it at all until a reviewer approves the run (DEC-046). The whole workflow
  is one job for that reason: a job outside the environment gets a token subject naming the
  branch, which no credential here accepts, and making one that did would hand every push to
  `main` unattended rights over the resource group.

The human decision that design D7 protected is intact; what changed is its form. It used to be a
command typed at a terminal, which meant no terminal, no deploy. It is now a click on a plan,
which is available from a phone.

## Setting up the deploy credential

```bash
./infra/ci-identity.sh
```

Once per subscription, by a human with `az login` and `gh auth login`. Idempotent, and it refuses
to run against the wrong subscription — the same SHA-256 guard Terraform uses, checked *before*
anything is created rather than at plan time. The subscription id goes from `az` straight into
`gh secret set` through a pipe and is never printed.

What it creates:

| Where | What | Why this shape |
|---|---|---|
| Azure | app registration `github-ai-orchestrator-dev` | no client secret exists — that is the point of OIDC |
| Azure | federated credential on `repo:…:environment:dev` | scoped to the **environment**, not a branch, so a token can only be minted for a run a reviewer already approved |
| Azure | *Contributor* + *User Access Administrator* on `rg-aio-dev` | Terraform creates role assignments of its own, and granting a role is itself a permission |
| Azure | *Storage Blob Data Contributor* on the state account | the backend authenticates as the workflow identity |
| GitHub | environment `dev` with you as required reviewer | without this the environment is a label, not a gate |
| GitHub | 4 secrets, 3 variables, at **repository** level | the plan job declares no environment so that it needs no approval — which also means it cannot read an environment-scoped secret |

Every grant is scoped to the resource group, never the subscription: a deploy identity that can
reach everything is one compromised workflow away from being able to change everything.

Then start a run:

```bash
gh workflow run "Deploy (dev)" --repo asantamariaplainconcepts/ai-orchestrator --ref main
```

The run waits for your approval before it can reach Azure at all. Approve it in Actions → the
run → *Review deployments*; the plan is printed in the summary of the run that made the change.

You approve a commit rather than a diff of resources. Showing the plan first would need a job
outside the environment, and therefore a credential usable without approval — which is the thing
being protected against. The Terraform in that commit already passed `validate` in PR review.

**This pipeline has never run** (ADR-0005). Its YAML parses, `shellcheck` and `terraform fmt`
pass, and every step is one that has worked by hand — but the federated credential does not exist
yet, so nothing has exercised the token exchange, the role assignments, or `az acr login` from a
runner identity. The first attempt failed in `azure/login` and the repair is already in — the
unattended plan job could not be authenticated, and should not have been (#69). Two failures are
still plausible past that point:

- **`Initialise` fails authorizing the blob** — the backend is not picking up the federated
  credential. Add `ARM_USE_OIDC: true` to the Terraform steps' `env` in `deploy.yml`.
- **`az acr login` fails in the deploy step** — `Contributor` on the resource group is documented
  to cover registry push, but if it does not, grant *AcrPush* on the registry itself.

## Cost

Dev SKUs sit at the bottom of each range (Consumption Container Apps scaling to zero, B1ms
Postgres, Basic ACR, standard Key Vault). The Postgres server is the only resource that bills
continuously. `terraform destroy` removes everything; the state backend survives, being outside
this root module.
