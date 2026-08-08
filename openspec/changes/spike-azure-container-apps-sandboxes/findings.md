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
| H1 | Our own image boots and runs an agent | **held, by a shorter route** — a public `claude` disk exists |
| H2 | The workspace reaches a remote sandbox at all | **HELD — co-location is broken** |
| H3 | A port is reachable while the Run lives, and gone after | **held** |
| H4 | A credential reaches the agent without living at rest | **partly** — egress verified, provider tokens not |
| H5 | A Run's shape survives the lifecycle | **not verified** |
| H6 | The economics and the limits fit a Run | **not verified** |
| — | Does this substrate fit `IAgentProcessHost`? | **held for four of five members** |

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

## Exercised 2026-08-08 — subscription `422bb77e-…`, group `aio-spike-es`, region `spaincentral`, `aca 1.0.0-preview.1`

### H1 — held, and by a shorter route than the hypothesis imagined

`aca sandboxgroup disk list-public` offers prebuilt images including **`claude`**, `copilot`,
`dotnet-10`, `python-3.1x` and `ubuntu`, all `Ready`. So an agent CLI does not need our image at
all. Inside the `claude` disk:

```
PRETTY_NAME="Ubuntu 26.04 LTS"
/usr/local/bin/claude   2.1.198 (Claude Code)
/usr/local/bin/git      2.55.0
python3 present · node NOT present · running as root
```

Claude Code ships as a native binary there, newer than the 2.0.44 this repo pins. Sandbox creation
took **5.75 s**; `exec` round-trips in about **1 s**. Our own image (`poc/Dockerfile`, built and
exercised locally) was therefore not needed to answer H1 and remains unimported.

### H2 — HELD. Co-location is broken, and this is the finding the spike existed for

A file written on the calling machine landed inside a sandbox created remotely over an API, with no
directory prepared anywhere and nothing mounted:

```
aca sandbox fs write --id <SBX> --path /workspace/probe.txt --file ./probe.txt
→ Uploaded '…/probe.txt' to '/workspace/probe.txt'.
aca sandbox exec --id <SBX> -c "cat /workspace/probe.txt"
→ prepared by the executor, not by the sandbox
```

The full surface is `fs ls|cat|write|rm|mkdir|stat|cp`, and `cp` is documented as copying **between
local machine and sandbox**, so the Run's output has a way back as well.

**The `--clone` spike's verdict was about sbx, not about microVMs.** The requirement this change
narrows was right to be narrowed.

Separately, a clone from inside also reached GitHub — it failed only on credentials, which is a
private repository behaving correctly, not an egress refusal.

### H3 — held, both halves

Served inside, published, fetched from this machine over the internet, then deleted:

```
aca sandbox port add --id <SBX> --port 8080 --anonymous
→ https://<sandbox-id>--8080.spaincentral.adcproxy.io
curl …/probe.txt  → 200, body "prepared by the executor, not by the sandbox"
aca sandbox delete → curl same URL → 404, and `sandbox list` is empty
```

That is run-previews' contract exactly: reachable while it lives, nothing afterwards. `--anonymous`
is opt-in; omitting it leaves the port Entra-gated.

### H4 — partly, and one documented claim is **false as measured**

The egress policy works and its deny side genuinely denies:

```
default (no policy set):   example.com=200  pypi.org=200          ← wide open
after: egress set --default Deny --rule github.com:Allow
                           example.com=403  pypi.org=403  github.com=200
```

`aca sandbox egress decisions` then returns an auditable log of what was refused, with timestamp,
host, method and path — something sbx does not offer.

**But "deny outbound traffic by default" is not true of a sandbox as created.** The portal page
states it as a property of the platform; measured, a sandbox with no policy set has unrestricted
egress, and `egress show` says "No egress policy configured". Deny-default is *available*, not
default. That is a security-relevant difference between the documentation and the product, and it
is exactly the shape ADR-0018 describes.

Not exercised: the typed provider credentials (`github-copilot`, `anthropic-claude`). They need
tokens only a human can mint, so they stay **not verified**.

### The seam — held for four of five members

| `IAgentProcessHost` | Verb | Verified |
|---|---|---|
| run a command | `aca sandbox exec -c` | yes |
| stream its output | exec returns stdout; PTY via `shell` | stdout yes, streaming not tested |
| the workspace | `aca sandbox fs write` / `cp` | yes |
| published port | `aca sandbox port add` | yes |
| credential boundary | `aca sandbox egress set` | yes, for the policy half |

## Verdict

**Not yet written.** H5 and H6 are unexercised and H4 is half-done, so the recommendation this
spike owes (task 7.1) is not due. What can be said already is that the question the spike was
created to answer — whether a remotely-created sandbox can be given a workspace — is **answered
yes**, and that the substrate fits the seam this product already has.
