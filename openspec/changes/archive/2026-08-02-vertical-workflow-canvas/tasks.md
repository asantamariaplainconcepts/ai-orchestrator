# Tasks: vertical-workflow-canvas

## 1. Layout

- [x] 1.1 Delete all three `xl:` forks; the chain is a column at every width, `max-w-[520px]`.
- [x] 1.2 Branch rows indent under the step they leave, keeping the `branchedFrom` chip.
- [x] 1.3 `HumanStepBlock` visible at every width — the reordering capability, restored.
- [x] 1.4 Replace `shrink-0` with `min-w-0` on the node wrapper and the connector (design D2).

## 2. Node and connector

- [x] 2.1 Node header: trigger, gate chip, state, and compact actions. The two stacked full-width
      buttons are gone.
- [x] 2.2 `GateChip` extracted to `shared/ui/gate-chip.tsx`; the board imports it.
- [x] 2.3 The hint is a prop (design D3) — the board's "dropping here…" sentence does not belong on
      the canvas.
- [x] 2.4 The dangling-label warning moves to the node that owns the label. Same condition as
      before (`!connected && outputLabels.length > 0`) — `connected` is passed in, because only the
      graph knows it.
- [x] 2.5 The connector's select is revealed by a named button instead of always rendered.

## 3. Tests

- [x] 3.1 E2E at 375px: the drag block is visible — the capability that did not exist below `xl`.
- [x] 3.2 E2E: the chain is a column and does **not** scroll sideways. Scoped to the canvas, not the
      document: the page genuinely overflows at that width because the project tab strip is 528px
      wide, which predates this change and is filed separately. Asserting on the document would fail
      for somebody else's defect.
- [x] 3.3 E2E: no select until a named button is clicked, then one appears.
- [x] 3.4 E2E: a gated step wears the shared chip, located by its surface-specific tooltip.
- [x] 3.5 Mutation check — see section 4, which is where the real findings are.

## 4. What the mutation check actually found

- [x] 4.1 **`rtk pnpm build` reported success for a build that failed.** The mutation broke
      TypeScript, the build errored, `wwwroot` kept the previous bundle, and `rtk` still exited 0 —
      so `&& echo "built"` printed. The E2E then tested the old artifact and passed, which read as
      "the test does not cover this". Re-run through `rtk proxy` and verified by grepping the class
      out of the bundle, the layout mutation reddens correctly. Same masking class as the known
      `rtk git commit` issue; recorded to memory.
- [x] 4.2 **The E2E serves `wwwroot`, not the source** — the #231 finding, repeated here one change
      later despite having written it down. Remembering is not sufficient; the build has to be part
      of the loop.
- [x] 4.3 A test asserted a scenario the product does not render: a single Automation never appears
      on the canvas ("No Automation hands work to another yet"), so seeding one and waiting for a
      node waited for something that was never coming. Reseeded as a real chain.
- [x] 4.4 Two tests flaked between runs because the helper waited for the "Workflow" heading, which
      renders before the automations load. Waiting for a node instead — #107's lesson.

## 5. Gates

- [x] 5.1 `tsc`, eslint, prettier, design-system validator, 440 non-E2E, 38 E2E.
- [x] 5.2 `docker build` of the portal image.
