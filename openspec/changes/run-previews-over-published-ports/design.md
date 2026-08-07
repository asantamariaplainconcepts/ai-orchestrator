## Context

Three facts decide this design, and all three are measured rather than assumed.

**One — the mechanism works.** Verified on this machine (2026-08-07, recorded in the sandboxing
change's `evidence.md`): omitting the host port from `sbx run -p 8000` allocates an ephemeral
host port bound to loopback (`127.0.0.1:49152 → 8000`), and `curl` from the host retrieved a page
served inside the sandbox. Two constraints found while proving it: `-p 0:8000` is rejected
(`port 0 out of range`) — omission is the ephemeral form — and the server inside must bind
`0.0.0.0`, not `127.0.0.1`.

**Two — the sandbox dies with the agent.** The sandboxing change's D3 disposes the sandbox in a
`finally` that survives cancellation, and this change does not touch that. Everything below
follows from accepting it rather than working around it.

**Three — the portal already has a ledger shaped like this.** `AgentPodsHost` keeps its record in
memory with the reason written down: "a table would outlive the pods it describes and lie after
a restart." A preview record has exactly that property.

## Goals / Non-Goals

**Goals:**

- A Member can look at the running change while the Run is executing.
- The preview's life is the sandbox's life, with no mechanism anywhere that could extend it.
- A Run that is not executing offers no preview affordance at all.
- Content an agent wrote cannot act with the portal's authority.

**Non-Goals:**

- Previews after a Run ends, snapshots, or recordings.
- Previews for the pod substrate or in-process execution.
- Sharing a preview with anyone not already on the machine.

## Decisions

### D1 — The preview is a property of an executing Run, not an artifact of a finished one

The user's constraint, and the design's spine: *while it is alive we can reach it; when it has
finished there is nothing, not even the option*. Every other decision is downstream.

Consequences taken deliberately: no durable record, no "preview expired" state to render, and no
UI branch that reasons about a preview that used to exist. The Run detail asks one question —
does this Run have a live preview **right now** — and a `Succeeded` Run answers no for the same
reason a Run that never published does.

*Alternative rejected — keep the sandbox alive for a grace period after the agent exits.* It
buys a preview you can open after the fact, and costs the property that makes the sandbox model
safe: a sandbox with no agent running in it is a machine nobody is accounting for, holding a
workspace and a network allowance. It also contradicts D3 of the sandboxing change, which would
then need a caveat rather than staying a rule.

### D2 — The record lives in the launcher's memory, beside the pods ledger

Same shape and same justification as `AgentPodsHost`: the launcher writes a sighting when it
publishes and removes it when the sandbox goes. A restart loses the record, which is correct —
the sandboxes are gone too.

The Run detail therefore reads the preview from the machine's snapshot, not from the Run row.
This also gives the honest answer for free in a queue habitat: a portal that is not the process
holding sandboxes has no such record, and says previews are not available here rather than
implying the Run failed to make one.

### D3 — The agent decides there is something to serve; the launcher decides the port

Only the prompt knows whether a change is runnable and how to start it. So the Automation's
prompt starts the app, and the launcher publishes the sandbox port named in configuration for
that Automation. The launcher does not guess a port and does not wait for a server to appear:
it publishes the mapping, and until something inside listens, the proxy answers "nothing is
serving yet" — a state, not an error.

*Alternative rejected — the launcher detects a listening port and publishes reactively.* It
means polling the sandbox's network state on a cadence, and it makes the preview's existence
depend on timing rather than on configuration. Naming the port is the same "asked, never
inferred" discipline as every other habitat contract (ADR-0010).

### D4 — The portal relays, and the relayed origin is not the portal's

A loopback port is unreachable from a browser, so the Server proxies. That makes this the first
time the product serves bytes an agent wrote, and the isolation must be structural rather than a
promise:

- The preview is framed with a restrictive `sandbox` attribute — scripts may run so the app is
  usable, but same-origin access to the portal is not granted, so the framed document cannot
  read the session or call the API as the Member.
- The proxy is scoped to one Run's published port and refuses everything else; it never becomes
  a general-purpose relay to arbitrary hosts.
- Authorization is the Run's own: whoever may see the Run may see its preview, decided at the
  proxy, not in the browser.

**The risk being accepted, named:** a Member's browser renders agent-authored HTML and script.
The sandbox attribute confines it, but a preview is not a safe place to type a password, and the
UI says whose code it is running.

### D5 — Streaming and preview are siblings, not the same feature

A Run's output already streams while it executes. The preview joins it as the second thing on
the Run detail that is live rather than recorded, and both disappear the same way. Wording and
placement should make them read as one idea — "this Run is happening now" — rather than as two
unrelated panels.

## Risks / Trade-offs

- **Agent-authored content in the Member's browser** → framed with a restrictive sandbox, proxy
  scoped to one Run, authorization at the proxy, and copy that names what is being rendered.
- **A preview that vanishes mid-look** (the Run ends while someone watches) → the frame reports
  the Run reached its terminal state and offers the diff, rather than showing a broken page. The
  transition is the expected case, not an error.
- **A habitat where the portal is not the sandbox host** → previews are unavailable and say so,
  in the same voice the pods panel already uses for "not hosted here".
- **A long-lived server inside a sandbox holds the slot** → the sandbox's own timeout (BR-005)
  already bounds it; nothing new is needed, but the preview must not be a reason to raise it.
- **Port exhaustion under concurrency** → ephemeral allocation plus one port per executing Run,
  bounded by the same concurrency cap that bounds sandboxes.

## Migration Plan

Additive. An Automation without a preview port configured behaves exactly as today; no Run
gains a preview by default, and removing the configuration removes the feature with no residue,
because nothing was ever persisted.

## Open Questions

- **Whether the preview port belongs to the Automation or the Project.** The Automation knows
  the action; the Project knows the app. Leaning Automation, since two Automations on one
  repository may run different things.
- **Whether a preview should survive a Run's cancellation for a moment** so a Member who cancels
  can see the last state. Current answer: no — cancellation is terminal and terminal means
  nothing, per D1. Worth revisiting only if real use asks for it.
