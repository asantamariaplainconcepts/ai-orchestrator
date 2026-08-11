## Context

Everything this needs exists in some form, which is why the slice is small:

- **A registry with the right lifetime.** `RunPreviewHost` is an in-memory `runId → port` map,
  written beside sandbox creation and removed in the same `finally`, with a comment explaining why a
  stored row would lie after a restart. A terminal needs `runId → sandbox name` with identical
  lifetime.
- **A surface that authorizes itself.** `RunLogHub.Watch` re-asks the two questions the CQS
  decorator asks, in the same order, against the same table, because a hub dispatches nothing and so
  the decorator never sees it. That reasoning applies verbatim here — and more sharply, because this
  surface writes.
- **A measured transport.** The spike
  (`openspec/changes/archive/2026-08-10-close-opn-007-live-agent-session/poc/`) drove xterm.js over a
  WebSocket to a pty into a live sandbox: signals, geometry, full-screen programs. It also measured
  the two constraints below.
- **A habitat that answers honestly.** `RunPreviewHost.Hosted` keeps *not hosted here* distinct from
  *this Run has none*, because collapsing them makes a habitat's limitation look like a failed Run.

Two facts from the spike bound the design. `sbx exec -it` **refuses a redirected pipe** — it dies
with `ERROR: inspect exec: context deadline exceeded`, not a tty-shaped message — so a pty must be
allocated on the host before sbx is spawned. And `script`, which the spike used, cannot propagate a
window size: the sandbox pty stayed `0x0` until the spike ran `stty` inside it, which fixes the size
at connect time only.

## Goals / Non-Goals

**Goals:**

- A caller holding `run.attach` gets a real shell in an executing self-host Run's sandbox, with
  signals and live resize.
- The agent's record is untouched: it keeps running headless, and its structured stream stays the
  only thing the transcript renders.
- Every refusal names its own reason — no permission, no terminal in this habitat, no Run.

**Non-Goals:**

- Typing into the agent's own process (#308).
- Preventing a human and an agent from writing the same working tree. Named as a consequence, not
  solved here.
- A recording of everything typed. Criterion 6 records *that* an attach happened.
- Holding a sandbox open after its Run ends — see D4, which is the design's most consequential
  finding.

## Decisions

### D1 — `RunSandboxHost`, copied from `RunPreviewHost` rather than invented

In memory, `runId → sandbox name`, written beside `_lifecycle.Create` and removed in the same
`finally` that already calls `_previews.Gone`. A `IRunSandboxMonitor` contract in `BuildingBlocks`
mirrors `IRunPreviewMonitor` so the Runs module can read it without seeing the sbx host.

*Rejected:* persisting the name on the `Run` row. `RunPreviewHost`'s own comment says why — a stored
row outlives the sandbox it describes and lies after a restart. The sandbox's name has exactly the
property that it exists while the sandbox does and not one moment longer.

### D2 — SignalR, not a raw WebSocket

`/hubs/run-terminal`, alongside `/hubs/run-log`. `Open(runId, cols, rows)` starts the pty,
`Send(runId, data)` carries keystrokes, `Resize(runId, cols, rows)` carries geometry, and the server
pushes `output` frames back.

*Why:* the authorization pattern this surface needs is already written and tested in `RunLogHub`;
reconnect, negotiation and the client are already dependencies; and the frontend already
dynamic-imports `@microsoft/signalr` in `useRuns.ts`.

*The cost, stated:* SignalR's JSON protocol base64-encodes `byte[]`, so output carries ~33%
overhead. Irrelevant for typing, not irrelevant for a build log scrolling past. The escape hatch is
the MessagePack protocol, which is a registration change rather than a redesign — and it is
deliberately **not** taken now, because a raw WebSocket would mean hand-rolling the authorization
that `RunLogHub` already proved.

### D3 — A pty from a P/Invoke, not a package

No pty package is pinned in `Directory.Packages.props` today. `Pty.Net` exists but its purpose is
cross-platform terminal hosting, and this code runs **only** in the self-host habitat — a
developer's own macOS or Linux machine, because sbx is Docker Sandboxes. A Unix-only
`openpty(3)` + `ioctl(TIOCSWINSZ)` P/Invoke is a few dozen lines against libc, satisfies criterion
8, and adds no dependency to a solution whose package list is centrally governed.

*Verify before committing to it* (task 2.1): that `openpty` resolves on both macOS and Linux from
.NET 10, and that the child's controlling terminal is set correctly — otherwise fall back to
`Pty.Net` and pin it. The spike proved the shape with `script`; it did not prove this.

### D4 — The terminal borrows the Run's lifetime, so nothing is held — and this contradicts two of #304's criteria

**The finding.** In this slice the agent stays headless and the sandbox's life is exactly what it is
today: created for the agent's exec, disposed in the `finally` when that exec returns. A human
attached beside it extends nothing. Therefore:

- **No inactivity bound exists to implement.** DEC-065 authorises one, and it is needed only by a
  slice that keeps a sandbox alive *past* its Run so a human can keep working — which #304 explicitly
  refuses in criterion 7 ("a finished Run offers nothing").
- **#304's criteria 4 and 5** — an inactivity bound reclaiming the sandbox, and the `aio-*` startup
  sweep reclaiming a held one — describe machinery this slice does not add. They are satisfied by
  the lifecycle that already exists (`ReapAbandoned` already claims the namespace), not by new code.

This is an artifact mismatch, not a scope cut: the criteria were written when #304 still carried both
attachment forms and the possibility of holding a sandbox. Resolving it is a spec-review question,
recorded in Open Questions rather than decided here.

**What replaces the bound:** a human's terminal simply dies with the Run, and the surface says so.
That is strictly simpler and strictly more honest than a timer.

### D5 — Three refusals, each with its own reason

Following `RunPreviewHost.Hosted`'s distinction rather than collapsing it:

| Situation | Answer |
|---|---|
| Caller lacks `run.attach` | refused — decided in the hub, server-side, never by hiding a control |
| Habitat's launcher is not sbx | *no terminal is hosted here* — the ACA and local hosts answer this |
| Run is not executing | nothing offered at all — no disabled affordance |

### D6 — The human's bytes never enter the Run log

The pty's output goes to the attached client and nowhere else. Interleaving it into the Run log would
put ANSI escapes into the stream `transcript.ts` parses, degrading the steps #299/#300 built into
`raw` lines — which is exactly the cost DEC-065 accepted for #308 and which this slice is defined to
avoid. What is recorded is the attach event (criterion 6): who, and when.

### D7 — One attach per Run at a time

A second concurrent attach is refused, naming the reason. #304 puts multiple humans in one sandbox out
of scope, and two shells in one workspace is a coordination problem this slice does not own. The
refusal is cheap now and reversible later; allowing it silently would not be.

## Risks / Trade-offs

- **A human and the agent write the same working tree** → not preventable at this layer; named in the
  ADR and in #304. The Run's outcome may reflect a human's edit, and criterion 6's attach record is
  what makes that traceable at all.
- **A Member spends the machine owner's session** (#288) → accepted on #304 with the grant given to
  both bundles. Mitigated only by the attach record, deliberately, because the alternative was
  withholding the capability from Members.
- **A pty leaks if a connection drops without `OnDisconnectedAsync` firing** → the child must be
  killed with `entireProcessTree: true` (the spike's harness does exactly this, because killing
  `script` alone orphaned the sbx CLI), and the sandbox's own disposal is the backstop.
- **Base64 output overhead** (D2) → accepted, with MessagePack as a known escape hatch.
- **`openpty` P/Invoke unverified on both platforms** (D3) → gated by task 2.1 with `Pty.Net` as the
  named fallback.

## Migration Plan

Nothing to migrate: no schema change, no message-contract change. The registry is inert without a
surface reading it, so the change can land back-to-front (registry, then permission, then hub, then
UI) with each step harmless on its own. Rollback is removing the surface.

## Open Questions

- **#304's criteria 4 and 5** (D4) — amend the issue to describe the lifetime this slice actually
  has, or keep them as statements about the existing lifecycle that this change verifies rather than
  builds? Needs a decision at spec review, and it is the one thing here that changes what "done"
  means.
- **Does the attach record belong on the Run's transcript or beside it?** Criterion 6 says it is
  recorded; where it surfaces (a log line, a detail field, an event) is a smaller call left to
  implementation.
- **`Pty.Net` or the P/Invoke** — decided by task 2.1's measurement, not by preference.
