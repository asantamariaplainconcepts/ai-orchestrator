# Proposal: story-automation-matching

## Why

Issue #17 (UC-011) — the loop closes for the first time: a label appearing on a Story causes a
Run to exist and work to be dispatched, with the rules holding. Every prerequisite is now on
`main`: Automations exist to match against (#14), the dispatch queue exists to enqueue into
(#16), and the Backlog announces `backlog.story-changed.v1` from inside its reconciliation
transaction (#41). Nothing consumes that event yet; this change is its first consumer.

Rules in scope: BR-001 (one active Run per Story — ignored, not queued), BR-002 (project cap,
creation-side only), BR-007 (single-phase lane only; `requiresApproval=true` matches create
nothing in this slice), BR-014 (record shape subset), BR-015 (matching consumes the normalized
event stream only).

## What Changes

- A new **Runs module** (`AiOrchestrator.Modules.Runs`, schema `runs`, BC-003): the Run entity
  and the `StoryChanged` handler that matches events against the owning Project's Automations.
- **`AiOrchestrator.Modules.Projects.Contracts`** — the second Contracts assembly:
  `IAutomationCatalog`, the read interface the Runs module matches against (design D6 of #41,
  now made real). The Projects module registers the implementation itself.
- **`IStoryReader` in `AiOrchestrator.Modules.Backlog.Contracts`**: the event carries identity
  only (by design), so the handler reads the Story's current labels and state through Contracts.
- Matching per BR-003's guarantee (at most one Automation matches — enforced at save by #14):
  Added/Updated events match; Removed never does. A match creates a Run (`Queued`) and, when the
  Project is below its BR-002 cap, enqueues the Run id via `IRunDispatcher` (#16).
- **BR-001 as a database constraint**: partial unique index on the Story reference over active
  Run states, plus the concurrency test that exercises it (ADR-0002).

## Impact

- Affected specs: new capability `run-orchestration`.
- New projects: `AiOrchestrator.Modules.Runs`, `AiOrchestrator.Modules.Projects.Contracts`,
  `AiOrchestrator.Modules.Runs.FunctionalTests`.
- Touched: `AiOrchestrator.Modules.Projects` (register catalog impl),
  `AiOrchestrator.Modules.Backlog` + its Contracts (story reader), MigrationService (new schema
  rides the existing migration path), Server composition (none — module discovery attaches Runs).
- Out of scope (stated limitations): two-phase approval lane; *Run now*; cancel; promotion of
  `Queued` Runs when capacity frees (no Run can complete yet); run UI; the crash window between
  Run commit and enqueue (recovery is *Run now*, a later issue — the window is logged loudly).
