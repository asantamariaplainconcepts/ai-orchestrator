## 1. The decision, before any code

- [x] 1.1 Get **ADR-0022** (`docs/adr/0022-an-order-a-person-can-rearrange-is-stored.md`) accepted at
      spec review, and flip its Status from `Proposed` to `Accepted`. It is written now because an ADR
      belongs to the change that noticed the recurrence (`docs/adr/README.md:13-16`); it is not
      accepted yet because accepting it is the reviewer's act, and an accepted ADR may not be edited.
- [x] 1.2 Answer design.md's first Open Question before writing the domain: does AC 13's "exactly one
      claimed transition" mean *at most one* (this design's reading — D3, so that DEC-053's standalone
      Automation and the last stage stay expressible) or literally one? The nullability of the new
      field depends on the answer, and so does the migration.
- [x] 1.3 Record DEC-053's supersession where DEC-053 lives
      (`docs/product/mvp/10-locked-mvp-decisions.md:209-221`), naming ADR-0022 and limiting it to the
      clause *"membership is derived from the edges and never stored"*. **Needs the owner's assent** —
      a locked decision is not amended unilaterally. While there, report (do not fix) the duplicate
      DEC-053 at `:367` of the same file.

## 2. The lifecycle, stored (design D1)

- [x] 2.1 Add `LifecycleStages` to `src/modules/Projects/.../Domain/Project.cs` as an ordered list of
      stage names, with the domain operations the claims need: resolve a stage case-insensitively,
      insert a stage immediately before another, and read the ordered list back.
- [x] 2.2 Map it in `src/modules/Projects/.../Persistence/ProjectsDbContext.cs` as a
      `character varying(200)[]` primitive collection with a 200-char element type, following the
      `OutputLabels` precedent at `:53-62` — array position is the order.
- [x] 2.3 Unit-test the ordering operations in
      `src/tests/modules/Projects/AiOrchestrator.Modules.Projects.UnitTests`: inserting before the
      first stage, inserting before a middle stage, and resolving a stage that differs only in case.

## 3. The transition and the mark, separated on `Automation` (design D2, D3, D4)

- [x] 3.1 Add the claimed transition to `src/modules/Projects/.../Domain/Automation.cs`: the from-stage
      **is** the existing `TriggerLabel`, and a new single-valued `ToStage` is the label the Run applies
      as the lifecycle move. Nullable per 1.2's answer — null means "claims no transition; it may mark
      the Story and the flow ends there".
- [x] 3.2 Rewrite `OutputLabels`' meaning to **marks only**, and rewrite the doc comment at
      `Automation.cs:54-56` that currently says the field is "the workflow's outgoing edges, **and**
      any mark that goes with them". That double duty is the substance of this change; leaving the
      comment would leave the ambiguity documented as intended.
- [x] 3.3 Enforce the adjacency invariant in exactly one place (design D4): a claim names two adjacent
      stages of the project's lifecycle, and claiming a from-stage that is not yet a stage inserts it
      immediately before the to-stage without disturbing the existing order. One implementation, for the
      reason `Features/Automations/OverlapGuard.cs:9-13` records for BR-003.
- [x] 3.4 Refuse a claim whose to-stage equals the Automation's own trigger label, and a mark that
      repeats the to-stage, extending the existing self-trigger refusal rather than adding a second one.
- [ ] 3.5 Confirm by test — not by reading — that BR-003 needs no fourth enforcement home (design D5):
      the expression index `IX_automations_trigger_identity`
      (`Persistence/Migrations/20260729150023_UniqueAutomationTrigger.cs:25-30`) and
      `Features/Automations/OverlapGuard.cs:35-53` already refuse a second enabled claimant of a
      from-stage, already name the conflicting Automation (`:78-81`), and already fold case (DEC-056).

## 4. The API and the contract

- [x] 4.1 Carry the claimed transition through `Features/Automations/UseCases/CreateAutomation.cs`
      (request, validator, command, handler, response) and `UpdateAutomation.cs` — including the
      wholesale `PUT` at `UpdateAutomation.cs:26-55`, which replaces every field. Keep
      `[Requires(ProjectPermissions.ManageAutomations)]` on every command that changes a claim, so
      BR-009's refusal comes from the pipeline and not from the UI (AC 9).
- [x] 4.2 Serve the project's ordered lifecycle over the API so no client re-derives it, and add the
      stages a claim creates as part of the claim's own write.
- [x] 4.3 Add the to-stage to the Projects → Runs contract `AutomationDetail`
      (`AiOrchestrator.Modules.Projects.Contracts/IAutomationCatalog.cs:31-56`) and to
      `Features/Automations/AutomationCatalog.cs:55`. Keep the contract the only public surface.
- [x] 4.4 Do **not** turn `UpdateAutomation` into `PATCH` (out of scope, ADR-0019's own Alternatives).
      If the wholesale replace is kept, every client must carry the new field — see 7.1.

## 5. Execution: the lifecycle move and the marks travel one write (design D9)

- [x] 5.1 In `src/modules/Runs/.../Features/Execution/RunExecutor.cs:196-231`, apply the claimed
      to-stage together with every mark through UC-008's licensed write, keeping #165's guarantees: each
      label attempted, and the Run failing while naming every label that did not land.
- [ ] 5.2 Leave `Features/Matching/StoryChangedHandler.cs` untouched, and assert in a functional test
      that a person applying an unclaimed transition's label produces the same Run an Automation's label
      would. That the handler needs no change is the evidence this model adds no dispatch machinery.

## 6. The migration — hand-written, and the riskiest thing here (design Migration Plan)

> **Groups 2–6 are one unavoidably-red window, and the order cannot be fixed by reordering.**
> Proved by exercise, not reasoned: the moment group 2 adds `LifecycleStages` to the model, EF's
> `PendingModelChangesWarning` — configured as an **error** in this repository — makes
> `ApiServiceFixtureBase.InitializeAsync` → `MigrateModules` throw, so **all three functional
> projects fail at fixture initialisation** (346 failures from one cause) until this migration
> exists. Group 6 cannot precede 2–5 either, because the migration is written against the model
> those groups create. The dependency is genuinely circular, so the groups stay in this order and
> the constraint is written down instead:
>
> - **No task in 2–5 may be gated on a green functional suite.** Between 2.1 and 6.1 a functional
>   run reports one fixture failure per test and says nothing about the code under it. Unit tests
>   and `dotnet build` are the only meaningful backend gates inside the window.
> - **Tasks whose evidence is a functional test are deferred to the end of the window, not
>   dropped** — 3.5 and 5.2 are exactly that, and they are completed once 6.1–6.7 land.
> - The suite going green again at the end of group 6 is what closes the window; that is the
>   verification for the whole of 2–6, and it is worth reading as such rather than as group 6's own.

- [ ] 6.1 Write **one hand-written** migration under
      `src/modules/Projects/.../Persistence/Migrations/`, shaped add-copy-drop like
      `20260730222648_OutputLabelSet.cs`. **A scaffolded `DropColumn` + `AddColumn` is not acceptable:**
      `20260730222648_OutputLabelSet.cs:9-19` records that the generated form "would have silently
      discarded every hand-off configured in the deployment: every workflow edge, gone, with the schema
      perfectly correct afterwards." Current column: `character varying(200)[]` on `projects.automations`.
- [ ] 6.2 `Up`, in order: add `LifecycleStages character varying(200)[] NOT NULL DEFAULT '{}'` on
      `projects.projects`; add `ToStage character varying(200) NULL` on `projects.automations`.
- [ ] 6.3 Derive each claim **case-insensitively**: `ToStage` = the first of the Automation's
      `OutputLabels` whose `lower()` matches another **enabled** sibling's `lower("TriggerLabel")` in the
      same project. Remove exactly that label from `OutputLabels`; keep every remaining label as a mark,
      including one that matches no sibling trigger. Fixing case here is not optional — `buildChains`
      compares through a plain `Map` while product identity is case-insensitive
      (`features/automations/planHandoff.ts:16-20` says so), so a case-sensitive read would drop edges
      the canvas draws today.
- [ ] 6.4 Build each project's `LifecycleStages` with a `WITH RECURSIVE` walk over those derived claims,
      in the order the board draws today (`features/backlog/KanbanBoard.tsx:110-137`): roots first, then
      what each hands to, then the loose ones — so the stored order is the order that was on screen.
- [ ] 6.5 Assert inside the same transaction that the number of `(automation, matched output label)`
      pairs before equals the number of non-null `ToStage` values after, and raise rather than commit on
      a mismatch.
- [ ] 6.6 `Down`: prepend `ToStage` back onto `OutputLabels` where non-null, then drop both columns.
      State in the migration's own doc comment that the stage **order** cannot survive the reverse,
      because the old shape has nowhere to put it — written down rather than pretended away, as
      `20260730222648_OutputLabelSet.cs:53-80` does.
- [ ] 6.7 Verify by exercising it (ADR-0001): a functional test in
      `src/tests/modules/Projects/AiOrchestrator.Modules.Projects.FunctionalTests` seeds a project with
      the shape the board draws today — including a differently-cased edge and an output label matching
      no trigger — runs the migration against the Testcontainers Postgres, and asserts the hand-off
      counts and the stored order. A correct schema afterwards is not the evidence.

## 7. The six label walks become one read (design D6)

- [ ] 7.1 Move `features/backlog/KanbanBoard.tsx:348-360` onto `requestFor`
      (`features/automations/automationRequest.ts`), ADR-0019's one builder. Read from the code and
      **not yet exercised**: that call site restates eight fields inline and omits `model`, so pressing
      "require a person" on a column header reverts a chosen model to the deployment's. Reproduce it
      first, then fix it, then assert it — otherwise the claim is unverified (ADR-0005). The new
      transition field would be the second field lost the same way.
- [ ] 7.2 `features/automations/workflowGraph.ts:41` — stop deriving a graph. What remains is the stage
      list read from the API plus a lookup from stage to the Automation claiming the transition out of
      it. **Delete `hasBranches` and `chain.branchedFrom`**, and with them `WorkflowChain.branches` and
      the branch-row walk (`:76-122`) — branching is unrepresentable (AC 13).
- [ ] 7.3 `features/automations/chainDrag.ts:80` — delete `reaches` entirely. A cycle cannot exist in a
      linear ordered lifecycle, so the loop refusal has nothing to compute; of the four refusals only
      `shared` (BR-003) survives, and it stays an explanation at the boundary, never a second
      enforcement (`chainDrag.ts:36-40`).
- [ ] 7.4 `features/automations/planHandoff.ts:35` — restate its question over the plan's own claimed
      transitions. It must keep operating on **uncreated** plan rows that have no ids
      (`planHandoff.ts:6-13`); any design that needs an id here is wrong. Its deliberate case folding
      becomes the norm rather than the exception.
- [ ] 7.5 `features/backlog/KanbanBoard.tsx:98-137` — replace the walk with the stored stage list, and
      delete the invented ordering rule at `:131-136` (unchained Automations after the flow). It existed
      only because the derivation could not supply an order.
- [ ] 7.6 `features/automations/BoardPreview.tsx:33-35` — drop the dedupe (it existed because branch
      rows re-entered at an existing column) and derive from the stored stage list. Re-parent it into
      `AutomationsSection.tsx`, since `WorkflowCanvas.tsx:264` is its only caller today and that file is
      being deleted (design D7).
- [ ] 7.7 `features/automations/AutomationNode.tsx:31` — remove the dangling badge. It warned that an
      output label points at no Automation; after the separation that is the normal case for a mark, not
      a fault. **Removing a warning is a judgement** — it is listed as its own task so a reviewer sees
      it rather than finds it.

## 8. The board becomes the authoring surface (design D7)

- [ ] 8.1 Render one column per lifecycle stage in the stored order, claimed or not, in
      `features/backlog/KanbanBoard.tsx` (AC 1) — and draw the Automation claiming a transition on the
      boundary between its two columns, and on no other (AC 2).
- [ ] 8.2 Label an unclaimed boundary as waiting for a person, with **no** validation error, no
      "incomplete configuration" marker and no elapsed-time or overdue indication (BR-006, AC 3). Keep it
      visually distinct from the on-card approval gate, which stays where it is (BR-007, UC-013, AC 8).
- [ ] 8.3 Add the boundary's explicit controls: assign an Automation to this transition, move one here,
      clear one. Every one of them is an ordinary Automation update through `requestFor`, and the drag
      path calls the **same function** — Playwright cannot perform an HTML5 drag
      (`WorkflowCanvas.tsx:248-252`, citing #110), so the shared function is what puts this under test at
      all (AC 12).
- [ ] 8.4 Make placing a step first work through the same control (AC 4), and reordering through it
      (AC 5), asserting that no other Automation's claimed transition changes.
- [ ] 8.5 State the end of the flow at the last stage without asserting who acts next (AC 8).
- [ ] 8.6 Offer no arrangement control to an ACT-002 Member, and assert the API refusal separately so the
      guarantee does not rest on the UI (AC 9, BR-009).
- [ ] 8.7 Decide and implement where the boundary control lives on the phone pager
      (`KanbanBoard.tsx:252-273`), where two columns never share a screen — design.md's last Open
      Question. AC 12 requires an explicit control at every width the board supports.

## 9. Delete the second drawing (AC 11)

- [ ] 9.1 Delete `features/automations/WorkflowCanvas.tsx` (267), `features/automations/Connector.tsx`
      (191) and `features/automations/DropSlot.tsx` (64) — 522 lines.
- [ ] 9.2 Remove the canvas from `features/automations/AutomationsSection.tsx` (`:23-30`, `:593`,
      `:633`, `:666`, `:767`) and retire whatever is left with no caller — `HumanStepBlock.tsx`,
      `automationDrag.ts`, `useChainRemoval.ts` — rather than leaving dead files behind.
- [ ] 9.3 Assert that the catalogue still offers create, edit, disable, re-enable and delete (UC-005,
      UC-006, AC 11), so deleting a surface cannot quietly take a capability with it (ADR-0006).
- [ ] 9.4 Remove the retired copy from `src/frontend/shared/i18n/en.ts` and add the new strings there —
      hardcoded JSX copy fails CI (DEC-009, DEC-021).
- [ ] 9.5 Update `src/frontend/shared/http/mock.ts` so the lifecycle and the claims are demonstrable
      against the mock. ADR-0016: the fixture derives what the server derives, and it must **replace**
      rather than mutate — a fixture that mutated in place once made the canvas and the catalogue
      disagree.

## 10. The starter tiers claim transitions (design D10)

- [ ] 10.1 Translate the wired hand-offs in `Features/Automations/StarterCatalogue.cs`,
      `UseCases/DiscoverPipeline.cs` and `UseCases/SetUpDefaultAutomations.cs` into claimed transitions,
      so installing a tier creates stages as a consequence of claiming. This is how a new project gets a
      lifecycle without "seed a default lifecycle" coming into scope.
- [ ] 10.2 Update `features/automations/useWorkflowSetup.ts` and the setup card's account of what it will
      create, so the plan says transitions rather than output labels.

## 11. Amend the normative spec text

- [ ] 11.1 Apply the change's delta specs to `openspec/specs/automation-configuration/spec.md`:
      `:437-459` ("Membership SHALL be derived from the edges and SHALL NOT be stored", and the
      one-edge-per-matching-output-label graph), `:536-546` (the two-edges-leave-one-node and
      disconnect-one-branch scenarios, both retired with branching) and `:559-560` (the human-review
      block's "SHALL NOT be a persisted entity" carve-out, which the lifecycle retires).
- [ ] 11.2 Apply the `backlog-mirror` delta: the board's columns become lifecycle stages rather than
      enabled Automation trigger labels (`openspec/specs/backlog-mirror/spec.md:220-308`).
- [ ] 11.3 Re-verify the ADR number against current `origin/main` at sync (`docs/adr/README.md:9`) and
      renumber if another change in flight has claimed 0022.

## 12. Gates and proof

- [ ] 12.1 Backend gates: `dotnet build`, `dotnet test` (unit + functional, Testcontainers Postgres and
      Azurite), CSharpier, and the NetArchTest/analyzer boundaries (MOD001-005, CQS001).
- [ ] 12.2 Frontend gates: `tsc --noEmit`, ESLint `--max-warnings=0`, Prettier, the **production build**,
      and the design-system validator — AC 14 requires no colour, spacing or size literal outside the
      theme tokens (DEC-051).
- [ ] 12.3 `openspec validate --strict` on this change.
- [ ] 12.4 End-to-end suite against the **rebuilt** bundle: a `.tsx` edit is invisible to the E2E tier
      until the production build runs. Assert AC 1, 2, 3, 5, 8, 9 and 12 through the explicit controls.
- [ ] 12.5 Exercise the board in the browser in both themes, and write down what was observed rather
      than what was expected (ADR-0001).
- [ ] 12.6 **State the gap rather than closing it:** the drag gesture itself remains without automated
      coverage. Playwright cannot perform an HTML5 drag (#110, recorded at `WorkflowCanvas.tsx:248-252`)
      and this repository still has no frontend unit runner, so the shared functions behind every
      boundary control are testable only through the end-to-end tier. A suite claiming to cover the
      gesture would be lying; one covering the shared function is honest.
- [ ] 12.7 Add the row for ADR-0022 to `docs/adr/README.md` — done in this proposal — and note that the
      index stops at 0013 while the directory holds ADRs through 0021. Backfilling those eight rows is a
      separate docs fix; it is reported here, not absorbed.
