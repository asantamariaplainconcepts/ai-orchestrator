## Context

`IRunDispatcher` is one method taking a Run id — the whole message, deliberately, so the worker
reads Run, Story and Automation from the database. That seam is why a second substrate is a
composition change rather than a redesign: nothing in any module learns which one it got.

The Server already composes everything needed to execute a Run — `AddAgentRuntime()`,
`AddCodeWorkspace()`, the executor, the reaper, the resume checker — and says why in its own
comment: the runtime is there because the executor depends on the seam and DI validation demands
it exist. **That is also the danger.** Today nothing calls the executor in the portal process, so
the portal cannot run an Agent. Composing a consumer makes it able to.

CAP's in-memory transport requires publisher and consumer in one process. So the local habitat
gains what the deployed one deliberately separates: the portal resolves project PATs and clones
repositories, which the dispatch identity exists to keep apart (`infra/dev/dispatch.tf`: "one
compromise should not reach both").

## Goals / Non-Goals

**Goals:**
- Two containers locally instead of five, with the Run lifecycle indistinguishable.
- A deployed habitat that structurally cannot acquire the in-process consumer.
- Redelivery across a process death — the reason CAP beats a `Channel<Guid>`.

**Non-Goals:**
- Replacing the queue anywhere a queue exists.
- Removing Postgres, or making the database embedded (CAP has no SQLite provider — see the risk).
- Changing `IRunDispatcher`, the executor, or anything a module can see.
- Packaging, installers, or a zero-container distribution. This makes one reachable; it is not it.

## Decisions

**D1 — the substrate is chosen by configuration presence, and ambiguity refuses at startup.** A
queue connection string means the queue; its absence means CAP. Both, or neither, throws naming
which contract is ambiguous. This is ADR-0010 applied verbatim — a habitat contract is asked,
never inferred — and it is what makes "the deployed habitat cannot acquire the consumer" a
structural fact rather than a deployment convention. *Alternative rejected:* an environment name
or a feature flag; DEC-049's compose defaults to Production, and gating on that once refused to
start the very habitat it protected.

**D2 — the consumer is composed by the host, never by the dispatcher registration.** Registering
`IRunDispatcher` and registering a subscriber are two acts, and only the Server in a local habitat
does the second. The dispatch worker process keeps its own reader; nothing composes both.
*Alternative rejected:* one `AddLocalDispatch()` doing both — the worker would then acquire a
consumer it must not have, and the mistake would be invisible.

**D3 — durability is the outbox, not the transport.** The publish goes through the same
`cap` schema the events use, so a Run dispatched and lost to a process death is redelivered by
CAP's fallback processor. BR-004 is about *Runs* not being retried automatically; a redelivered
dispatch message is the substrate doing what the queue does, and the executor's existing "not
Queued → log and return" guard is what makes a duplicate delivery change nothing. That guard is
now load-bearing on a second path, so it gets its own test rather than being inherited.

**D4 — the credential consequence is a recorded decision, not a silent trade.** The local habitat
loses the worker/portal separation because CAP's in-memory transport requires one process. This is
the same trade #166's design D2 already made for the in-process conversation runtime — correct on
a machine one person owns, not a degraded mode — and it is local only, by D1's construction.

## Risks / Trade-offs

- [The portal becomes able to execute Runs] → only where configuration says there is no queue, and
  the composition refuses ambiguity (D1). The deployed habitat's path is byte-identical to today's.
- [CAP couples the local habitat to Postgres] → stated in the proposal and worth repeating: there
  is no SQLite storage provider, so a future embedded database cannot keep this substrate. The
  alternative — a `Channel<Guid>` — is not durable, which is the property BR-004's crash story
  needs. Accepted with the constraint recorded.
- [The reaper and a redelivery could race] → the reaper terminates a Run; a redelivered message for
  it must execute nothing. The executor's state guard already says so; the acceptance criteria
  assert it on this path rather than assuming inheritance.
- [Two dispatchers is two code paths to keep honest] → they share the seam and the message shape,
  and the functional tier runs the same Run lifecycle assertions against both.

## Migration Plan

Nothing to migrate: the `cap` schema exists and the MigrationService already creates it. A
deployment with a queue connection string is unaffected. Rollback is reverting.

## Open Questions

(none — the two owner decisions were taken at grill time and are recorded in the issue)
