<!-- Note on the checkboxes: this change entered the loop at implementation. The code landed as
     d5cfbe7 (rebased onto ebff9f7) before the issue, proposal and specs existed, so the tasks
     already satisfied are checked and cite what verified them. Group 6 is deliberately open: it is
     the reconciliation any spec review might require, and it cannot be closed before that review
     happens. -->

## 1. Shared building block

- [x] 1.1 Add `shared/ui/responsive-dialog.tsx`: one panel with a centred `Dialog` at pointer widths
      and a bottom `Sheet` below `md`, exactly one mounted, state owned by the caller (design D1).
- [x] 1.2 Give it an optional sr-only title, so a panel whose content carries its own heading stays
      named in the accessibility tree without showing the words twice.
- [x] 1.3 Lay the desktop container out as rows (`auto / 1fr / auto`) so the body scrolls and the
      footer stays put.

## 2. Derivation the rail depends on

- [x] 2.1 Add `workflowMembers(automations)` to `features/automations/workflowGraph.ts`, derived from
      `workflowChains()` so the rail's tag and the drawn graph cannot disagree (design D3).

## 3. Copy

- [x] 3.1 Add the new keys to `shared/i18n/en.ts`: `common.cancel`, `automations.editTitle`,
      `automations.inWorkflow`, `automations.standalone`, `automations.standaloneGroup`,
      `automations.tools.tryPrompt`, `automations.tools.setup`, `automations.tools.more`,
      `automations.delete.start`, `automations.delete.confirm`.
- [x] 3.2 Remove `automations.new.close` with the toggling button it labelled.

## 4. The tab

- [x] 4.1 Move the create/edit form into `ResponsiveDialog`, keeping the three questions and the live
      sentence unchanged; pin the sentence at the top of the scrollable body.
- [x] 4.2 Submit from the footer via `form="automation-form"` so Save survives a long scroll
      (design D2).
- [x] 4.3 Move delete into the footer behind a two-step confirm, and move enable/disable beside it;
      remove all three from the catalogue rows (design D4).
- [x] 4.4 Report the save, delete and enable refusals inside the panel.
- [x] 4.5 Reorder the tab: workflow first, catalogue as a rail at `xl` (`minmax(0,1fr)` + a fixed
      rail column).
- [x] 4.6 Make each catalogue row a single full-width button named for the Automation it edits,
      showing trigger, disabled marker and relation tag.
- [x] 4.7 Below `xl`, hide the in-workflow rows and switch the group heading to *Standalone*; hide
      the rail entirely when nothing is standalone (design D6).
- [x] 4.8 Add the toolbar: *Try a prompt*, *Set up from repo…*, *New Automation*, with the two tools
      folding under a `⋯` menu below `md`.
- [x] 4.9 Render `WorkflowSetupSection` inline while no Automation exists, and drop the toolbar's
      setup action in that state (design D7).
- [x] 4.10 State the derived step and human-stop counts in the tab header.

## 5. The canvas

- [x] 5.1 Compact each node to one line (trigger, Gate chip, prompt file, state) and add an Edit
      affordance that opens the same panel (design D5).
- [x] 5.2 Preserve the DOM shape the canvas tests measure: `[node, connector]` children,
      `max-w-[520px]` on the chain, Gate chip ahead of the approval toggle.
- [x] 5.3 Remove the canvas's own summary paragraph, now stated by the tab header.
- [x] 5.4 Stop stacking `WorkflowSetupSection` and `PromptScratchpad` on the tab in
      `features/backlog/ProjectScreen.tsx`.

## 6. Reconcile with the spec review

- [x] 6.1 Apply whatever the spec review (HITL #1) changes in the delta specs, or record that it
      changed nothing. — **It changed nothing.** The proposal, the design and the
      `automation-configuration` delta were validated as written, including the two things flagged
      for close reading: the reconciliation of the wide-viewport chain sentence #232 left behind, and
      the recorded RULE-002 deviation. No requirement, scenario or decision was edited.
- [x] 6.2 Re-run the gates in group 7 after any such change. — Not applicable: 6.1 changed no spec
      and no source file, so the group 7 results stand as recorded. CI re-ran on the PR head and is
      the independent witness.

## 7. Verification

- [x] 7.1 Update the two E2E cases that encoded the old placement:
      `GuidedAutomationForm_Should_Constraint` scopes its text assertions to the dialog (portalled
      outside `main`) and clicks the footer's *Add Automation*;
      `PromptScratchpad_Should_Constraint` reaches its surface from the toolbar.
- [x] 7.2 `npx tsc --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` from
      `src/frontend` — all clean.
- [x] 7.3 `bash .claude/skills/aio-design/scripts/validate-design-system.sh` — all three stages pass.
- [x] 7.4 `pnpm build` from `src/frontend` — production bundle builds and carries no mock adapter.
- [x] 7.5 `dotnet build src/AiOrchestrator.slnx` — 0 errors, so the updated E2E project compiles.
- [x] 7.6 Verify in the browser in mock mode: scroll offset unchanged on open, Esc and save; the
      two-step delete; the rail's relation tags; the compact node with its Gate chip; the first-run
      inline setup with no duplicate toolbar action; light and dark; 1440, 1280 and 375 widths.
- [x] 7.7 Verify the geometric properties the canvas suite asserts still hold: chain is a column at
      375, no sideways overflow, connector offset ≥ 0 at 1280, first `[draggable=true]` visible,
      `[title='A person approves the plan']` first match reads `Approval`.
- [x] 7.8 Verify the composition with #269 after rebasing: its consent switch renders inside the
      relocated setup panel, toggling it grows the plan, and the panel stays within the viewport.
- [ ] 7.9 The full suite including E2E. Left open on purpose until `/aio:sync` reads it: the evidence
      is CI's own `build-test` and `e2e` jobs on the PR head, which boot real PostgreSQL, Azurite and
      the AppHost. A local pass on one machine is weaker evidence than the lane the merge gates on,
      so this closes when that rollup is green — not before.
