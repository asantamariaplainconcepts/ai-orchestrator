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

## The environment is workload-profiles, and replacing it is a rebuild

`azurerm_container_app_environment.main` declares a `workload_profile` block because dynamic
sessions refuse a Consumption-only environment (#200). Azure does not convert the type in place, so
**changing or removing that block replaces the environment** — and the portal and the migration
job go with it, because they take their environment id from it.

What survives a replacement: PostgreSQL, Key Vault, the registry, the storage account and the Data
Protection key ring. What does not: the portal is down while it is recreated, and **its hostname
changes**, because it comes from the environment's default domain.

After any replacement, re-run the Entra registration against the new origin — sign-in fails until
the redirect URIs match to the character:

```bash
DEPLOYED_ORIGIN="$(terraform -chdir=infra/dev output -raw portal_url)" ./infra/entra-app.sh
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

Pushes the images, runs the migration job, waits for it to succeed, then moves the conversation
session pool and the portal revision, in that order. **A failed migration stops the deploy with
the previous revision still serving.** It finishes by reading back the *running* image of each and
refusing to report success unless it carries the tag just deployed — #92 shipped a worker three
days stale precisely because every command returned zero and nothing compared the result to the
intent, and the session pool was left out of both the roll and the check until #193. (The dispatch
worker that lesson was learned on retired with the queue in #296; the check outlives it.)

Nothing here needs a `terraform apply`. Every workload's image is Terraform's only at bootstrap;
each carries `ignore_changes` on it and this script owns it afterwards, so a release never touches
the infrastructure and an apply never rolls back a deploy.

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

**There is no dispatch infrastructure any more.** The Storage Queue and its KEDA-scaled worker
job retired with DEC-013's supersession (#296): a Run is published to the Postgres outbox that
integration events already use, and consumed by the portal's own subscriber. Executing a Run
became an API call and a poll loop — the heavy half lives in a per-Run **sandbox**, which scales
itself — so there was nothing left for a scaler to scale.

`dispatch.tf` keeps its name and two residents, each for a reason that has nothing to do with
dispatch:

- **the storage account**, because it hosts the portal's Data Protection key ring (#180) — the
  thing that lets an OIDC sign-in survive a scale-to-zero and a revision change;
- **the user-assigned identity**, because conversation sessions deliberately run as it. A session
  clones repositories with project PATs, and the portal must not gain the ability to read a
  project credential just because it can start a conversation.

To exercise dispatch now, create a Run in the portal and watch it move. There is no message to
put by hand, and the loop it exercises is the same one the functional suite runs — which is the
point of having one substrate.

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
| Entra | id-token issuance ON, access-token issuance OFF | Microsoft.Identity.Web's sign-in-only web apps use the id_token hybrid by form post to the server (#172) — the browser still never sees a token; access tokens stay off because nothing acquires them yet |

The tenant id and client id are printed: they *identify* the app, they do not *authenticate* it. What
authenticates it is in the vault, and what reaches configuration is that secret's **name** (BR-010).

Rotation is deliberate: if the secret already exists the script leaves it alone rather than quietly
minting a second one.

The local redirect URI is the Server's own dev profile — `http://localhost:5080`, from
`launchSettings.json` rather than guessed, because a redirect URI that does not match to the character
fails sign-in with no useful message. Plain `http` is fine there only because Entra exempts
`localhost`; anywhere else it is rejected.

**Cookies, for whoever wires this up.** A cookie marked `Secure` is not sent over plain `http`, so the
local profile needs that relaxed in development or the session never arrives at all. And the session cookie is
`Strict`, which only works because **the SPA shell is served anonymously**: the callback's return to
`/` is the one cross-site-initiated navigation in the flow, and it needs no cookie. Requiring a
session for the shell is what produced an infinite sign-in loop (#176) and got the cookie wrongly
relaxed to `Lax` before the real cause was found (#182, DEC-060). The handshake cookies stay at the
library's defaults — that response really does arrive cross-site. `SameSite=Strict` is right for the *application session* —
every request carrying it is same-origin. The OIDC handshake cookies are not: the response arrives
from `login.microsoftonline.com`, which is cross-site, so `Strict` would drop them and sign-in would
fail in a way that looks like nothing happened at all. Leave those at the library's default.

**Configuring the Server** (what the script prints, wired):

```
AzureAd__TenantId   = <tenant id>          # identifies, does not authenticate
AzureAd__ClientId   = <app client id>      # same
AzureAd__ClientSecret = <from Key Vault>   # the vaulted 'entra-client-secret' — arrives as a
                                           # container secret reference, never a committed value
```

Presence of `AzureAd__ClientId` is what turns sign-in on (#12): no configuration, no provider —
`aspire run` and the self-host compose keep behaving exactly as before, which is the two-mode
contract DEC-058 records.

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
