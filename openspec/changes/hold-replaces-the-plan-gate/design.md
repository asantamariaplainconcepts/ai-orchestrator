## Context

Two mechanisms stop work for a person today, and they stop it at different moments:

- `requiresApproval` routes a Run into a two-phase lane — `Planning` → `AwaitingApproval` → a human
  decides in the portal → `Executing` (BR-007, DEC-039, DEC-040). The wait lives inside a Run,
  holding its Story against a second one (BR-001), untimed (BR-006).
- An unclaimed lifecycle boundary means nobody automates that transition, so a person moves the work
  across it (#310). Nothing is recorded anywhere; the absence *is* the signal.

Neither is visible from the vendor, which is where the work lives and where BR-008 puts the source
of truth. DEC-062 already downgraded the first one: once every Automation runs the repository's own
prompt, "a plan phase publishes nothing" became a prompt-level promise, and the decision log records
the approval gate as "a workflow control now, not a containment control". A workflow control does
not need two Run states and a review surface.

This change makes a **hold** — the reserved label `hitl` on the Story — the one way work waits. It
supersedes DEC-039 and DEC-040, rewrites BR-007, and removes `requiresApproval` from the aggregate,
the Contracts surface, the starter catalogue and the form.

## Goals / Non-Goals

**Goals:**

- One human gate, visible in the vendor, clearable by anyone who can edit labels (UC-008, BR-009).
- Every creation path honours it — event matching and *Run now* alike (BR-013).
- No new persisted concept: applying a hold reuses the marks an Automation already writes, and the
  only schema change is dropping a column.
- The starter chain keeps stopping three times, so a project set up from the catalogue behaves the
  same way for the person operating it.

**Non-Goals:**

- Deleting `Planning`, `AwaitingApproval`, `DecideOnPlan` or the `Plan`/`ApprovedAt` columns.
- Restoring the Inbox's third category (UC-026) — its replacement is a named follow-up.
- Making the Automations tab's board preview an authoring surface (its own issue).
- Teaching the repository's own `/aio:*` commands about a hold (its own issue).
- Per-project naming of the label.

## Decisions

### D1 — the hold is enforced in `RunCreator.Create`, not in `StoryChangedHandler.Matches`

`RunCreator.Create` is the single path both matching and *Run now* take, and it already carries the
rules that must not be forgotten by either — its own comment says the archived-Project check lives
there "because this is the one creation path both matching and Run now share, so neither can forget
it". The hold joins BR-001, BR-002 and BR-016 there.

*Rejected — checking in `Matches`*: it is the more obvious place and it is wrong. `Run now` does not
go through `StoryChangedHandler`, so a held Story would still be dispatchable by hand, contradicting
BR-013 ("manual dispatch bypasses detection only").

### D2 — `RunCreator` gains `IStoryReader`

`Create` receives a `vendorStoryId`, not a Story, so it cannot see labels today. It gains
`IStoryReader` from `AiOrchestrator.Modules.Backlog.Contracts` — an assembly the Runs module already
references and already injects (`StoryChangedHandler` takes the same interface), so no new module
edge is introduced and the MOD analyzers see nothing new. Only a constructor widens.

*Rejected — passing labels in from each caller*: two call sites means two chances to forget, which
is the failure D1 exists to prevent.

### D3 — a held Story is a new `RunCreation` outcome, not an exception

`RunCreation` is already a closed hierarchy of outcomes with two voices — the event handler stays
silent where at-least-once makes silence correct, the endpoint answers the human. A `Held` outcome
joins `AlreadyActive`, `ProjectArchived` and `QueuedAtCap`: silent on the event path, and on the
endpoint a refusal that names the hold.

### D4 — the hold is a reserved constant compared case-insensitively

One name, product-wide. It lives as a constant in `BuildingBlocks` rather than in any module's
Contracts, because three places need it and none owns it: Runs (the refusal), Projects (the
catalogue's wiring and the form's copy) and the frontend (rendering a held column). Comparison folds
case, the same way BR-003 and matching already compare labels (DEC-056) — `HITL` and `hitl` are one
hold, or an Admin who typed it in the vendor's own casing would find the flow running anyway.

*Rejected — per-project configuration*: a Project field, a migration, and every surface resolving a
name before it can render a hold, to solve a problem nobody has reported. Revisit when a project
with a conflicting convention actually appears.

### D5 — applying a hold reuses `OutputLabels`

DEC-062 kept output labels as the one vendor write the orchestrator performs on success, on the
grounds that "the workflow's wiring is machinery like matching, not action ceremony". A hold is that
same machinery, so it travels in the same licensed write as the Automation's other marks and its
claimed transition — one write, no new field on the Automation, no new migration beyond dropping
`RequiresApproval`.

### D6 — the plan machinery is left unreachable rather than deleted

Removing `requiresApproval` makes `Planning`, `AwaitingApproval`, `DecideOnPlan` and the
`Plan`/`ApprovedAt` columns unreachable — nothing can produce a Plan or enter either state. They
stay. This is DEC-062's own precedent: it kept `AwaitingInput`, its machinery and its inbox category
"unreached rather than removed, because Run states were out of scope". They are out of scope here
too, and deleting them would double a diff that already crosses two modules and the corpus. BR-001's
partial unique index keeps naming the states, which is harmless — an index over states nothing
enters costs nothing and stays correct for the historical Runs recorded in them (BR-014: Runs are
never deleted).

### D7 — the corpus edit lands with this change, and its number is allocated at implementation

The new decision supersedes DEC-039 and DEC-040 and rewrites BR-007. It lands *with* this change,
not ahead of it — the pattern the log itself shows (DEC-062: "Decided 2026-08-01 with #162").
`DEC-066` is the highest allocated on `origin/main` at the time of writing, so this is expected to
be **DEC-067**; the `decision-records` capability requires numbers to be allocated against
`origin/main`, so the implementer re-checks at the moment of writing rather than trusting this line.

`openspec/config.yaml`'s own project context states "Approval-gated runs are two-phase: plan ->
human review in the website -> execute (DEC-040, BR-007)" and must follow, or every future change's
context will teach the superseded model.

### D8 — the UI change is a removal, governed by the existing design system

No new component. The approval `Switch` and its copy leave the Automations form; `AutomationSentence`
drops its gated clause; `GateChip` stops being fed by `requiresApproval` in `BoardPreview` and
`KanbanBoard` and is fed by the hold instead; `summarise()`'s human-stop count becomes "the claimant
marks the hold, or nobody claims the boundary". `DESIGN.md` and the design-system tokens govern as
they already do (DEC-009 for the i18n catalog: removed keys must leave the catalog, and new hold
copy enters it, or CI fails on hardcoded copy).

## Risks / Trade-offs

- **The review moves from before the work to after it** → An Automation used to be stoppable before
  it spent tokens or opened a PR; now it acts and the *next* step is held. Accepted deliberately,
  and it is the argument DEC-062 already made: with one action running the repository's prompt, the
  plan phase never guaranteed containment. Anyone needing a pre-flight stop writes it into the
  prompt, which is where that promise already lived.
- **A hold applied while a Run executes does not stop that Run** → Stated as a requirement rather
  than left to discovery, and cancellation (BR-012, UC-014) remains the deliberate way to stop work
  in flight. The alternative — a label that kills running work — makes mislabelling destructive.
- **The Inbox under-reports until its follow-up lands** → UC-026 promises "everything waiting on a
  human" and its approval category empties here. The gap is named in the proposal and carried as a
  follow-up rather than silently accepted; held work is visible on the board and in the vendor
  meanwhile.
- **A `hitl` label already in use on somebody's repository would hold their flow unexpectedly** →
  The label is new vocabulary for this product; a project already using that exact name for another
  purpose would see Runs refused. The refusal names the hold, so the cause is legible immediately,
  and D4's rejected alternative (per-project naming) is the escape hatch if it ever bites.
- **Unreachable code is a liability if the follow-up never lands** → Mitigated by naming the
  follow-up in the proposal and by AC 6, which asserts no Run can reach either state — a test that
  fails the day something reaches them again.

## Migration Plan

1. Corpus and decision first, in the same branch: the new DEC, BR-007, and the UC/ACT edits — so
   the rest of the diff is read against a rule that already says what it does.
2. Backend: drop the column (EF migration), remove the flag from the aggregate and both Contracts
   records, add the hold constant, widen `RunCreator` and add the `Held` outcome.
3. Catalogue: the manifest's `automation` blocks lose `requiresApproval`; propose, implement and
   sync gain the hold in their output labels.
4. Frontend: remove the control and its copy, feed the chip from the hold, recount human stops.
5. Tests: retire the approval-path coverage, add the four hold behaviours.

**Rollback**: the change is a column drop plus deletions. Rolling back means reverting the branch
and re-applying the migration in reverse; no data is lost by the drop except a boolean whose only
consumer is being removed in the same commit, and historical Runs keep their `Plan` and `ApprovedAt`
values untouched.

## Open Questions

- Which surface, if any, should show *why* a Story is held? The label carries no reason today, and a
  comment on the Story would be a vendor write DEC-062's carve-out does not license. Left open
  deliberately — this change does not need it, and answering it inside the hold's first slice would
  widen the licensed writes.
- Does the Inbox follow-up list held **Stories** or keep listing Runs only? Deciding it here would
  pre-empt the grill that follow-up owes.
