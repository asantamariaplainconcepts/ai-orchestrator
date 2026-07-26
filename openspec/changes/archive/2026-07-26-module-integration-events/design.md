# Design — module-integration-events

## D1 — CAP wrapped, not sanctioned

Modules reference no infrastructure SDK — the rule that kept Octokit in one file, Azure out of
BuildingBlocks, and the queue client out of every module. CAP does not get an exception: modules
see `IIntegrationEventPublisher` and `IIntegrationEventHandler<T>` (BuildingBlocks), and the CAP
implementation lives in ServiceDefaults. A generic CAP subscriber receives each topic and fans
out to registered handlers.

The cost is one layer of indirection and losing CAP's attribute-based subscription ergonomics
inside modules. The benefit is that MOD001–005 and the ArchTests keep meaning what they say, and
a future transport (or a CAP replacement) is a ServiceDefaults change.

**Rejected:** letting modules use `[CapSubscribe]` directly. Cheaper today; it normalises
"modules reference infrastructure when convenient", which is how the boundary dies.

## D2 — Events are versioned records in `.Contracts` assemblies

`StoryChanged` lives in `AiOrchestrator.Modules.Backlog.Contracts` — the first use of the
pattern the guardrails ratified at bootstrap (`Discover` skips `*.Contracts`; ArchTests allow
referencing a sibling Contracts assembly). The event carries **identity, not state**: project id,
story vendor id, and what kind of change — consumers read current truth through contracts, the
same staleness reasoning as dispatch design D2.

A `Version` field from day one, like `DispatchMessage`: a consumer that meets a shape it does
not understand drops it explicitly rather than misreading it.

## D3 — Transactional publish is the point; transport is a detail

CAP stores the published message in Postgres **in the same transaction** as the publishing
module's `SaveChanges`. That is the property that makes events trustworthy: a Story update and
its announcement commit or roll back together. Delivery then happens in-process (in-memory
transport) with the stored message as the source of truth for redelivery.

**Spike results (task 0, run 2026-07-26 against CAP 10.0.1 + Postgres 18 — observed, not
assumed):**

- **The dominant crash window survives.** Killed with `Environment.FailFast` while a handler was
  blocked mid-execution: `cap.received` already held the message as `Scheduled`, and after
  restart the retry processor re-executed it. Redelivery proven.
- **A residual loss window exists and is accepted.** Between the broker-send succeeding and the
  consumer persisting its `received` row, the in-memory channel is volatile; a death exactly
  there loses the delivery (architecturally — the spike demonstrated the adjacent case, where a
  send with no subscriber went `Failed`). The consequence: a lost `StoryChanged` is not
  re-emitted next poll, because nothing changed again. MVP accepts this; the remedies are the
  BR-013 *Run now* path and, if it ever bites in practice, a reconciliation sweep in the matcher
  — recorded here so it is a decision, not a surprise.
- **Retry exhaustion is terminal and silent by default.** A message that fails its
  `FailedRetryCount` sends stays `Failed` forever with nothing automatic touching it — the
  telemetry-shrug shape again. The composition therefore wires CAP's failure threshold callback
  to a loud log from day one; #17 owns surfacing it operationally.
- `FallbackWindowLookbackSeconds` (default 240) gates how quickly restarts redeliver; the
  composition sets it deliberately rather than inheriting a number nobody chose.

## D4 — At-least-once, said out loud

Consumers WILL see duplicates — after a crash, after a redelivery, eventually after a webhook
races the poller (#31, BR-015). The contract is therefore: **every handler is idempotent**. For
#17's matcher the floor is structural — BR-001 ("one active Run per Story") is pure equality and
becomes a partial unique index, the constraint automation-overlap could not be (and the
`context.md` there explains why the two cases differ).

CAP's retry ceiling is configured **deliberately** (small, not the default ~50): BR-004 forbids
retrying a failed *Run*, and while retrying a failed *handler* is legitimate, an unbounded
default is a policy nobody chose. Exhausted retries land in CAP's failed table and the
`verify-telemetry` lesson applies: a dead-letter nobody looks at is a shrug — surfacing it is
#17's operational task.

## D5 — CAP's schema belongs to the MigrationService

CAP creates its storage tables at first use by default. That is an app migrating at startup —
the exact behaviour two changes were spent removing. **Spike verdict: the fallback is active.**
CAP 10 exposes no switch to disable its initializer; it runs idempotent `CREATE IF NOT EXISTS`
at every startup. Resolution: the MigrationService creates the `cap` schema and tables first
(running CAP's own initializer), so the app-side init is a structural no-op. The invariant
narrows honestly from "the Server executes no DDL" to "the Server's DDL is a no-op by
construction" — written down rather than discovered.

## D6 — Reads cross the boundary through Contracts, not events

The Runs module will need Projects' Automations and Backlog's Stories. Fat events carrying that
state were rejected (staleness, growth — dispatch D2's reasoning). Instead: a module that owns
data exposes a narrow read interface in its Contracts assembly and registers the implementation
itself; consumers depend on the interface. First concrete instance lands with #17
(`IAutomationCatalog` in Projects.Contracts); this change establishes the pattern with
`StoryChanged` + the Backlog Contracts assembly so #17 copies rather than invents.
