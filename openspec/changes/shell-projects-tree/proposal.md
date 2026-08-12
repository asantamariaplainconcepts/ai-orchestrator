## Why

The shell's sidebar is two flat links (Projects, Inbox), so "what is running right now" costs a
navigation into each project, one at a time — and an operator with several projects, or the
self-host developer with several Runs on one machine, has no at-a-glance view of live work
([#335](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/335)).

This introduces **UC-033 — a Member sees every project's live work in one panel** for
**ACT-002 Member**, extending UC-021 (per-project Runs) and sitting beside UC-026 (the Inbox)
rather than replacing it: the tree answers *"what is this project doing"*, the Inbox answers
*"what waits on me"*. It is deliberately not blocked on UC-030 (live agent status) — Run state
exists today. No open `OPN-*` is involved (RULE-006).

## What Changes

- The expanded sidebar becomes a **projects tree**: each visible project is a row linking to
  `/projects/:id`, with its live work nested beneath it.
- A **Story row** appears under a project when the orchestrator is engaged with that Story —
  it carries the **hold** (`hitl`, BR-007/DEC-067) **or** it has a non-terminal Run
  (`Queued` · `Executing` · `AwaitingInput`). Its **Runs nest under it**, each linking to
  `/projects/:id/runs/:runId`.
  - This is the scoping decision for "live issues", and it is forced by the corpus: `Story.State`
    carries the **vendor's own** state value, permanently un-normalised (DEC-045, closing
    OPN-003), so "an open issue" has no cross-vendor definition the product may assert. "Held, or
    has a Run" is derivable, vendor-neutral, and bounded — BR-001 caps one active Run per Story
    and BR-002 caps Runs per project, so the tree cannot grow without limit.
- A project with no held Story and no in-flight Run renders **as its row alone** — no empty group,
  no placeholder.
- The **icon rail and the mobile sheet render the same entries and the same destinations** as the
  expanded tree. The rail drops the label, never the entry (#126 design D2, already the standing
  rule in `frontend-architecture`).
- **New cross-project read** `GET /api/in-flight`, in the Runs module's Observation feature beside
  `/api/inbox`: every visible project with its held Stories and non-terminal Runs, scoped by
  `IProjectPermissions.VisibleProjects` (BR-009) exactly as the Inbox is.
- `IStoryReader` (`AiOrchestrator.Modules.Backlog.Contracts`) gains a **held-Stories read** so the
  tree does not have to fetch every Story id and then `Find` each one.
- **UC-033 is added to `docs/product/v1/04-capabilities.md`** by this change (RULE-003).
- The Inbox entry and its ambient count are **unchanged**. A held Story appears in **both**
  surfaces on purpose, and UC-026 keeps its subtraction semantics.

Not breaking. No integration contract moves: no Aspire change, no host `csproj` change, no outbox
message schema change, no CI change. `IStoryReader` gains a member and loses none, and its only
implementation is the module that owns it.

### A deviation the spec review must accept or reject

Issue #335's acceptance criterion 4 says the tree refreshes with *"data the shell already
queries"* and introduces *"no new transport and no new polling channel"*. The first half is not
satisfiable as written: **the shell queries `/api/inbox` and the current principal, and nothing
else** — it has never read Runs, and `/api/inbox` covers only `AwaitingInput` and failures, so it
can see neither a `Queued`/`Executing` Run nor a held Story.

This change therefore reads **one new endpoint on the cadence the shell already runs** (the
Inbox's 30s `refetchInterval`), rather than fanning out N per-project queries from the shell. The
*intent* of AC 4 is kept exactly — no websocket, no SignalR, no second polling cadence, one more
REST read on the existing one — but a reviewer who reads AC 4 literally should reject this and say
so. `design.md` records the rejected alternatives.

## Capabilities

### New Capabilities

- `shell-projects-tree`: the sidebar as a tree of every visible project and the live work nested
  under it — which rows exist, what they link to, when they leave, and how the rail and the sheet
  stay identical to the expanded panel.

### Modified Capabilities

- `frontend-architecture`: the shell requirement currently fixes navigation as *sidebar links* —
  a flat list. It gains the tree as the sidebar's structure, and the existing collapse rule
  ("every navigation destination SHALL remain reachable in one click", "a collapsed entry SHALL
  carry its name") is extended to cover tree children.
- `run-orchestration`: two requirements change. *"Runs are observable per project and per Story"*
  gains the cross-project in-flight read (a third observation surface beside the per-project list
  and the Inbox). *"Cross-module reads happen through the second and third Contracts surfaces"*
  gains the held-Stories read on `IStoryReader`.

## Impact

**Backend**
- `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Observation/UseCases/` — new
  `GetInFlight.cs`, modelled on `GetInbox.cs` (same `[Requires(Access.FiltersToCaller)]`, same
  `VisibleProjects` scoping, same `IProjectCatalog.ActiveProjectIds` fallback for the null-means-all
  caller that `GetInboxChanges` already established).
- `src/modules/Backlog/AiOrchestrator.Modules.Backlog.Contracts/IStoryReader.cs` — new held-Stories
  member; its implementation in the Backlog module.
- Case-folding stays in `StoryHold.Is` (`src/shared/AiOrchestrator.BuildingBlocks/Domain/`), never
  re-expressed in SQL — DEC-056's fold has one home.
- Reads Postgres only. The **Mirror is Postgres**, so held Stories cost no vendor call — this
  surface does not repeat the hazard `GetInboxChanges` documents (a per-project *vendor* read on a
  30-second cadence).

**Frontend**
- `src/frontend/shared/ui/AppShell.tsx` — `NavItems` becomes the tree; the three containers
  (expanded sidebar, rail, sheet) keep rendering from one component, which is what stopped the
  phone from losing entries before.
- New query hook + types under an existing feature slice (no new top-level directory; `shared/` is
  for cross-cutting plumbing only).
- Typed i18n catalogue for every new string (DEC-021 — hardcoded JSX copy fails CI); Platform-theme
  tokens and kit primitives only (DEC-051, `DESIGN.md`).

**Docs**
- `docs/product/v1/04-capabilities.md` — UC-033.

**Tests**
- Runs functional tests for the new endpoint, including the BR-009 case: a project the caller may
  not see is **absent**, never merely empty.
- E2E for the rail/sheet parity claim — the assertion that the collapsed tree offers the same
  destinations is the one a unit test cannot make honestly.
