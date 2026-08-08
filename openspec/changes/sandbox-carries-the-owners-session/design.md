## Context

Three facts, all recorded rather than recalled.

**The pod substrate already answered this question** (`run-dispatch`, #246 D5): the host's
agent-CLI configuration enters the pod by default, the operator can turn it off, the transcript
names the source either way, and the mechanism was fixed *by observing a real CLI in a pod*. That
requirement also records what the observation cost: opencode keeps credentials in
`~/.local/share/opencode/auth.json`, **not** in `~/.config/opencode` which holds agents and
commands, so both travel — and macOS Docker Desktop can refuse a dot-directory bind outright.

**The sandbox lane deliberately took the opposite side.** `SbxAgentProcessHost.SuppliesCredentials`
is true and `AgentCredentialEnvironment` hands it nothing; the spike proved the value never
exists inside, and yesterday's clone spike proved an agent can still push a branch and open a
pull request holding nothing. That property is worth keeping wherever the repository is somebody
else's.

**The dev loop is now sandboxed by default** (`appsettings.json`, `Parameters:sandbox`), which is
what turns this from a nicety into a gap: the default habitat cannot authenticate the default
runtime.

## Goals / Non-Goals

**Goals:**

- A developer signed into Claude Code can run sandboxed Runs with no `anthropic` secret at all.
- The arrangement is recognisably the pod's, so there is one idea to learn, not two.
- The softening is bounded to the dev loop, structurally and visibly.

**Non-Goals:**

- Session carriage in the server shape or selfhost. Not "later" — deliberately never, unless a
  future change argues it with a different threat model.
- Any change to vendor-PAT handling: github stays proxy-injected.
- opencode Zen accounts, which have no sbx service secret and would forfeit the sentinel property.

## Decisions

### D1 — A copy, not a mount

The pod substrate binds the host's directories read-only. This copies them into the sandbox at
creation instead, for two reasons of different weight.

The small one is mechanical: #246 recorded that macOS can refuse a dot-directory bind outright,
and a mechanism that fails on the maintainer's own machine is not a mechanism.

The larger one is that a copy has a lifetime, and the lifetime is the sandbox's. The sandbox is
already disposed in a `finally` that survives cancellation, so the copied session dies with it —
BR-010's shape ("nothing secret at rest") applied to a thing that is not a secret value but is
exactly as sensitive. A bind would leave the agent writing into the developer's own session
state, which is the failure `--clone` was praised for avoiding one spike ago.

*Alternative rejected — bind read-only, like pods.* Cheaper, and it is what the sibling does, but
it inherits a known macOS failure and gives the agent a live view of the developer's session
rather than a snapshot.

### D2 — The habitat rule is structural, not documentary

Carriage is declared where the dev loop is declared (`AppHostHabitats.DeclareDevLoop`), not read
from a global default that the server shape must remember to unset. `DeclareServerShape` never
sets it, so a habitat cannot acquire the softening by forgetting something.

This mirrors how the sandbox launcher itself is chosen — presence of configuration, never an
environment name (ADR-0010) — and it is the reason the E2E fixture's `Parameters:sandbox = false`
line exists one seam over: dev convenience must never leak into a tier that runs elsewhere.

### D3 — The transcript learns a third sentence, in the seam that already carries it

`AgentRuntimeSelection.CredentialSource` already crosses from composition into the Runs module
precisely so the executor can say where the agent's authority came from. It gains a third value.
Nothing new is plumbed; the sentence composed in `RunExecutor` reads correctly with it because it
was written as a clause, not a fixed phrase (`fix(agents)`, one header with one owner — ADR-0015).

### D4 — The mechanism is observed before it is claimed

#246's requirement says the mechanism SHALL be fixed by observing a real CLI in a pod. The same
applies here and is not optional: the spike's own history is the argument — a broken brew cask, a
mandatory Docker login, and a template with no agent CLI in it, none of which any document
mentioned. Which files a signed-in Claude Code actually needs inside a sandbox is a question for
the machine, not for the documentation.

### D5 — The end-to-end Run is this change's proof, and its target already exists

Two merged changes carry an unverified end-to-end claim because no rehearsal target existed.
ADR-0014 exists for exactly that, the target now exists, and this change is where the debt gets
paid — not as a bonus but as acceptance criterion 7. If it cannot be exercised, the change is not
done; that is the difference between this and the two before it.

## Risks / Trade-offs

- **A carried session is exfiltrable by whatever runs in the sandbox** → bounded to the dev loop
  by D2, stated in the option's own copy, and named in every Run's transcript. This is a real
  softening and the design says so rather than implying the boundary is unchanged.
- **A copy can go stale** — a developer who re-authenticates mid-Run has an old copy inside →
  acceptable: the copy is per Run and a Run is minutes.
- **The observed file set may be wrong or incomplete** → D4 makes observation a task, not an
  assumption; a wrong guess would show up as "Not logged in" exactly as the spike saw.
- **It may simply not work** — a session may be machine-bound in ways a copy cannot carry → then
  the finding is that Claude Code needs an API key under sbx, recorded honestly, and the change
  becomes a smaller one that says so in the readiness panel instead of pretending.

## Migration Plan

Additive and habitat-scoped. A deployment that is not the dev loop is untouched; a developer who
turns carriage off returns to today's behaviour with one key.

## Open Questions

- Whether a carried Claude Code session survives the copy at all, or needs something
  machine-specific. D4's observation answers it before any of this is claimed.
- Whether opencode's session is worth carrying given DEC-044's free default needs no credential.
  Carried anyway for symmetry with the pod set, unless observation shows it costs something.
