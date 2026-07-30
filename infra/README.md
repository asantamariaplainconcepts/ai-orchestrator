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

Pushes all three images, runs the migration job, waits for it to succeed, then moves the dispatch
worker and the portal revision. **A failed migration stops the deploy with the previous revision
still serving.** It finishes by reading back the *running* image of the portal and the worker and
refusing to report success unless both carry the tag just deployed — #92 shipped a worker three
days stale precisely because every command returned zero and nothing compared the result to the
intent.

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
  *does* hold a credential — federated, short-lived, and scoped to the `dev` Environment. The
  whole workflow is one job for that reason: a job outside the environment gets a token subject
  naming the branch, which no credential here accepts, and making one that did would hand every
  push to `main` rights over the resource group by a route nobody chose.

**Approval is per environment, not per pipeline** (DEC-047). `prod` will have a reviewer and its
own identity — a run holding dev's credential cannot reach it, because the subject names the
environment.

**`dev` has no required reviewer: merging to `main` deploys it, unattended.** That is DEC-047's
position — dev is disposable, `terraform destroy` recreates it, and the owner deploys many times
a day. `prod` will keep a reviewer.

The consequence, stated rather than buried: anyone who can merge to `main` can change that
resource group and read its secrets. Terraform *manages* the vault's secrets, so it reads their
values on every refresh; the deploy identity therefore holds *Key Vault Secrets Officer* and can
see the database password. Fine for an environment `terraform destroy` recreates. Not fine for
production data.

> **This paragraph describes configuration no commit can verify.** Reviewers live on the GitHub
> environment, deliberately outside version control so the gate can be tightened without a pull
> request — which also means no diff ever forces this prose to keep up. It has now been wrong
> twice. Check `gh api repos/{owner}/{repo}/environments/dev` before trusting it, and correct it
> here when it drifts.

## Registering the sign-in app

```bash
./infra/entra-app.sh
```

Once per tenant, by a human with `az login`. Idempotent. Creates the Entra ID app registration the
portal signs users in with, its service principal, and a client secret written straight into Key
Vault without being printed.

A script rather than Terraform for the same reason `ci-identity.sh` is one: this is a **directory**
object, not a subscription resource. Managing it in Terraform would mean granting the CI deploy
identity Graph permissions with admin consent — widening what a pipeline can do inside the tenant,
which is a bigger blast radius than the resource group it manages today.

**Why a confidential web client and not a SPA.** The portal is a same-origin single web app: the Vite
build is served from the Server's `wwwroot` with an `index.html` fallback, API calls are relative, and
there is no CORS configuration anywhere. That shape is already a backend-for-frontend, so the session
belongs in an `HttpOnly` cookie on the server and no access token ever needs to reach the browser. A
public-client SPA flow would put tokens in JavaScript to solve a cross-origin problem this product
does not have.

| Where | What | Why this shape |
|---|---|---|
| Entra | app registration, this tenant only, **web** platform | the code is redeemed on the server; `/signin-oidc` is Microsoft.Identity.Web's default |
| Entra | front-channel logout URL | signing out of Entra should drop the local cookie session too |
| Entra | service principal | so the app appears as an enterprise application and users can be assigned |
| Entra | one client secret, 1 year | a confidential client needs a credential to redeem the code |
| Key Vault | the secret's **value** | piped from `az` into `az keyvault secret set` and never printed — this repository is public and so are its Actions logs |
| — | **no** implicit flow | the code flow redeems server-side, so no `id_token` issuance is needed |

The tenant id and client id are printed: they *identify* the app, they do not *authenticate* it. What
authenticates it is in the vault, and what reaches configuration is that secret's **name** (BR-010).

Rotation is deliberate: if the secret already exists the script leaves it alone rather than quietly
minting a second one.

The local redirect URI is the Server's own dev profile — `http://localhost:5080`, from
`launchSettings.json` rather than guessed, because a redirect URI that does not match to the character
fails sign-in with no useful message. Plain `http` is fine there only because Entra exempts
`localhost`; anywhere else it is rejected.

**Cookies, for whoever wires this up.** A cookie marked `Secure` is not sent over plain `http`, so the
local profile needs that relaxed in development or the session never arrives at all. `SameSite=Strict` is right for the *application session* —
every request carrying it is same-origin. The OIDC handshake cookies are not: the response arrives
from `login.microsoftonline.com`, which is cross-site, so `Strict` would drop them and sign-in would
fail in a way that looks like nothing happened at all. Leave those at the library's default.

This answers [OPN-002](../docs/product/mvp/07-open-decisions.md)'s first half. Its second half — a
local-dev and functional-test strategy, given that Entra cannot be containerized — the BFF shape
answers cheaply: the server owns the session, so the test tiers keep injecting `ICurrentPrincipal`
and Entra is composed only in the real host. Issue #11 records both outcomes.

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
| Azure | federated credential on `<sub_claim_prefix>:environment:dev` | scoped to the **environment**, not a branch, so a token can only be minted for a run a reviewer already approved. The prefix is read from GitHub rather than assembled: the default now embeds immutable owner and repo IDs, and a hand-built subject silently fails to match |
| Azure | *Contributor* + *User Access Administrator* on `rg-aio-dev` | Terraform creates role assignments of its own, and granting a role is itself a permission |
| Azure | *Storage Blob Data Contributor* on the state account | the backend authenticates as the workflow identity |
| Azure | *Key Vault Secrets Officer* on the vault | Terraform manages the secrets, so it reads their values on every refresh — without this, `plan` fails 403 before proposing anything |
| GitHub | environment `dev`, reviewers left as they are | the environment scopes the credential; approval is configured separately per environment (DEC-047) and is deliberately not in version control, so it can be tightened without a commit |
| GitHub | 4 secrets, 3 variables, at **repository** level | one place to look |

Every grant is scoped to the resource group, never the subscription: a deploy identity that can
reach everything is one compromised workflow away from being able to change everything.

Then start a run:

```bash
gh workflow run "Deploy (dev)" --repo asantamariaplainconcepts/ai-orchestrator --ref main
```

On `dev` the run proceeds unattended and the plan is printed in the summary of the run that made
the change — which is where anyone asking "what did this deploy do" will look. On an environment
with reviewers, the run waits at Actions → the run → *Review deployments*, and what is approved
is a commit rather than a diff of resources: showing the plan first would need a job outside the
environment, and therefore a credential mintable without approval.

**This pipeline has run** — [run 30293533800](https://github.com/asantamariaplainconcepts/ai-orchestrator/actions/runs/30293533800),
green end to end: token exchange, backend auth, plan, apply, `deploy.sh`, health check. Verified
from outside afterwards rather than trusting the workflow's own verdict: `POST /api/projects`
against the deployed portal returned **201** with the created entity, which exercises the app,
Postgres and Key Vault together.

It took four attempts, and every defect was found by running it — none by review. Worth keeping,
because the same three mistakes are available to anyone wiring OIDC:

1. **`azure/login`, wrong subject shape.** The plan job declared no environment, so its token
   named the branch; no credential accepted it, and none should have (#69).
2. **`azure/login`, wrong subject value.** GitHub's default subject embeds immutable owner and
   repository IDs; the script had assembled `repo:owner/repo` by hand (#71).
3. **`terraform plan`, 403 on Key Vault.** Terraform manages the vault's secrets and reads their
   values on refresh; `Contributor` is management plane only (#73).

`ARM_USE_OIDC` turned out to be unnecessary — the backend inherits the CLI session — and
`Contributor` does cover `az acr login`, so no *AcrPush* grant was needed. Both were predictions
in this file; both were wrong, which is the argument for writing predictions down.

**Still not exercised:** a from-scratch apply into an empty subscription, `terraform destroy`, and
a *failing* migration. That last one matters most — the ordering that keeps a bad migration from
taking the site down is verified by design and by a local run, not by CI having watched it
happen.

## Cost

Dev SKUs sit at the bottom of each range (Consumption Container Apps scaling to zero, B1ms
Postgres, Basic ACR, standard Key Vault). The Postgres server is the only resource that bills
continuously. `terraform destroy` removes everything; the state backend survives, being outside
this root module.
