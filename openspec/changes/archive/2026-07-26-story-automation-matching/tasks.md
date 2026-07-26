# Tasks — story-automation-matching

## 1. The Contracts surfaces

- [x] 1.1 `AiOrchestrator.Modules.Projects.Contracts`: `IAutomationCatalog` (enabled Automations
      of a Project: id, trigger label/state, requiresApproval) — no implementation types.
      Projects registers the implementation.
- [x] 1.2 `IStoryReader` in `AiOrchestrator.Modules.Backlog.Contracts` (labels + state by
      project id and vendor story id). Backlog registers the implementation.
- [x] 1.3 Guardrails re-run with both in place: boundary ArchTests green, no vacuous pass
      (the allowed-direction test must now see three Contracts references).

## 2. The Runs module

- [x] 2.1 Module skeleton: `AiOrchestrator.Modules.Runs`, schema `runs`, migration, discovery
      attaches it (no host edits). Run entity per BR-014 subset: story reference, Automation id,
      created/updated timestamps, state (`Queued`/`Planning`/`AwaitingApproval`/`Executing`
      defined; only `Queued` reachable in this slice).
- [x] 2.2 BR-001 partial unique index over the story reference across active states — in the
      initial migration, not a follow-up.
- [x] 2.3 The `StoryChanged` handler: read Story via `IStoryReader`, match against
      `IAutomationCatalog` (Added/Updated only; Removed never matches), BR-007 lane split
      (`requiresApproval=true` → loud refusal log, nothing written), BR-002 creation-side cap,
      create Run, narrow-catch the unique violation as "already done".
- [x] 2.4 Enqueue via `IRunDispatcher` after commit (design D4); enqueue failure logs loudly
      and leaves the `Queued` Run visible.

## 3. Tests

- [x] 3.1 Functional (real containers, real CAP relay): the loop-closes scenario — configure
      backlog, add Automation, label a Story, refresh → a Run exists and the queue holds its id.
- [x] 3.2 Functional negatives with the fence pattern (#41 retro): no match → nothing;
      requiresApproval=true → nothing + log; second match while active → nothing (BR-001);
      duplicate delivery → one Run, one message; at-cap → Run `Queued`, queue empty (states
      seeded directly, design D5).
- [x] 3.3 Concurrency (ADR-0002): parallel deliveries for the same Story → exactly one Run —
      the index decides, both handlers report success.
- [x] 3.4 ArchTests as task 1.3; analyzers untouched.

## 4. Close-out

- [x] 4.1 ARCHITECTURE.md: Runs section — where matching lives, what the stated limitations are.
- [ ] 4.2 Full suite + verify sweep; CI green.
