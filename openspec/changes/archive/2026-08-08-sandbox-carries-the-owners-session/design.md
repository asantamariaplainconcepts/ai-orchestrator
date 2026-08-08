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

- A developer signed into a **file-credentialled** CLI — opencode today, Copilot when #243 lands
  — runs sandboxed Runs on their own seat, with no API key anywhere.
- A runtime whose credential **cannot** be carried says so with its remedy, rather than meeting
  the developer as "Not logged in" inside a microVM.
- The arrangement is recognisably the pod's, so there is one idea to learn, not two.
- The softening is bounded to the dev loop, structurally and visibly.

**Non-Goals:**

- Session carriage in the server shape or selfhost. Not "later" — deliberately never, unless a
  future change argues it with a different threat model.
- Any change to vendor-PAT handling: github stays proxy-injected.
- opencode Zen accounts, which have no sbx service secret and would forfeit the sentinel property.
- Carrying Claude Code's macOS session by any means, including extracting it from the Keychain
  (D4 says why that is the wrong trade rather than merely hard).

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

**The copy is staged, and had to be.** Observed 2026-08-08: `sbx cp` preserves the *host's* uid
and mode, so a 0600 credential owned by uid 501 arrives inside the sandbox still 0600 and still
owned by 501. The sandbox user cannot read it and cannot chown it either, and the CLI then
reports "0 credentials" from a file that is demonstrably present — carriage appearing to work and
to fail at the same time. So the file goes in through a 0644 copy in a 0700 host directory, is
re-created *by* the sandbox user with `cp` inside, and is returned to 0600 there. Note what this
says about D4's method: copying by hand as the machine owner and the server copying on the
owner's behalf are different acts, and only the second one hits this.

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

### D4 — The mechanism was observed, and it decided the scope

#246's requirement says the mechanism SHALL be fixed by observing a real CLI. That observation
happened before this design was written, and it removed a runtime from the change:

- **opencode** keeps everything in `~/.local/share/opencode/auth.json` — 950 bytes. Copied into a
  sandbox on its own, `opencode auth list` showed both configured providers and
  `opencode run -m github-copilot/claude-haiku-4.5` answered and then edited a file. The
  developer's GitHub Copilot seat, working in a microVM, no API key anywhere.
- **GitHub Copilot** keeps files under `~/.config/github-copilot/`, so the same approach applies
  when its runtime lands (#243).
- **Claude Code on macOS** keeps its credential in the **system Keychain**. There is no
  `~/.claude/.credentials.json`; copying `~/.claude` and `~/.claude.json` into a sandbox produced
  `Not logged in · Please run /login`, verified rather than assumed.

Two things follow. The set copied is the **credential file**, not the tree — `~/.config/opencode`
is 1.4 GB of caches that buy nothing. And a runtime whose credential cannot be carried must say
so (D6) rather than failing mute, which is precisely what the sbx spike hit.

*Deliberately not done — extracting the Keychain item and writing it into the sandbox as a
credentials file.* It would probably work, and it is the wrong trade: it converts a
Keychain-protected token into a plain file inside a sandbox, which is worse than the API key it
would replace. The premise of this whole change is that a session is safer than a shared key;
defeating the Keychain would make that premise false.

### D5 — The end-to-end Run is this change's proof, and its target already exists

Two merged changes carry an unverified end-to-end claim because no rehearsal target existed.
ADR-0014 exists for exactly that, the target now exists, and this change is where the debt gets
paid — not as a bonus but as acceptance criterion 7. If it cannot be exercised, the change is not
done; that is the difference between this and the two before it.

### D6 — A runtime that cannot be carried says so, where readiness already speaks

Claude Code on macOS will meet a sandbox with no session. The readiness panel already reports
per-runtime state with a copyable remedy (#279) and already reports it from the machine the CLI
will run on (the sandboxing change's D6), so it is where this belongs: not-ready with the reason
(the Keychain) and the fix (`sbx secret set -g anthropic`).

This is the half of the change that survives even if carriage itself were dropped, and it is what
turns the sbx spike's unverified box into an answer.

## Risks / Trade-offs

- **A carried session is exfiltrable by whatever runs in the sandbox** → bounded to the dev loop
  by D2, stated in the option's own copy, and named in every Run's transcript. This is a real
  softening and the design says so rather than implying the boundary is unchanged.
- **A copy can go stale** — a developer who re-authenticates mid-Run has an old copy inside →
  acceptable: the copy is per Run and a Run is minutes.
- **The observed set may drift when a CLI changes where it stores things** → the set is named in
  one place and the failure is loud (`auth list` shows nothing, the agent says not logged in),
  not silent.
- **A developer may expect Claude Code to work in the sandbox and find it does not** → D6 exists
  for exactly that: the panel names the Keychain and the remedy before a Run fails.
- **Carrying a Copilot seat means Runs consume that seat's quota** → true, and it is the point;
  the transcript names the source so a surprising bill is diagnosable from the Run's own record.

## Migration Plan

Additive and habitat-scoped. A deployment that is not the dev loop is untouched; a developer who
turns carriage off returns to today's behaviour with one key.

## Open Questions

- Whether Claude Code on **Linux** — where the credential is a file — would be carried by the
  same mechanism. Almost certainly yes, and deliberately unclaimed: there is no Linux machine here
  to exercise it on, and shipping an unexercised claim is what ADR-0001 forbids.
- Whether the pod substrate should adopt the same minimum-set copy, since it currently binds
  1.4 GB of opencode tree read-only for the sake of one file. Out of scope; worth its own look.
