## Why

Issue [#288](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/288) ·
ACT-001 · UC-012, UC-027 · BR-010, BR-004, BR-011 · Product

In enterprise accounts the API key belongs to the **organisation** — shared, with blurry cost
attribution and bureaucracy to obtain. The session belongs to the **person**. The pod substrate
already took that side: the host's agent-CLI configuration enters the pod by deliberate default,
with an off switch and the consequence stated where the option lives (#246 D5, `run-dispatch`).

The sandbox lane never got it, and the sandbox lane is now the dev loop's default. The sbx spike
recorded `claude` answering "Not logged in" inside a sandbox and closed with "claude headless
auth ergonomics under sbx" as its one unverified box. So today a developer's `aspire run` puts
agents in a microVM where the product's first runtime (DEC-012) cannot authenticate the way the
other two lanes do — it wants an API key the developer may not be able to get.

## What Changes

- **The dev loop's sandbox carries the owner's session by deliberate default**, mirroring the pod
  substrate: the observed set (`~/.claude`, `~/.config/opencode`, `~/.local/share/opencode`)
  re-observed rather than assumed, one key to turn it off, and the consequence stated where the
  option lives — sandboxed Runs act and bill as those sessions.
- **A copy, not a bind.** The sandbox receives its own copy, so an agent cannot alter the
  developer's session state, and the dot-directory bind failures #246 already hit on macOS do not
  apply.
- **The transcript gains a third credential source.** The runtime header already names which
  credential was chosen and whether its value reaches the agent; it learns to say "the owner's
  session, carried into the sandbox".
- **Only the dev loop.** The server shape and selfhost keep carriage off: their answer stays
  egress injection, because a session inside a sandbox is exfiltrable and those habitats run
  third-party repositories.
- Not **BREAKING**: with carriage off, behaviour is exactly today's.

### The proof this change owes

Acceptance criterion 7 of #288 is the definitive one, and it is the debt two merged changes
already carry: a Run dispatched **end to end through the orchestrator** against the ADR-0014
rehearsal target (`asantamariaplainconcepts/ai-orchestrator-rehearsal`), publishing its own
branch and pull request (DEC-062), streaming to the Run page (UC-027), leaving no sandbox behind.

## Capabilities

### Modified Capabilities

- `agent-sandboxing`: the credential requirement gains a third arrangement — a carried session —
  alongside injected and passed, with the habitat rule and the copy-not-bind property.

## Impact

- **Code**: the sbx driver (copying session state at creation), `AgentSandboxComposition` (the
  key), the AppHost's dev-loop declaration (the default), and the credential-source sentence.
- **Security**: this deliberately softens the boundary the sandboxing change built — a session
  inside a sandbox can be read by whatever runs there. Confined to the dev loop on purpose, and
  the design says so rather than leaving it implied.
- **Tests**: unit coverage for the habitat rule, the off switch and the copy property; the
  end-to-end Run is the manual exercise, as CI has no KVM.
