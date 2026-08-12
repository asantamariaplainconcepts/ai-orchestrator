## Context

The shell (`src/frontend/shared/ui/AppShell.tsx`) renders navigation from one `NavItems`
component into three containers — the expanded sidebar, the collapsed icon rail, and the mobile
sheet. That single source is deliberate: the comments record that two copies is how the phone lost
the identity block in the first place. Today `NavItems` is two `NavLink`s.

**What the shell actually queries today** (verified, not assumed): `useInbox()` →
`GET /api/inbox` on a 30s `refetchInterval`, and `useCurrentPrincipal()`. That is all. Runs and
backlog are per-project endpoints (`/api/projects/{id}/runs`, `/api/projects/{id}/backlog`) that
the shell has never called. `/api/inbox` returns only Runs `AwaitingApproval`/`AwaitingInput` and
undismissed failures — it can see neither a `Queued`/`Executing` Run nor a held Story, so it cannot
feed this tree.

**Constraints that decided this design:**

- `Story.State` is the vendor's own value, un-normalised **permanently** — DEC-045 settled it with
  two vendors implemented, closing OPN-003. The product may not assert what "open" means.
- `Story.Labels` is a Postgres **`text[]`** column (verified: `BacklogDbContext.cs:50`
  `HasColumnType("text[]")`, and the initial migration's `table.Column<List<string>>(type:
  "text[]")`). Server-side `Contains` on it is exact, case-sensitive matching.
- The hold folds case (DEC-056) and that fold has exactly one home: `StoryHold.Is` /
  `StoryHold.IsHeld` in `BuildingBlocks/Domain/StoryHold.cs`.
- The **Mirror is Postgres** (glossary): reading held Stories is a local read model query, not a
  vendor call.
- `GetInboxChanges.cs`'s own docstring records the hazard to avoid: folding a second concern into
  `/api/inbox` would inflate a count the shell's badge computes as `length` over that array, and
  would put a per-project **vendor** read on a 30-second cadence.
- Design-system artifacts governing the UI: **`DESIGN.md`** at the repo root (verified present,
  generated token block), the canonical `docs/design-system/` tokens — `--sidebar-w-expanded:
  280px`, `--sidebar-w-collapsed: 64px` (`tokens/layout.css:8-9`) — and the `design-contract` and
  `design-adherence` specs. The kit has `popover.tsx` but **no** collapsible/accordion primitive
  (verified by listing `src/frontend/shared/ui/`).

## Goals / Non-Goals

**Goals:**

- One panel answers "what is every project I can see doing right now", at every sidebar width.
- Tree membership is **derived** — nothing about tree state is stored anywhere.
- BR-009 holds by construction: a project the projects list would not show is absent, not empty.
- The Inbox and its ambient count are untouched, including UC-026's subtraction semantics.
- The rail and the sheet offer the same entries and destinations as the expanded tree (#126 D2).

**Non-Goals:**

- Worktree rows (#331 makes the worktree an attribute of a Local Run — Run detail, not a node).
- Live agent status (UC-030); the tree shows Run state until that exists.
- Sandboxes and runtimes (machine-scoped by design review 5b and #311).
- Finished Runs in the tree; reordering, pinning, filtering, or per-project collapse.
- Any change to what the Inbox contains or counts.

## Decisions

### D1 — "Live" means held **or** carrying a non-terminal Run

A Story row exists under a project when `StoryHold.IsHeld(labels)` **or** it has a Run in
`Queued` · `Executing` · `AwaitingInput`.

*Why:* it is derivable, vendor-neutral, and bounded — BR-001 caps one active Run per Story, BR-002
caps Runs per project, so a project's contribution to the tree has a ceiling.

*Rejected — every Story in a non-closed vendor state* ("the project's open backlog"). This would
require the product to decide what "not closed" means per vendor, which is exactly the question
DEC-045 closed by refusing it, and would reopen OPN-003. It also puts an unbounded list — hundreds
of rows — into a navigation panel.

*Rejected — Stories whose trigger label matches an enabled Automation* ("about to become live").
Derivable without touching vendor state, but unbounded in the one case that matters: a label
applied in bulk floods the tree with work that has not started.

### D2 — One new aggregate read, not a shell fan-out and not a wider Inbox

`GET /api/in-flight` returns every visible project with its held Stories and non-terminal Runs.
The shell consumes it on the **same 30s cadence** the Inbox query already runs.

*Why:* the shell cannot iterate projects it has not fetched, and a per-project fan-out makes the
sidebar's cost a function of project count — N queries every 30s, from every page.

*Rejected — widen `/api/inbox`.* `GetInboxChanges` already documented why this is wrong, and its
reasoning applies verbatim: the badge is `length` over that array, so adding rows that are not
"waiting on you" corrupts a count UC-026 defines. AC 6 of the issue requires that count unchanged.

*Rejected — fan out per-project `/api/projects/{id}/runs` + `/backlog` from the shell.* 2N requests
per refresh, and `/backlog` can trigger a vendor-facing path — the hazard `GetInboxChanges` names.

**This is the change's one deviation from the accepted acceptance criteria** and the spec review
should rule on it explicitly. AC 4 reads "data the shell already queries" and "no new transport and
no new polling channel". The literal first clause is unsatisfiable — the shell queries no Run data
at all. What is preserved is the *intent*: no websocket, no SignalR, no second cadence. What is not
preserved is the letter: there is one more REST endpoint. A reviewer who reads AC 4 strictly should
reject this proposal and say which of the rejected alternatives they prefer.

### D3 — `IStoryReader` gains a held-Stories read; the case-fold stays in `StoryHold`

A new member answers "which Stories in this project are held", returning vendor id and title only.

*Why a new member:* the alternative with today's contract is `VendorStoryIds(projectId)` followed
by `Find` per id — every Story in the mirror, one round trip each, per project, every 30 seconds.

*Why the fold is in memory:* `Labels` is `text[]`, so a server-side `Contains` matches
case-sensitively and would let a Story labelled `HITL` render as unheld — the precise failure
`StoryHold.Is`'s docstring exists to prevent. The implementation projects `(VendorId, Title,
Labels)` for the project and filters with `StoryHold.IsHeld`. Expressing the fold as SQL
(`ILIKE ANY`) was rejected: it would put DEC-056's rule in a second place, where the two can drift.

### D4 — The tree is Project → Story → Run, not Project → Run

*Why:* a Run row is not self-describing. `GetInbox`'s own comment makes the point for the sibling
surface — "a Story id without its Project's name answers 'which #491?' with silence" — and a bare
Run id under a project is worse. Nesting under the Story also makes the two membership reasons one
shape: a held Story with no Run and a Story with two Runs are the same kind of node.

Issue #335's AC 1 describes Runs nested directly under the project. This structure satisfies every
destination AC 1 requires (`/projects/:id`, `/projects/:id/runs/:runId`, the story route) while
adding the Story level; it is a superset, and it is the shape the operator confirmed for this
change.

### D5 — The rail reveals children through a popover; the sheet renders them inline

The rail is 64px — there is no room for an indented row. A project glyph opens a popover carrying
the same children with the same destinations.

*Why this pattern:* it is already the shell's established rail idiom — `frontend-architecture`
specifies the environment chip as "chip with popover on desktop and the collapsed rail, a plain
section in the phone sheet". The sheet renders inline for the reason `design-contract` gives: "a
popover inside a drawer would be a flyout on a flyout".

*Rejected — adding a collapsible/accordion primitive.* None exists in the kit, per-project collapse
is out of scope, and the child count is bounded by D1, so the expanded tree simply renders open.

### D6 — Leaving the tree is derived, never signalled

A Run reaching a terminal state stops matching the predicate, so the next refresh omits it. Nothing
is stored, invalidated, or pushed. This mirrors how the Inbox drops a resolved wait and how the
"close the loop" checklist derives its progress with nothing remembered.

### D7 — The use case lives in the Runs module

`GetInFlight` goes in `Modules.Runs/Features/Observation/UseCases/`, beside `GetInbox` and
`GetInboxChanges`.

*Why:* Runs already reads `IProjectCatalog`/`IProjectPermissions` and `IStoryReader` through
Contracts — the exact dependency direction `run-orchestration`'s cross-module requirement fixes.
Putting a cross-project aggregate in the Projects module would make Projects depend on Runs, which
the MOD analyzers and NetArchTest reject. No architectural convention is deviated from: vertical
slice, CQS query with `[Requires(Access.FiltersToCaller)]`, internal sealed types, Contracts-only
cross-module reads.

### D8 — One shared state chip, extracted from the Runs list rather than copied

Every row that has a state renders it through a new shared chip in `shared/ui/`, and
`RunsSection`'s local `StateBadge` is migrated onto it.

*Why not reuse `StateBadge` where it is:* it is local to
`src/frontend/features/runs/RunsSection.tsx` and carries three defects the tree would inherit and
multiply.

1. It renders the **raw enum** as its label (`<Badge …>{state}</Badge>`), which is user-facing copy
   that never passed through the catalogue — the thing `frontend-architecture`'s i18n requirement
   exists to prevent.
2. It paints `Succeeded`, `Executing` **and** `Planning` the same `bg-success` green. A panel whose
   entire purpose is "what is live" cannot render "running now" identically to "finished". (That
   `Planning` is in the list at all is residue: DEC-067 made the state unreachable.)
3. It carries **no glyph** — state arrives as colour plus an untranslated word, against the rule
   `locus.tsx` states for its own vocabulary: *"always beside a word, never colour alone."*

*Why shared rather than a second chip:* `GateChip`'s docstring already argued this case for the
board and the canvas — two chips that merely look alike drift the first time one is restyled, and
the design gate will not catch it because the tokens are right in both. `LocusChip` is the precedent
to copy: one component, one vocabulary, glyph plus word.

*Scope note:* migrating `RunsSection` is a change to an existing surface, so it is stated as a
`design-contract` requirement rather than smuggled in as a refactor. It is a small, contained edit,
and leaving the defective copy in place while the tree renders a correct one would put two
contradictory state vocabularies on screen at once.

*Rejected — a glyph for the linked change (PR number plus merged/open), the Orca pattern that
prompted this.* The data is a **vendor** read per project (`IChangeReader.Open`, the surface behind
`/api/inbox/changes`), which D2 and `GetInboxChanges`'s own docstring both forbid on the shell's
polling cadence. It is not refused on merit — it needs its own issue and its own cadence decision.

## Risks / Trade-offs

- **A new query on a 30s cadence from every page** → It is Postgres-only: one `Runs` query scoped
  by the visible set, plus one mirror projection per project. No vendor call, which is what makes
  the cadence affordable — the hazard `GetInboxChanges` documents does not apply. The per-project
  loop is still O(projects); if that ever bites, the mirror projection collapses into a single
  `WHERE ProjectId = ANY(...)` query, deliberately left as the obvious next step rather than
  pre-optimised.
- **A permission bug here leaks story titles across tenants** → Same scoping as the Inbox
  (`VisibleProjects`, null-means-all with the `ActiveProjectIds` fallback `GetInboxChanges`
  established), plus a functional test asserting a non-visible project is **absent** from the
  response rather than present-and-empty. AC 5 is a test, not a comment.
- **Tree height grows with project count** → D1 bounds each project's children, but nothing bounds
  the number of projects, and filtering is out of scope. Accepted and flagged: an operator with
  many projects gets a long panel. If it needs solving it is its own change, not a silent cap here.
- **A held Story appears in both the tree and the Inbox** → Intended, and AC 6 says so: the tree
  answers "what is this project doing", the Inbox "what waits on me". The risk is a reader reading
  it as duplication, which is a copy problem, not a data one.
- **Rail popover parity is easy to break** → The three containers keep rendering from one
  component, and the parity claim gets an E2E assertion, because a unit test cannot honestly assert
  "the collapsed tree offers the same destinations".

## Migration Plan

Purely additive. No data migration: no new table, no new column, no outbox schema change. The new
`IStoryReader` member is additive with a single implementation. Deploy is the ordinary path;
rollback is a revert, since nothing persists any tree state.

## Open Questions

1. **AC 4's literal reading** (D2) — the spec review must accept the new endpoint or name which
   rejected alternative it prefers. This is the one gate on the change.
2. ~~Should a held Story with no Run be visually distinct from one with a Run?~~ **Resolved
   (operator, spec review): yes.** DEC-067 makes the hold a wait on a *person* and execution a wait
   on a *machine*, so rendering them alike answers "what needs me?" wrongly. Specified in
   `shell-projects-tree` and carried by the shared chip of D8.
