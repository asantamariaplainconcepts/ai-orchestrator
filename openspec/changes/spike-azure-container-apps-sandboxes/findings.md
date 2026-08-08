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
| H5 | A Run's shape survives the lifecycle | **REFUTED in its first half, held in its second** |
| H6 | The economics and the limits fit a Run | **limits held; cost not measured** |
| — | Does this substrate fit `IAgentProcessHost`? | **NO, not as a shell-out — `exec` caps at ~50 s** |

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

### H5 — the first half is REFUTED, and it is the most important result here

**Auto-suspend is on by default, at 600 seconds, mode `Memory`** — read from
`aca sandbox lifecycle show` on a sandbox nobody configured.

Then the test that matters. A process was started inside writing a line every second, the idle
timeout was set to 60 s, and the sandbox was left alone — no data-plane calls, while work
continued inside:

```
t+21s state=Running
t+41s state=Stopped
```

**It suspended while a process was actively running.** "Idle" means no activity *from outside*,
not no work *inside*. A Run under BR-005 may last 30 minutes and an agent can think for ten of them
without the executor calling the data plane — so on default settings a Run would be suspended
mid-thought. Any adoption must set `--auto-suspend disable`, or hold the sandbox open deliberately.
This is the one finding that would have bitten in production and is invisible from the
documentation.

The second half held, exactly as advertised. Resume took **~1 s**, and the process was not
restarted — it continued:

```
gap of 43s between tick 307 (17:27:41) and tick 308 (17:28:24)
process ALIVE   ·   counter continued 307 → 308, not 1
```

"Memory, disk, and all running processes" is true as measured.

### The seam, corrected — `exec` cannot hold a Run, and that changes the answer

The earlier entry said four of five members held. That was measured with commands that finish in
about a second, and generalised — the mistake ADR-0018 exists to stop, made twice in one spike.
Measured properly, with a 60 s idle timeout so suspension could be ruled out separately:

```
sleep 20 → OK  (21 s)      sleep 50 → OK  (51 s)
sleep 30 → OK  (31 s)      sleep 60 → FAILS at 121 s   ← 3 attempts, 3 failures
sleep 40 → OK  (40 s)      sleep 90 → FAILS at 121 s
```

`Error: Network issue — retry policy expired`. There is a hard ceiling between **50 and 60
seconds** on a single `aca sandbox exec`, after which the client retries to ~121 s and gives up.
The sandbox itself stays `Running` throughout — this is the call that dies, not the workload.

`IAgentProcessHost.Run` must hold an agent for up to thirty minutes (BR-005) and stream its output
line by line (#96). **One `exec` cannot do either.** So this substrate is *not* a drop-in third
implementation of that seam the way sbx is.

It is still adoptable, by a different shape — verified here:

```
exec: start the agent detached, writing to a file inside
poll: short execs read the tail        → "work 4" … "work 7", state Running throughout
```

Start detached, poll for output and completion. That works and keeps the sandbox alive, but it is
machinery the sbx path does not need, and it changes what the executor is: not "run a process and
watch it", but "start work, poll it, collect it". Whether `aca sandbox shell` (PTY) or the Python
SDK's streaming holds a longer connection was not tested, and is the obvious next question.

### H6 — limits held at this scale; cost not measured

Four sandboxes ran concurrently in one group with no cap encountered, which comfortably exceeds
this product's per-project concurrency. `aca sandbox stats` reports CPU, memory, network and a 20 GB
root filesystem (1.5 GB used by the `claude` disk). **Cost was not measured** — it needs billing
data over time rather than a session, so it stays open.

### H4's provider half — confirmed at the surface, not exercised

`aca sandboxgroup credential create --help` states the types and their validation:

```
* github-copilot   — token must start with `github_pat_` (classic `ghp_` is rejected)
* anthropic-claude — token must start with `sk-ant-`
```

Both need tokens only a human can mint, so this remains **not verified**.

## Verdict

**Not yet written.** H5 and H6 are unexercised and H4 is half-done, so the recommendation this
spike owes (task 7.1) is not due. What can be said already, and it is a lot:

- The question the spike was created to answer is **answered yes**: a remotely-created sandbox can
  be given a workspace, so the executor no longer has to share a machine with it.
- The substrate does **not** fit `IAgentProcessHost` as a shell-out: `exec` is capped at ~50 s and
  a Run may last thirty minutes. Adoption needs a start-detached-and-poll executor, which is a
  different component rather than a different implementation.
- Two claims in the vendor's own documentation are **false as measured** — deny-default egress, and
  the implication that suspension tracks whether the workload is busy. Both matter for safety and
  both are invisible without exercising.
- What is genuinely better than the current path: prebuilt `claude` and `copilot` disks, typed
  credential providers for both runtimes, an auditable egress decision log, and snapshot/resume
  that actually restores a live process.
- What is worse or unknown: `exec` cannot hold a Run, auto-suspend must be disabled deliberately,
  cost is unmeasured, and session carriage (#288) cannot exist here at all.

### The verdict was framed against the wrong comparison

The paragraph below was written as though this substrate were competing with sbx for one slot. It
is not, and saying so is the most useful thing this spike produced.

**sbx cannot go to the cloud.** Its constraint is co-location: the executor prepares a directory
and the sandbox mounts it, so "sbx in Azure" means operating a VM — one to size, patch, scale and
keep alive — with a worker on it consuming the queue. The cloud story for sbx was always a machine
somebody runs. This substrate has no machine at all: sandboxes are created over an API, from a
process that can be anywhere, and cost nothing idle.

So the two are not alternatives. They are two habitats:

| | Dev loop (a laptop) | A deployment |
|---|---|---|
| Substrate | **sbx** | **ACA Sandboxes** |
| Why | local, free, and the only place #288's session carriage can exist — there is a machine owner whose files can be copied | no VM to operate, no co-location, typed credential providers for both runtimes, scale to zero |
| Credentials | the owner's own session | stored, platform-injected — which is what a deployment needs anyway |

That is what `IAgentProcessHost` was built for: where the CLI runs is a habitat's choice. A cloud
habitat picking a different host is the seam working, not the seam breaking.

**And the `exec` ceiling reads differently in that light.** Holding an open process handle is what
the *local* executor does, and it is fragile in a way that only matters in a deployment: if the
worker restarts mid-Run, the Run dies with it, and nothing retries (BR-004). Start-detached-and-poll
survives that — the sandbox keeps working independently of whoever is watching, and a restarted
worker can pick the Run back up by polling. Combined with suspend and snapshot, a Run can outlive
the process that started it.

So the pattern the ~50 s ceiling forces is the pattern a queue-driven worker should want. It is a
cost against the local design and a fit for the remote one. That does not make the ceiling
harmless — it still has to be built — but it stops being an argument against.

**Recommendation (task 7.1): pursue as the deployment substrate, not as a replacement.** The finding that justifies the work is
H2 — co-location is broken, which is the ceiling the current design has and cannot lift. The
finding that sizes it is the `exec` ceiling: this is a new executor shape, not a new
`IAgentProcessHost`. Anything that follows should decide that shape first, and should not begin
until the cost of a thirty-minute Run is known.

Three things a follow-up must carry, all measured here and none of them in the documentation:
auto-suspend off, the ~50 s `exec` ceiling, and deny-default egress being opt-in.
