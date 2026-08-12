## 1. Corpus and decision

- [x] 1.1 Allocate the next DEC number against current `origin/main` (expected DEC-067; re-check —
      `decision-records` requires allocation against `origin/main`) and write it in
      `docs/product/mvp/10-locked-mvp-decisions.md`, revising DEC-039 and DEC-040, citing DEC-062's
      stated cost as the rationale and issue #321 as the decision's occasion
- [x] 1.2 Rewrite BR-007 in `docs/product/v1/05-business-rules.md` from approval routing to the hold
      rule; drop the `AwaitingApproval` clauses from BR-001, BR-006 and BR-013
- [x] 1.3 Update `docs/product/v1/04-capabilities.md`: retire UC-013 and UC-015, drop the approval
      clause from UC-005, UC-006 and UC-011, note UC-026's emptied category and its follow-up
- [x] 1.4 Update `docs/product/v1/01-actors-and-responsibilities.md` — ACT-001 no longer manages
      `requiresApproval`, ACT-002 no longer approves plans; add the hold to
      `docs/product/v1/02-domain-glossary.md`
- [x] 1.5 Update `openspec/config.yaml`'s project context, which still states "Approval-gated runs
      are two-phase"

## 2. The hold constant

- [ ] 2.1 Add the reserved hold label as a constant in `BuildingBlocks`, with case-folding
      comparison matching DEC-056, and a unit test that `HITL` and `hitl` are one hold

## 3. Backend — remove the flag

- [ ] 3.1 Remove `RequiresApproval` from the `Automation` aggregate and its EF configuration
- [ ] 3.2 Remove it from `AutomationTrigger` and `AutomationDetail` in
      `src/modules/Projects/AiOrchestrator.Modules.Projects.Contracts/IAutomationCatalog.cs`, and
      from `AutomationCatalog`'s projections
- [ ] 3.3 Remove it from the create/update request contracts, validators and endpoints
- [ ] 3.4 Add the EF migration dropping the column; keep `Plan` and `ApprovedAt` untouched
- [ ] 3.5 Verify no code path can reach `Planning` or `AwaitingApproval` — the states,
      `DecideOnPlan` and its endpoints stay in place but unreachable (design D6)

## 4. Backend — enforce the hold

- [ ] 4.1 Inject `IStoryReader` into `RunCreator` (design D2 — the Runs module already references
      `Backlog.Contracts`)
- [ ] 4.2 Add a `Held` outcome to the `RunCreation` hierarchy beside `AlreadyActive`
- [ ] 4.3 Refuse creation in `RunCreator.Create` when the Story carries the hold, before any write,
      following the BR-001 refusal pattern
- [ ] 4.4 Confirm the refusal reaches both callers correctly: silent in `StoryChangedHandler`, an
      answering refusal naming the hold on the *Run now* endpoint (BR-013)
- [ ] 4.5 Confirm an already-active Run is untouched by a hold — no change to the executor, asserted
      by test rather than by reading

## 5. Starter catalogue

- [ ] 5.1 Remove `requiresApproval` from the manifest's `automation` blocks and from the manifest
      model
- [ ] 5.2 Add the hold to the output labels of the spec-first tier's propose, implement and sync
      steps
- [ ] 5.3 Update the manifest-enumeration test to refuse a block naming an approval flag

## 6. Frontend

- [ ] 6.1 Remove `requiresApproval` from `features/automations/types.ts`, `automationRequest.ts` and
      the form state in `AutomationsSection.tsx`, including the approval `Switch`
- [ ] 6.2 Drop the gated clause from `AutomationSentence.tsx`
- [ ] 6.3 Feed `GateChip` from the hold instead of the flag in `BoardPreview.tsx` and
      `KanbanBoard.tsx`
- [ ] 6.4 Recompute `summarise()`'s human-stop count in `workflowGraph.ts` — a claimant that marks
      the hold, or an unclaimed boundary
- [ ] 6.5 Remove the approval i18n keys and add the hold's copy in `shared/i18n/en.ts` (DEC-009 —
      hardcoded copy fails CI)
- [ ] 6.6 Update `shared/http/mock.ts` so the mock surface matches the real one

## 7. Tests

- [ ] 7.1 Retire the approval-path functional tests and the `requiresApproval` fixtures across
      `src/tests/modules/Projects/**`
- [ ] 7.2 Add functional coverage for the four hold behaviours: event match refused, *Run now*
      refused, executing Run unaffected, clearing the hold creates the next Run
- [ ] 7.3 Add coverage that a stopping Automation writes the hold alongside its other marks and its
      claimed transition, in one write
- [ ] 7.4 Update the migration-shape tests that assert the Automations column list
      (`ClaimedTransitionMigration_Should_Constraint`, `OutputLabelMigration_Should_Constraint`)

## 8. Verification

- [ ] 8.1 `dotnet build` and `dotnet test` clean, including ArchTests
- [ ] 8.2 CSharpier + Prettier + `eslint --max-warnings=0` + `tsc --noEmit` clean
- [ ] 8.3 `pnpm build` — the production bundle, because the E2E suite serves the built output
- [ ] 8.4 `openspec validate hold-replaces-the-plan-gate` passes
- [ ] 8.5 Grep the repository for surviving `requiresApproval` / `AwaitingApproval` references and
      confirm each remaining one is deliberate (the unreachable machinery of design D6)
