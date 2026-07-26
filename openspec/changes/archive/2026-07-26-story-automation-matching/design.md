# Design — story-automation-matching

## D1 — A Runs module, not a feature inside Projects or Backlog

BC-003 names Run orchestration as its own bounded context. Neither existing module can own it
without violating its boundary: Backlog owns the mirror (BR-008), Projects owns configuration
(BC-001). The Run is the first entity whose lifecycle spans both — it is born from a Backlog
event, shaped by a Projects rule, and handed to the dispatch substrate. Module discovery
attaches it with zero host edits, which is precisely what the bootstrap pattern was for.

## D2 — The handler reads current truth through Contracts, never trusts the event

`StoryChanged` deliberately carries identity and change-kind only (#41 D2). Matching needs the
Story's *current* labels and state, and the Automation list — both read at handle time:

- `IStoryReader` (Backlog.Contracts): the Story's labels + state by (ProjectId, VendorStoryId).
- `IAutomationCatalog` (Projects.Contracts): enabled Automations of a Project.

Consequence, stated: an event for a change that has since been superseded matches against the
*newer* truth. That is correct by BR-008 (the vendor is the source of truth), not a bug. A
Story deleted between event and handling reads as absent → no match, no Run.

## D3 — Idempotency comes from BR-001's constraint, not from delivery bookkeeping

Delivery is at-least-once; the handler must be idempotent (#41 D4). No message-id ledger:
BR-001's partial unique index — one Run per Story reference across active states — already
makes the second identical delivery a no-op (the insert loses, the handler treats the unique
violation as "already done", nothing is enqueued — the same narrow-catch pattern as the
Backlog's concurrent reconcile). A dedup ledger would duplicate the invariant the index
already owns (ADR-0003: one owner per derived artifact).

Residual window, stated honestly: if a Run completed *and* the duplicate arrived later, a second
Run would be created. No Run can leave an active state in this slice, so the window is empty
today; it is re-examined by the issue that introduces completion.

## D4 — Run first, enqueue second; the crash window is logged, not hidden

The Run insert commits in Postgres; the dispatch enqueue (Azure Storage Queue) cannot join that
transaction. Order chosen: commit the Run, then enqueue. A crash between the two leaves a
`Queued` Run with no message — visible in the database, recoverable by *Run now* (BR-013, later
issue), and logged loudly at enqueue failure. The reverse order (enqueue first) could dispatch
a Run that does not exist, which the worker cannot distinguish from corruption. An outbox for
the queue would re-solve what #41 built, for a window this slice can only log anyway.

## D5 — BR-002 is creation-side only, and the test seeds states directly

The cap counts `Planning`/`Executing`. In this slice every Run is born `Queued` and nothing
promotes it yet, so the cap can never actually bind in production — but the rule is encoded and
tested now (states seeded directly in the functional tier) so the issue that adds promotion
inherits an enforced rule instead of a comment.

## D6 — `requiresApproval=true` matches create nothing

BR-007's two-phase lane is its own issue. Creating a parked Run now would freeze approval
semantics before that issue runs its grill. The match is evaluated, the refusal is logged with
the Automation id, and nothing is written — a stated limitation, not silence.
