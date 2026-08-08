## Context

Explored 2026-08-08, following the sbx spike and the two changes it licensed. The question came
from asking whether sbx is viable on an Azure VM and how the orchestrator would reach it — and
the answer turned out to be blocked on something more basic.

What the code actually does today:

```
RunExecutor (worker process)                    sandbox (microVM)
  workspace.Prepare(...) ──► /tmp/aio/run-x ──► mounted at /tmp/aio/run-x (virtiofs)
        clone, branch, PAT                            agent reads and writes here
```

The path is the same string on both sides — the sbx driver even asserts it before running
(`VerifyWorkspace`). That assertion is right, and it is also the constraint: a sandbox on
another machine has nothing to mount.

Nobody chose this; it fell out of the sandbox being local. That is precisely why it deserves
testing rather than inheriting.

## Goals / Non-Goals

**Goals:**

- Learn whether `--clone` removes the shared-filesystem requirement, and if it does not, learn
  exactly where it stops.
- Learn whether the credential property survives that arrangement, because a decoupling that
  costs BR-010 is not a decoupling worth having.
- Leave a rehearsal target behind, so the end-to-end verification two changes owe stops being
  blocked (ADR-0014).

**Non-Goals:**

- Any remote execution path, any Azure resource, any change to the executor or the driver.
- Deciding the Azure habitat. That decision wants this answer first, not instead.

## Decisions

### D1 — The decoupling test is H4, and it must be able to fail

H1–H3 are mechanics; H4 is the question. It is easy to run `--clone` alongside a mounted
workspace, see the agent work, and conclude the mount is unnecessary — while the mount is
quietly doing the work. So H4 hands the sandbox a workspace path **the host does not expose**,
and a Run that still succeeds is the only evidence that means anything.

If H4 cannot be arranged, the spike says so and H1–H3 are recorded as mechanics without a
conclusion. A partial answer stated plainly beats a conclusion the evidence does not carry
(ADR-0005).

### D2 — The spec addition is made now, not after the verdict

`agent-sandboxing` gains a requirement naming the locality contract. Written before the spike
runs, deliberately: it is true today, and a spec that only records constraints somebody
remembered to write is a spec that hides its assumptions. If the spike shows the constraint is
removable, a follow-up change modifies the requirement — which is the mechanism working, not a
mistake being corrected.

*Alternative rejected — wait and let the verdict decide whether to write it.* That leaves the
single most load-bearing fact about deployment undocumented for as long as the question stays
open, which is exactly when someone would design against it.

### D3 — The rehearsal target is created here

ADR-0014 says a change whose proof needs a real Run names its target before implementation. This
spike needs one, so it makes one — a throwaway repository, named in `tasks.md` as its first
task. Two other changes are waiting on the same thing, so the cost is paid once and shared.

The credential is the owner's existing GitHub secret already stored in sbx's keychain; nothing
new is granted, and the target is disposable by construction.

### D4 — Evidence discipline, unchanged

ADR-0001: every verdict carries the command and the observed output. A hypothesis that could not
be exercised reads **not verified**, never inferred from documentation. The sbx spike's own
history is the argument — it found a broken brew cask, a mandatory Docker login and an empty
template, none of which any document mentioned.

## Risks / Trade-offs

- **`--clone` may be a dev-loop convenience rather than a transport** → that is the finding, and
  it is worth the day either way; the alternative is designing an Azure habitat on a guess.
- **A cloned sandbox may break DEC-062** (the agent publishes its own work) → H5 names it rather
  than discovering it during a habitat change.
- **The rehearsal repository is a real repository an agent may write to** → throwaway by
  construction, owner's account, and nothing else is granted.
- **Answering "decoupled" would invite a bigger design than we have appetite for** → the exit
  criteria stop at naming the follow-up, not at sketching it.

## Open Questions

- Whether the git-daemon carries work **back** or only **out**. H2 is written to find out rather
  than assume, because the sbx documentation describes the direction loosely.
- Whether a decoupled arrangement could keep the diff-reading surfaces working — the Run's file
  changes are read from the vendor today, so possibly yes, but it is not in scope to confirm.
