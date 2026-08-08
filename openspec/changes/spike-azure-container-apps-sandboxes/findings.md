# spike-azure-container-apps-sandboxes — findings

The spec's rule for a spike: every hypothesis carries a verdict with the command exercised and the
observed output, and **a hypothesis that was not exercised reads "not verified"** rather than being
inferred from documentation. This file starts in that state on purpose — so the honest answer is
the default and filling it in is the work, not remembering to be careful.

Vendor announcements, portal docs, an official sample repository and a third-party write-up have
all been read. **None of it counts here.** Where they say something specific, it is recorded below
as an expectation the exercise will confirm or refute, which is a different column from a verdict.

## Verdicts

| # | Hypothesis | Verdict |
|---|---|---|
| H1 | Our own image boots and runs an agent | **partly verified** — see below |
| H2 | The workspace reaches a remote sandbox at all | **not verified** |
| H3 | A port is reachable while the Run lives, and gone after | **not verified** |
| H4 | A credential reaches the agent without living at rest | **not verified** |
| H5 | A Run's shape survives the lifecycle | **not verified** |
| H6 | The economics and the limits fit a Run | **not verified** |
| — | Does this substrate fit `IAgentProcessHost`? | **not verified** |

## What has actually been exercised

### Access — verified 2026-08-08

`bash poc/00-preflight.sh` on the authoring machine: Azure CLI 2.82.0, signed in to a Visual Studio
Enterprise subscription, `Microsoft.App` **Registered**. Checked separately with
`az provider show -n Microsoft.App`: the provider offers `sandboxGroups`,
`sandboxGroups/vnetConnections` and `sandboxes` at api-version **`2026-02-01-preview`**, in a region
list including **Spain Central**, West Europe and North Europe.

**This overturns the assumption the spike was written under.** The proposal said Azure access was
suspect because the deploy pipeline has been failing at `Initialise`; that failure is a disabled
Terraform state storage account and says nothing about whether the subscription can create
sandboxes. It can. The only missing piece is the `aca` CLI, which is not installed.

### Access, second look — verified 2026-08-08, and it blocks the spike

The first preflight found a signed-in subscription and concluded the access question was answered.
It was not: **signing in is not the same as being able to create**.

Subscription `e2f02d95-…` ("Sandbox - Services", Enabled), CLI `aca 1.0.0-preview.1` installed.
`aca doctor` reports only unconfigured settings, and usefully names what the region is for —
*"required for data plane operations (exec, files, ports, egress)"*, which is the first
confirmation from the tool itself that those four exist.

Then `az group create` refused:

```
(AuthorizationFailed) The client 'asantamaria@plainconcepts.com' … does not have authorization
to perform action 'Microsoft.Resources/subscriptions/resourcegroups/write'
```

Effective role, read from RBAC: **`Reader` at subscription scope.** So every read in this file
works and nothing can be created. The spike cannot proceed on this subscription as it stands.

**A second fact, and the more important one.** That subscription holds **34 resource groups** whose
names read as live client environments — `acciona-dev-rg`, `mediamarkt-dev`, `puigbot-dev-rg-01`,
`casabatllo-dev-rg`, `agbar-dev-rg`, `aliseda-dev-rg01` among them — across North Europe and West
Europe. It is a shared company subscription rather than a personal sandbox, whatever its name says.
Even granted the rights, creating a spike's resources there is a decision for its owner to take
deliberately, not a corner for an agent to find. Nothing was created.

Region is not a blocker either way: `sandboxGroups` is offered in both North Europe and West
Europe, where those groups already live.

### H1, packaging half — verified 2026-08-08

`docker build` of `poc/Dockerfile`, then
`docker run --rm aio-spike-aca:local sh -c 'node --version; git --version; opencode --version'`:

```
v22.23.2
git version 2.39.5
1.18.6
```

256 MB, workdir `/workspace`. So an image with everything a Run needs builds and runs locally. What
is **not** verified is whether the platform's disk-image import accepts it, or what it costs to
boot — which is the half that needs the account.

### The `aca` install URL — verified 2026-08-08

`https://aka.ms/aca-cli-install` resolves to
`https://raw.githubusercontent.com/microsoft/azure-container-apps/main/aca-cli/preview/install.sh`,
HTTP 200, 6433 bytes. The CLI exists and comes from Microsoft's own repository. Not installed and
not run.

### No .NET SDK — verified 2026-08-08

GitHub code search over `Azure/azure-sdk-for-net`'s default branch: `SandboxGroup` → **0** results,
`sandbox` in any path → **0**. `sdk/containerapps` holds only `Azure.Provisioning.AppContainers` and
`Azure.ResourceManager.AppContainers`. The same query returns 20 in `azure-sdk-for-python` and 38 in
`azure-sdk-for-js`. Method's limit: code search sees the default branch only.

## Expectations to confirm, from reading — not verdicts

- The `aca` CLI's own reference lists `sandbox exec`, `sandbox shell`, `sandbox fs write`,
  `sandbox port add`, `sandbox egress set|init|apply`, `sandbox mount`, `sandbox snapshot` and
  `sandboxgroup volume`. If that holds under exercise, every member of `IAgentProcessHost` has a
  verb and this substrate is a third implementation of an existing seam.
- A port is Entra-gated unless `--anonymous` is passed, i.e. **not** public by default.
- Credentials attach to the **group** as typed providers, and the preview's types are
  `github-copilot` and `anthropic-claude` — the two this product's runtimes authenticate against.
- Microsoft's own sample `02-coding-agents` runs a coding-agent CLI inside a sandbox with the PAT
  held on the egress proxy and *"the agent process itself runs unauthenticated"* — the same shape
  as sbx's sentinel, and the same sentence this product's transcript already carries. That sample
  says the `claude-code/` flow "will follow", so Claude may not be covered yet.
- Our shape is that sample's `02`, not its `08`: we put the CLI binary inside, not shell commands
  emitted by a harness that stays outside.

## Verdict

**None yet.** The spike has not been run.
