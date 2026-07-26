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

## What CI does and does not do

CI runs `terraform fmt -check`, `terraform validate` and `shellcheck`. It has **no Azure
credentials** and cannot change the subscription. Applying is a human action — see
[ARCHITECTURE.md](../ARCHITECTURE.md#deployment).

## Cost

Dev SKUs sit at the bottom of each range (Consumption Container Apps scaling to zero, B1ms
Postgres, Basic ACR, standard Key Vault). The Postgres server is the only resource that bills
continuously. `terraform destroy` removes everything; the state backend survives, being outside
this root module.
