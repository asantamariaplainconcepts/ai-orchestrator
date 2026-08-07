## 1. Domain + persistence (Runs)

- [ ] 1.1 Widen `Run`: nullable `VendorStoryId`/`AutomationId`; add `TargetChangeNumber`,
      `TargetChangeUrl`, `TargetChangeBranch`, `Instruction`; a `Run.CreateForChange` shape whose
      invariant is exactly-one-target; `RunStates` untouched.
- [ ] 1.2 EF migration: the nullability changes, the new columns, and the filtered unique index on
      `(ProjectId, TargetChangeNumber)` generated from `RunStates.ActiveStateFilter()` — the same
      source BR-001's filter uses, so the two cannot drift.
- [ ] 1.3 Mirror BR-001's trio in `RunCreator`: pre-check by change number, the unique-violation
      race arm, one refusal naming the active Run.

## 2. Launch (Runs)

- [ ] 2.1 `RunOnChange` use case: `POST /api/projects/{projectId}/changes/{number}/runs` with
      `{ instruction, runtime? }`, `[Requires(RunPermissions.Trigger)]`, FluentValidation refusing
      empty instructions; resolves URL + head branch via `IChangeReader.Open` and refuses a number
      not among the open changes (design D3); pod lane only.
- [ ] 2.2 Route creation through `RunCreator`'s shared guards (accepts-work, concurrency cap,
      dispatch, MarkDispatched) so a change Run obeys every rule a story Run does.

## 3. Execution (Runs)

- [ ] 3.1 Fork `RunExecutor.Invoke` on the target (design D4): no Story read, the instruction as
      the prompt body framed by number/title/branch, named-branch `Prepare`, one phase, `HandOn`
      skipped, BR-005 default timeout, launch-named or default runtime.
- [ ] 3.2 `GetRunChanges` resolves change Runs by their recorded number through a new
      `IChangeFileReader.ForChange(projectId, number)` (design D5).

## 4. The marker #274 shipped dead (design D7)

- [ ] 4.1 `GetInboxChanges` matches `run/<guid>` head branches to Runs and change-targeted Runs to
      their recorded target; the `OutputLink` join goes with the retired column it read.
- [ ] 4.2 Its functional test stops seeding `OutputLink` by hand and seeds what production writes:
      a Run whose id the branch carries — the test must be able to fail (ADR-0013).

## 5. Surfaces (frontend)

- [ ] 5.1 Launch affordance on the Inbox change entries: a small dialog with the instruction
      textarea (and runtime select), posting to the new endpoint; refusals rendered in place.
- [ ] 5.2 Runs list + Run screen identity for change Runs: `PR #n`, vendor link instead of Story
      link, the instruction readable in the detail; the board's grouping filters change Runs out.
- [ ] 5.3 i18n keys; mock handlers for the launch and a change-targeted Run.

## 6. Tests

- [ ] 6.1 Functional: launch records the instruction; per-change concurrency refusal; story Run
      and change Run do not contend; empty instruction 400; unknown change number refused; HandOn
      skipped (no labels applied).
- [ ] 6.2 Executor-level: prepared on the head branch by name (fake workspace records it), no
      Story read for change Runs, instruction reaches the runtime as the prompt body.
- [ ] 6.3 Migration applies cleanly forward from the previous head (the
      `OutputLabelMigration_Should_Constraint` pattern).
- [ ] 6.4 Marker: a listed change with a `run/<id>` head branch links its Run — seeded the way
      production writes it.

## 7. Verification

- [ ] 7.1 csharpier, frontend gates, `dotnet build` 0 errors, module suites green locally.
- [ ] 7.2 Mock mode: launch dialog, refusal states, change-Run identity on list and detail, board
      tolerant, both themes.
- [ ] 7.3 CI green on the PR head (verified job-by-job), at sync.
