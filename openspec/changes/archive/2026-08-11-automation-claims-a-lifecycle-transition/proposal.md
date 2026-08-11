## Why

[#310](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/310). An Admin
(**ACT-001**) cannot lay out a project's flow in the place they read it, and cannot change its order.
Both complaints are *unrepresentable* rather than unimplemented: the flow is derived from labels, so
the first step's trigger **is** the entry point and there is no "before" to put anything into, and a
picture derived from labels has no stored order to change. The same absence makes a step that waits
for a person read as a gap in the wiring rather than as a person's turn.

Primary use case **UC-006** (an Admin edits an Automation — claiming and moving a transition is an
edit), with **UC-005** (creation now names the transition it claims), **UC-007** (the board's columns
change meaning from enabled trigger labels to lifecycle stages) and **UC-013** (the Plan approval gate
must stay distinguishable from an unclaimed transition). Rules: **BR-003** (one claimant per
transition), **BR-006** (a human wait is untimed and is not a fault), **BR-007** (approval routing
stays its own wait), **BR-009** (a Member observes and configures nothing). **BR-008** is unchanged —
the vendor stays the source of truth and a lifecycle move is still UC-008's licensed label write.

## What Changes

A project's Story lifecycle becomes a **linear, ordered list of stages**, stored on the Project. An
**Automation claims exactly one transition** along it: its trigger label is the *from*-stage, and a new
single-valued *to*-stage is the label its Run applies on success. A **human step needs no
representation** — it is a transition no Automation claims, so nothing fires until a person moves the
label. That works because the hand-off already travels through the vendor label: a Run applies its
labels through UC-008's write (`RunExecutor.cs:196-231`), they return as an ordinary `StoryChanged`
and are matched like any other label (`StoryChangedHandler.cs:59`). **A person moving a label is
already the same mechanism as an Automation applying one.**

- **BREAKING (data shape, migrated).** `Automation.outputLabels` stops doing double duty. Today one
  field means both the workflow's outgoing edge *and* a mark on the Story
  (`Domain/Automation.cs:54-56`). After this change the transition is the edge and every remaining
  output label is **only** a mark. Separating them is the substance of the change.
- The **Backlog board becomes the authoring surface.** Every stage is a column whether or not an
  Automation claims the transition into it; an Automation is drawn on the boundary it claims; an
  unclaimed boundary reads as a person's turn, with no validation error and no timer (BR-006).
- A **stage can be placed first**, and the flow can be **reordered**, through explicit controls on the
  boundaries. Stages appear only as a consequence of a claim and are never pruned.
- **Branching is removed**, not deferred: `hasBranches` and `chain.branchedFrom` cease to exist and no
  API, form or view accepts or draws a second transition.
- **The second drawing of the flow is deleted**: `WorkflowCanvas.tsx` (267), `Connector.tsx` (191) and
  `DropSlot.tsx` (64) go — 522 lines removed. The catalogue keeps create, edit, disable, re-enable
  and delete (UC-005, UC-006).
- A **hand-written** migration carries every configured hand-off across. A scaffolded
  `DropColumn` + `AddColumn` is forbidden here by precedent: `20260730222648_OutputLabelSet.cs:9-19`
  records that the generated version "would have silently discarded every hand-off configured in the
  deployment: every workflow edge, gone, with the schema perfectly correct afterwards."
- **ADR-0022** lands with the change (`docs/adr/README.md:13-16` — an ADR is written in the change
  that noticed the recurrence). It supersedes one clause of **DEC-053**, *"membership is derived from
  the edges and never stored"* (`docs/product/mvp/10-locked-mvp-decisions.md:209-221`); the rest of
  DEC-053 stands. A second, unrelated DEC-053 (Connector permissions) exists at `:367` in that file
  and is **not** the decision cited.

**Integration contracts.** No queue message schema change, no Aspire or host `csproj` change, no CI
change. The Projects → Runs contract `AutomationDetail` (`IAutomationCatalog.cs:31-56`) gains the
claimed to-stage; `PUT /api/projects/{id}/automations/{id}` keeps replacing the whole Automation, so
ADR-0019's one-builder discipline applies to the new field on every client.

## Capabilities

### New Capabilities

None. The stored stage list is not a capability of its own — an Admin never authors it directly, and
authoring it (renaming, removing, seeding a default) is explicitly out of scope for #310. It exists
only as a consequence of claiming a transition.

### Modified Capabilities

- `automation-configuration`: the workflow stops being a derived picture and becomes a stored,
  ordered lifecycle that an Automation claims one transition of; the canvas requirement and the
  human-review-block requirement are retired; branching is removed.
- `backlog-mirror`: the board's columns become the project's lifecycle stages rather than its enabled
  Automation trigger labels, an unclaimed boundary is a person's turn rather than a column derived
  from an absent output label, and the board is where the arrangement is authored.

## Impact

**Backend** (`src/modules/Projects`): `Domain/Automation.cs` (the claimed transition; output labels
become marks), `Domain/Project.cs` (the ordered stage list), `Persistence/ProjectsDbContext.cs:53-75`,
one **hand-written** migration under `Persistence/Migrations/`,
`Features/Automations/UseCases/CreateAutomation.cs` and `UpdateAutomation.cs` (request, validation,
the claim), `Features/Automations/OverlapGuard.cs:35-53` (BR-003 under its new meaning),
`Features/Automations/AutomationCatalog.cs:55`, `Features/Automations/UseCases/DiscoverPipeline.cs`,
`SetUpDefaultAutomations.cs`, `StarterCatalogue.cs` (the starter tiers wire hand-offs, so they now
claim transitions), and the contract `AiOrchestrator.Modules.Projects.Contracts/IAutomationCatalog.cs`.

**Backend** (`src/modules/Runs`): `Features/Execution/RunExecutor.cs:196-231` applies the claimed
to-stage as the lifecycle move alongside the marks. `Features/Matching/StoryChangedHandler.cs`
unchanged — that is the point.

**Frontend** (`src/frontend/features`): `backlog/KanbanBoard.tsx` (columns from stages; boundary
controls), `automations/AutomationsSection.tsx` (the catalogue keeps its CRUD, loses the canvas),
`automations/workflowGraph.ts`, `automations/chainDrag.ts`, `automations/planHandoff.ts`,
`automations/BoardPreview.tsx`, `automations/AutomationNode.tsx`, `automations/automationRequest.ts`,
`automations/types.ts`, `automations/useChainRemoval.ts`, `shared/i18n/en.ts`, `shared/http/mock.ts`.
**Deleted:** `automations/WorkflowCanvas.tsx`, `automations/Connector.tsx`, `automations/DropSlot.tsx`.

**Docs**: `docs/adr/0022-an-order-a-person-can-rearrange-is-stored.md` (new),
`docs/adr/README.md`, and DEC-053's supersession note in
`docs/product/mvp/10-locked-mvp-decisions.md`.

**Not touched.** The queue, dispatch, KEDA scaling, the Connector seam, the label write itself
(UC-008), BR-001/BR-002 run badges on cards, and `requiresApproval` — folding human review into it is
out of scope and the crux that would have needed deciding dissolves, because a human step needs no
representation.
