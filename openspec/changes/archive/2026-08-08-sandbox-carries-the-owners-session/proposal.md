## Why

Issue [#288](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/288) ·
ACT-001 · UC-012, UC-027 · BR-010, BR-004, BR-011 · Product

In enterprise accounts the API key belongs to the **organisation** — shared, with blurry cost
attribution and bureaucracy to obtain. The session belongs to the **person**. The pod substrate
already took that side (#246 D5): the host's agent-CLI configuration enters the pod by deliberate
default, with an off switch and the consequence stated where the option lives.

The sandbox lane never got it, and the sandbox lane is now the dev loop's default. So today a
developer's `aspire run` puts agents in a microVM where they cannot authenticate the way the
other two lanes do.

**Measured before proposing** (2026-08-08, this machine — the scope below is what the evidence
supports, not what was hoped):

| runtime | where the credential lives | carried by a copy? |
|---|---|---|
| opencode | `~/.local/share/opencode/auth.json` — one 950-byte file | **yes** |
| GitHub Copilot | files under `~/.config/github-copilot/` | **yes** |
| Claude Code on macOS | the **system Keychain**, no file at all | **no** |

Inside a sandbox, with that single opencode file copied in: `opencode auth list` showed both
providers, and `opencode run -m github-copilot/…` answered and then edited a file — the
developer's own **GitHub Copilot seat**, working in a microVM, with no API key anywhere. That is
the value #288 asked for, reached by a route the original proposal did not anticipate.

## What Changes

- **The dev loop's sandbox carries file-based agent-CLI credentials by deliberate default**,
  mirroring the pod substrate: an off switch in one setting, and the consequence stated where the
  option lives — sandboxed Runs act and bill as those sessions.
- **A copy of the credential files, not a mount of the tree.** The minimum set, observed: the
  copy dies with the sandbox, the agent cannot write into the machine's own session state, and
  copying `~/.config/opencode` wholesale would have moved 1.4 GB of caches for nothing.
- **Claude Code on macOS is out of scope, and the product says why.** Its session is in the
  Keychain, so no copy can carry it. The runtimes panel names that and the remedy
  (`sbx secret set -g anthropic`) instead of a developer meeting "Not logged in" inside a
  sandbox with no explanation — which is exactly what the sbx spike recorded and left unverified.
- **Only the dev loop.** The server shape and selfhost keep carriage off: their answer stays
  egress injection, because a carried session is readable by whatever runs in the sandbox and
  those habitats run third-party repositories.
- Not **BREAKING**: with carriage off, behaviour is exactly today's.

### The proof this change owes

Acceptance criterion 7 of #288, now reachable: a Run dispatched **end to end through the
orchestrator** against the ADR-0014 rehearsal target
(`asantamariaplainconcepts/ai-orchestrator-rehearsal`), authenticated by the carried session,
publishing its own branch and pull request (DEC-062), streaming to the Run page (UC-027), leaving
no sandbox behind. This is the debt two merged changes already carry.

## Capabilities

### Modified Capabilities

- `agent-sandboxing`: the credential requirement gains a third arrangement — a carried session —
  alongside injected and passed, with the habitat rule, the copy-not-mount property, and the
  requirement that a runtime whose credential cannot be carried says so rather than failing mute.

## Impact

- **Code**: the sbx driver (copying the observed set at creation), `AgentSandboxComposition` (the
  setting), the AppHost's dev-loop declaration (the default), the credential-source sentence, and
  the runtimes readiness panel (the macOS Claude remedy).
- **Security**: this deliberately softens the boundary the sandboxing change built. Confined to
  the dev loop, stated in the option's own copy, named in every Run's transcript.
- **Tests**: unit coverage for the habitat rule, the off switch, the minimum-set copy and the
  not-carryable remedy; the end-to-end Run is the manual exercise, as CI has no KVM.
