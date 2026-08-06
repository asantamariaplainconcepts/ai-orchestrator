## Why

Editing an Automation moves the page under the reader: the form mounts inline above the catalogue,
so opening one scrolls the Automations tab to the top, pushes everything below it down, and leaves
the Admin hunting for their place after Save. On the same tab, the workflow — the thing looked at on
every visit — sits below two tools most visits never touch, so the tab opens on configuration rather
than on what the project actually runs.

Both are failures of information order on a surface that has grown four full-width cards
(setup, scratchpad, catalogue, workflow) without anyone deciding which one an Admin came for.
Closes [#271](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/271) —
ACT-001, UC-005, UC-006, upholding BR-003 and BR-009.

## What Changes

- **Creating and editing an Automation arrive in a modal panel over the tab** — a centred dialog at
  pointer widths, a bottom sheet below them — instead of a form inserted into the page. The form
  itself is unchanged: the same three questions and the same live sentence, in a different container.
  Page scroll is untouched on open, on dismissal, and on save.
- **The workflow becomes the tab's first content**, with the catalogue beside it as a rail at wide
  viewports rather than stacked above it.
- **A catalogue row states its relation to the flow** — `in workflow` or `standalone` — derived from
  the same graph the workflow draws, and the row itself is the way into the edit panel.
- **Delete and disable leave the catalogue rows for the panel**, and Delete gains a confirmation.
  Delete was previously one un-confirmed click sitting beside Edit, at the width of a mis-aim.
- **Setup and the scratchpad become toolbar actions** that open over the tab, except on a first run:
  with no Automations configured, setup renders inline, because then it is the content of the tab.
- **A workflow node is one line** and offers editing, so the picture and the rail lead to one panel.
- **The workflow's derived summary is stated by the tab header** rather than inside the canvas.

Not breaking. No endpoint, request shape, response shape, or database schema changes, so no
integration contract is affected: Aspire wiring, host csproj, queue message schema and CI are all
untouched. Two existing E2E cases are updated where they asserted the old placement — the
guided-form assertions scope to the dialog, which is portalled outside `main`, and the scratchpad
case reaches its surface from the toolbar.

## Capabilities

### New Capabilities

None. This changes how an existing capability is presented; it adds no behaviour of its own.

### Modified Capabilities

- `automation-configuration`: three requirement-level changes. The editing surface acquires a
  stated container and a scroll-preservation guarantee, where the requirement previously fixed only
  that edit and create share one form. The catalogue's stated content narrows — trigger label,
  enabled state and relation to the flow — and its actions become reachable *through* a row rather
  than rendered *on* it, where the requirement currently says the catalogue "SHALL show each
  Automation's trigger label, action, runtime and whether it is enabled, and SHALL offer every
  action already available". And the tab acquires a required information order, plus the rule that
  first-run setup is inline while every other tool opens over the tab.

## Impact

Frontend only, all under `src/frontend`:

- `features/automations/AutomationsSection.tsx` — the toolbar, the panel, the rail, the first-run
  branch.
- `features/automations/WorkflowCanvas.tsx` — one-line nodes, an edit affordance, the summary
  removed in favour of the header's.
- `features/automations/workflowGraph.ts` — `workflowMembers()`, so the rail's relation tag and the
  drawn graph cannot disagree.
- `features/backlog/ProjectScreen.tsx` — setup and the scratchpad stop being stacked on the tab.
- `shared/ui/responsive-dialog.tsx` — new: one panel, two containers, composed from the existing
  shadcn dialog and sheet primitives. Three consumers in this change, so it is not a seam without
  one.
- `shared/i18n/en.ts` — new keys; `automations.new.close` removed with the toggle it labelled.

Tests: `GuidedAutomationForm_Should_Constraint` and `PromptScratchpad_Should_Constraint` (E2E).

**A pre-existing divergence surfaced, not addressed here.** `automation-configuration` still
requires that "at a wide viewport the chain SHALL be a single row read left to right, scrolling
horizontally within its own container… at a narrow viewport it SHALL read top to bottom instead."
#232 replaced that with one top-down layout at every width, and the E2E suite asserts the column at
1280px. This change neither relies on nor worsens that gap, and correcting a requirement it does not
touch would be scope it was not asked for — recorded here so it is not lost.
