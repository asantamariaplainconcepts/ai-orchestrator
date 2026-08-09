## Context

What a Run needs from a substrate is not a guess any more — this programme has measured it while
building the sbx path:

- an agent CLI (`claude`, `opencode`) running in a full Linux userland, with `git`;
- a workspace the agent writes, reachable at a path the executor and the sandbox agree on;
- egress to a model provider and to GitHub, and nothing wider than a deployment intends;
- a credential that reaches the agent without existing at rest (BR-010);
- optionally a port published for the life of the Run, and gone with it (run-previews);
- a bounded lifetime, because BR-005 kills a phase at its timeout and BR-004 never retries.

sbx meets all six and imposes one thing nobody chose: the sandbox and the executor share a machine.
This spike asks whether ACA Sandboxes meets the six **without** the seventh.

## Goals / Non-Goals

**Goals.** Answer, with observation, whether a Run could execute in an ACA Sandbox driven from
somewhere else. Record what it costs and what it refuses.

**Non-Goals.** Building the runtime. Choosing between substrates — the spike informs that decision
and does not make it. Replacing sbx in the dev loop. Anything about AWS or Hyperlight: Hyperlight
was the question that started this, and reading it settled it (no guest kernel, no OS, no arbitrary
shell — the wrong boundary for a whole agent CLI, the right one for a code-interpreter tool we do
not have).

## Hypotheses

Each is stated so that failing is a result, not a disappointment (ADR-0005, ADR-0013).

### H1 — Our own image boots and runs an agent

A sandbox created from an OCI image containing `opencode`, `node` and `git` starts and runs
`opencode run -m <model> "<prompt>"` to completion.

*Refuted if* the image conversion rejects the layers, the CLI cannot start, or the environment
lacks something a Node CLI assumes.

### H2 — The workspace reaches a remote sandbox at all

**The load-bearing one**, and it is deliberately broader than the first draft of this spike, which
asked only whether the sandbox could clone the repository itself. The portal documentation names a
second mechanism the first draft missed — *"upload, download, and stream files in and out of a
running sandbox over the data plane"* — and the co-location requirement itself already listed three
candidate escapes: a clone the sandbox performs, a shared volume, **a transport**. Asking only
about the clone would have measured one of the three and concluded about all of them, which is the
mistake ADR-0018 exists to stop.

So: a workspace reaches a sandbox created from somewhere else, by any of the three, and the agent
works in it. Which mechanism is viable for a repository-sized tree — and what it costs in time — is
part of the answer, not a detail after it.

*Refuted if* every mechanism requires the caller's own filesystem to be reachable from the sandbox
host, which is the constraint sbx imposes and the whole reason for asking.

Note the second half, which is easy to forget: the Run's **output** has to come back. Today the
agent publishes its own branch and pull request (DEC-062), so the answer may be that nothing needs
to come back — worth confirming rather than assuming.

### H3 — A port is reachable while the Run lives, and gone after

A sandbox declaring a port serves something the portal can reach for the life of the agent, and
after the sandbox ends there is nothing — not an error page, not a stale route. That is the
contract run-previews already fixed: while alive it is reachable, once finished there is nothing,
not even the option.

*Refuted if* exposure is public-by-default, or survives the sandbox, or requires ingress this
product would have to operate.

### H4 — A credential reaches the agent without living at rest, and the platform may already know our runtimes

The documentation claims **both** shapes: an egress proxy that injects credentials at the boundary
— *"never inside the sandbox"*, which is sbx's sentinel model in different words — and secrets
injected as environment variables at boot. Both are workable and they are not the same promise, and
which one a Run uses decides what its transcript must say (#288's third credential source exists
because that sentence has to be true).

**And a third-party report says the preview goes further than generic secrets.** A write-up by
Tamir Dresher (2026-06-12), from someone driving the preview CLI rather than reading its
announcement, records that credentials attach to the **sandbox group** as typed provider tokens,
and that the types the preview exposes are **`github-copilot` and `anthropic-claude`** — which are
exactly the two this product's runtimes authenticate against. It also reports that the Copilot
credential validates a fine-grained `github_pat_…` prefix and rejects classic `ghp_…` tokens.

If that holds, the credential story on this substrate is not a downgrade from the dev loop's — it
is a different and arguably better one, because the platform injects a typed provider token and no
value is baked into an image. Note the irony worth recording: the same write-up names "copy my host
machine's token store into the sandbox" as the wrong shape, and that is precisely what #288 does —
correctly, for a laptop. Two habitats, two right answers.

Documentation and blog posts are not evidence (ADR-0001), so all of this stays a hypothesis:
exercise the provider path for both types and confirm no value is readable inside.

Also confirm the negative: **session carriage is impossible here.** #288 copies the machine
owner's credential files into the sandbox, and there is no machine owner on a remote host. A
deployment on this substrate needs real stored credentials, which is a scope fact worth writing
down rather than discovering.

*Refuted if* egress policy cannot express deny-all-plus-allow, or a credential can only be supplied
by baking it into the image.

### H5 — A Run's shape survives the lifecycle

Idle-suspend and snapshot/resume are the platform's headline features and this workload is unusual
for them: an agent can think for minutes with no I/O at all. Does an idle timeout suspend a working
agent? Does resume actually continue a running process, as "memory, disk, and all running
processes" claims?

*Refuted if* a quiet agent is suspended mid-Run, or resume does not restore a live CLI.

### H6 — The economics and the limits fit a Run

What a 30-minute Run costs, and whether a Sandbox Group's maximum sandbox count collides with this
product's per-project concurrency cap.

*Refuted if* preview quotas cannot express the cap the product already has.

## What the documentation already suggests, and why it is not the answer

Read before writing these hypotheses: the portal's own summary describes an egress proxy with
deny-default and credential injection, exec with streamed stdout/stderr, file upload and download
over the data plane, HTTP ports *"for inbound connections and previews"*, and control from a CLI,
a Python SDK or MCP.

Two consequences worth stating now.

**The shape fits a seam this product already has.** `IAgentProcessHost` is command, arguments,
workspace, environment, a timeout, a line callback and an optional published port. Exec-with-stream
plus ports plus a CLI to shell out to is that interface, which means adopting this substrate would
plausibly be a third implementation of an existing seam rather than a new architecture. The spike
should confirm or deny that, because it is the difference between a change and a programme.

**The CLI is its own surface, and it is not `az`.** Reported install:
`curl -fsSL https://aka.ms/aca-cli-install | sh`, then `aca auth login`, with an `aca doctor` for
preflight. `aca sandboxgroup create` is reported to auto-assign the caller the data-owner role
unless opted out. Egress is set per sandbox — `--egress-default Deny --egress-rule "github.com:Allow"`
— and can be changed on an existing sandbox with `aca sandbox egress set`, which is deny-all plus
allow list, the shape sbx already gives this product. Disk images come either from public prebuilt
ones (`--disk copilot`) or private ones (`--disk-id`).

All of it is preview surface, and the same write-up says to re-read `--help` before automating any
of it. The spike records the flags it actually ran, not the ones it read.

**There are SDKs, and the interesting question is not whether but which client.** The docs offer
Bash, PowerShell and an SDK tab whose contents are **Python**
(`azure.containerapps.sandbox`, on PyPI at `0.1.0b3`); a JavaScript package exists too
(`@azure/containerapps-sandbox`, `1.0.0-beta.1`). Both are beta.

The shape that matters is the split. There are **two** clients: a control plane
(`ContainerAppsSandboxManagementClient`) that creates and deletes groups over ARM, and a **data
plane** (`SandboxGroupClient`) that owns sandboxes, disk images, snapshots, volumes, secrets, ports,
egress and **files**. Everything this product would do at Run time lives in the second one.

**For .NET there is nothing, and this was measured rather than assumed.** Searched 2026-08-08 over
`Azure/azure-sdk-for-net`'s default branch: `SandboxGroup` returns **0** results and `sandbox` in
any path returns **0**; `sdk/containerapps` holds only `Azure.Provisioning.AppContainers` and
`Azure.ResourceManager.AppContainers`. The same query returns 20 hits in `azure-sdk-for-python` and
38 in `azure-sdk-for-js`. So the ARM package does not even carry a `SandboxGroup` type yet, let
alone a data-plane client — and the data plane is where exec, files, ports, egress and secrets
live, which is everything a Run does.

*Method and its limits, so a later reader knows what this is worth:* GitHub code search over the
default branch. A package on an unmerged branch, or shipped to NuGet from elsewhere, would not
appear. Re-check before treating the gap as permanent.

That makes the CLI path not a preference but the realistic one, and it is the shape this codebase
already runs: `SbxAgentProcessHost` shells out to `sbx` today. The open half is whether `aca`
covers the **data plane** — exec with streamed output, file transfer, ports — or only group
management. If it does, adopting this substrate is a third implementation of an existing seam. If
it does not, the answer is raw REST from C#, and the follow-up is larger than a seam.

None of this counts as an answer. ADR-0001: a claim is exercised or it is a hypothesis, and a
vendor's summary of its own preview is exactly the kind of claim that reads settled and is not.

## Method

A scratch harness, not repository code — the same shape the sbx spike used, so nothing half-built
lands in the product before a decision exists. Each hypothesis gets its verbatim observation
recorded in `tasks.md`, including the ones that fail, and including the exact preview version and
date so a later reader knows what the answer was true of.

## Risks / Trade-offs

**A public preview moves.** Anything measured here has a shelf life, which is why the date and the
region are part of the record.

**Cost.** Sandboxes cost nothing when idle, per the announcement; the spike still deletes its
resource group at the end and says what it spent.

**The interesting answer is the expensive one.** If H2 holds, the deployment design opens up and
this programme has more to build, not less.

## Open Questions

- Does driving this from the portal process mean holding an Azure credential in the portal, and is
  that a shape this product wants at all?
- If H2 holds, does the executor still have a job, or does it become a thing that creates sandboxes
  and reads their transcripts?
