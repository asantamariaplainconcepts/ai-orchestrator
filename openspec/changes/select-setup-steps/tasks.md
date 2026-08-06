# Tasks: select-setup-steps

## 1. The seam the card needs (design D5, D7)

- [ ] 1.1 `DiscoverPipeline.PlannedStep` gains `IReadOnlyList<string> OutputLabels`, filled from
      `step.Wiring.OutputLabels` in the plan projection. No new read: the labels come from
      `PipelineSteps.All`, which the projection already walks.
- [ ] 1.2 Correct the comment at `DiscoverPipeline.cs:109-110` — it claims a step that is neither
      present nor installable is "listed as not installable", and the filter below it drops that
      step. The filter is right; say what it does and why (a row that changes nothing either way is
      noise in a list of what will happen).

## 2. Selection on the endpoint (design D1, D2, D4)

- [ ] 2.1 `SetUpDefaultAutomations.Request` and `Command` gain `IReadOnlyList<string>? Steps`.
      Document on the record that absent means every step and empty means none — the distinction is
      load-bearing and one word of XML doc is cheaper than the bug.
- [ ] 2.2 Build the selection filter as a case-insensitive set (the BR-003/DEC-056 identity, the
      comparison already used at `SetUpDefaultAutomations.cs:155`), applied to `adopted` and `gaps`
      at lines 164-167 — **before** the taken/overlap loop, so an excluded step never reaches the
      skip path.
- [ ] 2.3 `Response` gains `IReadOnlyList<string> Excluded`, populated from the steps the filter
      removed. Keep it separate from `Skipped`: they answer different questions.
- [ ] 2.4 Confirm `FillGaps` receives only selected gaps, so its existing `gaps.Count == 0`
      short-circuit (lines 315-318) is what produces "no branch, no pull request, no failure".
      `StarterInstaller.Install` must never be reached with an empty file list — it answers
      `Workspace.NoChanges`, which would surface as a failure for a choice the Admin made.
- [ ] 2.5 Leave `InstallMissing` in place and unchanged (design D3). Verify by reading, not by
      assumption, that a bodyless call still creates Automations and writes nothing.

## 3. Selection on the card (design D5)

- [ ] 3.1 Mirror both DTO changes in `useWorkflowSetup.ts`: `outputLabels: string[]` on
      `PlannedStep`, `steps?: string[]` on `WorkflowSetupInput`, `excluded: string[]` on
      `WorkflowSetupReport`.
- [ ] 3.2 Hold selection state in `WorkflowSetupSection`, seeded to every row of the shown
      candidate's plan and reseeded when the chosen candidate changes — a plan for a different
      directory is a different list, and carrying a stale selection across it would exclude rows
      nobody chose.
- [ ] 3.3 Render a selection control per row in `Plan`, labelled from the i18n catalogue (DEC-021 —
      hardcoded JSX copy fails CI). The row stays readable when deselected; it is excluded, not
      gone.
- [ ] 3.4 Write the hand-off function: a pure function over plan rows returning, for the current
      selection, the still-selected steps whose incoming hand-off an excluded step used to provide.
      Edge rule: A hands to B when A's output labels name B's trigger, compared
      **case-insensitively**. Do not reuse `buildChains` (different input type, and its `Map`
      comparison is case-sensitive) and do not copy that comparison.
- [ ] 3.5 Mark the gap on the affected row and leave the confirm enabled. Disable the confirm only
      when the selection is empty.
- [ ] 3.6 Send `steps` from the confirm; leave `installMissing: true` as it is.
- [ ] 3.7 Render `excluded` in the report as its own fact, beside `skipped` and distinct from it.
- [ ] 3.8 Add the new copy to `en.ts`. Remove the orphaned `workflowSetup.installMissing` key
      (verified unreferenced — #233 deleted the control it belonged to).

## 4. Keep mock mode honest

- [ ] 4.1 The mock's `discover-pipeline` candidates carry no `plan` at all (`mock.ts:445-461`), so
      the card renders an empty plan in mock mode today and would render an unusable selection
      surface. Give each candidate plan rows including `outputLabels`, with at least one hand-off
      edge so the gap marker is exercisable by hand.
- [ ] 4.2 Have the mock's `set-up-defaults` honour `steps` and answer with `excluded`, so mock mode
      shows the same report shape the API returns.

## 5. Tests

- [ ] 5.1 Functional (`PipelineAdoption_Should_Constraint`): an absent selection creates every step;
      an empty selection creates none, opens no pull request, and reports every step excluded.
      Two tests — the absent/empty distinction is the one this change most needs pinned.
- [ ] 5.2 Functional: a partial selection creates exactly the selected steps, and the excluded ones
      appear in `Excluded` and nowhere else.
- [ ] 5.3 Functional: an excluded gap is absent from the published files
      (`StubInstallWorkspace.PublishedFiles`), while the selected gaps are present.
- [ ] 5.4 Functional: excluding every gap leaves `PreparedBranch` unset, opens no pull request, and
      reports no failure — the trap in 2.4, asserted rather than assumed.
- [ ] 5.5 Functional: a step excluded by the selection whose trigger the project also already uses
      appears only in `Excluded`, never in both lists.
- [ ] 5.6 Functional: a selection naming a trigger in a different case selects the step; a selection
      naming an unknown trigger succeeds and creates nothing from it.
- [ ] 5.7 Functional: the discovery plan carries each step's output labels, and computing it still
      costs no additional vendor read.
- [ ] 5.8 **Stated, not faked:** no E2E test for the selection surface.
      `SetupPlan_Should_Constraint.cs:18-24` already records why — plan rows need a Connector serving
      directory listings and that tier's GitHub stub answers issues only. Extending the stub is its
      own change. Record this in the test file's doc comment rather than writing a test that cannot
      reach the state.
- [ ] 5.9 **Stated, not faked:** the hand-off function has no unit test, because
      `src/frontend/package.json` has no test runner (`lint`, `typecheck`, `build` only). Its data
      contract is covered by 5.7; the function itself rests on `tsc` and review. Do not add a test
      runner inside this change.
- [ ] 5.10 Mutation check on the load-bearing assertions: making the filter case-sensitive, treating
      an empty selection as "every step", and reporting an excluded step as skipped must each redden
      a distinct test. Build first — a green suite against a stale build proves nothing.

## 6. Gates

- [ ] 6.1 `dotnet build`, then the non-E2E suite, then the E2E suite.
- [ ] 6.2 CSharpier, Prettier, ESLint `--max-warnings=0`, `tsc --noEmit`, the design-system
      validator.
- [ ] 6.3 Frontend production build — run through `rtk proxy`, not `rtk`. This repository has had a
      broken build report success under `rtk` and invalidate a mutation check; grep the new copy out
      of the emitted bundle before trusting any result.
- [ ] 6.4 `openspec validate select-setup-steps`.
