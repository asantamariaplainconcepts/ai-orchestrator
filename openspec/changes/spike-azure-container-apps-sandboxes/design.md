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

### H4 — A credential reaches the agent without living at rest

The documentation claims **both** shapes: an egress proxy that injects credentials at the boundary
— *"never inside the sandbox"*, which is sbx's sentinel model in different words — and secrets
injected as environment variables at boot. Both are workable and they are not the same promise, and
which one a Run uses decides what its transcript must say (#288's third credential source exists
because that sentence has to be true).

Documentation is not evidence (ADR-0001), so this stays a hypothesis: exercise the proxy path and
confirm no value is readable inside.

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

**There is no .NET SDK named.** CLI shell-out is the precedent this codebase already runs on — the
sbx host shells out to `sbx` — so that is not a blocker, but it is a fact the spike records rather
than discovers.

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
