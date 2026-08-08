# spike-sandbox-clones-its-own-workspace — proposal

Issue: none (spike) · Investigation · Actors: whoever chooses a habitat for agent execution ·
Touches `agent-sandboxing` and `run-dispatch` · ADR-0010, ADR-0014, BR-010, DEC-062

## Why

One undocumented fact decides every deployment question we have asked this week: **the executor
and the sandbox must share a machine.** The executor prepares the workspace as a local directory
(`RunExecutor` → `workspace.Prepare` → `prepared.Value.Path`) and the sandbox mounts that host
path over virtiofs. Nothing else about the design forces co-location — this does.

It is why "connect the orchestrator to an Azure VM running sbx" has no answer as posed: you
cannot call a sandbox on another machine when the thing it must mount is on this one. The only
shape that works today is moving the whole worker to that VM, which is fine but forecloses
options nobody has priced.

The sbx spike noticed an escape hatch and never tested it: `sbx create --clone` runs the agent
on a **private in-container clone** of the host repository, wired back through a git-daemon. If
that works and the agent's work comes back, the workspace stops being a shared filesystem and
becomes a protocol — and the executor and the sandbox no longer have to be the same machine.

That single question changes what Azure, AKS and selfhost each cost. It is worth a day before
anyone builds a habitat around the assumption.

## What Changes

- **Nothing in the product.** The spike produces evidence and a recommendation in `findings.md`.
- **One spec addition** (below), which is true today regardless of the verdict: the co-location
  constraint is currently invisible to a reader of the specs, and naming it is what lets a
  future change deliberately remove it.
- Hypotheses under test:
  - **H1 — the clone happens**: `--clone` gives the agent a working copy of the host repository
    inside the sandbox, with the branch the executor prepared.
  - **H2 — the work comes back**: commits the agent makes inside reach the host's repository
    (the git-daemon's direction of travel), or they do not — and which it is, exactly.
  - **H3 — credentials still never enter**: the clone and any push authenticate through the
    host-side proxy, with `GITHUB_TOKEN` still empty inside (the property BR-010 rests on).
  - **H4 — the host path stops mattering**: with `--clone`, a workspace that is NOT visible to
    the sandbox at the host's absolute path still produces a usable Run — the actual test of
    decoupling. If it still needs the host path, the escape hatch is imaginary.
  - **H5 — DEC-062 survives**: an agent that publishes its own PR can still do so from inside a
    cloned sandbox, or the change to that promise is named.
  - **H6 — the remote shape is nameable** (desk-check): if H1–H4 hold, what would actually
    carry a Run to a sandbox on another machine — and what would still be missing.

## Out of scope

- Building any remote-execution path. This decides whether one is possible, not how it looks.
- Azure resources of any kind. No VM is created; the cloud question is answered on paper once
  the local physics are known (and the subscription's tfstate is separately broken).
- Changing `ICodeWorkspace`, the executor, or the sbx driver.

## Rehearsal target (ADR-0014)

This spike needs a repository an agent may write to, which is exactly what ADR-0014 says must be
named before the proof depends on it. **Task 1.1 creates it** — a throwaway repository under the
owner's account, used here and left in place, because the end-to-end verification that two
changes now owe is blocked on the same thing.

## Non-breaking

No integration contract is touched. The spike's harness lives inside the change directory, never
under `src/`.

## Capabilities

### Modified Capabilities

- `agent-sandboxing`: state the workspace's locality contract, which today is real but implied.

## Exit criteria

A `findings.md` answering H1–H6 with exercised evidence, and a verdict: either co-location is a
property we choose to keep (and the spec keeps saying so), or it is removable and a named
follow-up change says how.
