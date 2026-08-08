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

### H2 — The workspace is created inside, and that is what breaks co-location

**The load-bearing one.** A sandbox clones the repository itself, over its own egress, with a
credential the sandbox holds — and the executor never prepares a directory. If that works, the
executor no longer has to be on the sandbox's machine, and the `--clone` spike's verdict was about
sbx rather than about microVMs.

*Refuted if* the sandbox cannot reach GitHub, cannot be given a usable credential, or the model
requires a volume the caller must populate from its own filesystem.

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

Whether there is a host-side injection like sbx's egress proxy, or whether values travel in the
sandbox's environment. Either is workable — the in-process lane already passes values for a
process's lifetime — but which one it is decides what the transcript must say (#288's third
credential source exists because that sentence has to be true).

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
