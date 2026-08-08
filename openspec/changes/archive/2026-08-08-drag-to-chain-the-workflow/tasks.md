## 0. The order this change was built in

- [x] 0.1 **Stated plainly, because the branch's history shows it:** the implementation was written
      first, from the design review, and the issue and this bundle were written afterwards to land
      it through the normal path. So this file records work that already exists rather than
      planning work to come, and the retro should count it as the process inversion it is.
      Nothing here is aspirational — every box below was ticked against something observed.

## 1. The rules, apart from the picture (design D3)

- [x] 1.1 What a drop rewrites and what refuses it live in `chainDrag.ts` as functions of the
      Automations alone — no React, no DOM. Four refusals: self, an edge that already exists, a
      trigger shared with another enabled Automation (BR-003), and a loop found by walking output
      labels (the same thing the graph derives from).
- [x] 1.2 `rewritesFor` returns the label changes rather than performing them, so the caller stays
      the one place an Automation update is made (design D4, inherited from #137).

## 2. The gesture (design D1, D2, D4)

- [x] 2.1 Catalogue rows and each step's grab handle start the drag; the carried Automation is held
      on the section that renders both surfaces, because `dataTransfer.getData` is empty during
      `dragover` and a slot must know what is over it to say anything (design D4).
- [x] 2.2 Every gap renders the wiring its drop would perform; the end gap renders its one clause.
- [x] 2.3 A refused gap shows the rule and never accepts the drop.
- [x] 2.4 Dropping a step on the catalogue clears whatever handed to it, and invents nothing.

## 3. The board preview (design D5)

- [x] 3.1 Collapsible, read-only, derived from the same `workflowChains` the canvas draws. Gated
      columns marked, the human stop drawn where the flow ends, the just-placed column
      distinguished.

## 4. The field that was being cleared (design D6)

- [x] 4.1 One builder now constructs the whole-Automation request for all three callers. Found by
      reading the canvas while adding a third caller: it resent every field except `model`, which
      #291 had added days earlier, so **any gesture on the picture silently reverted a chosen model
      to the deployment's**. A regression already on `main`, fixed here.

## 5. Proof

- [x] 5.1 **Exercised in the browser** against the mock, by dispatching the real drag events —
      synthesized mouse events do not start an HTML5 drag, which is #110's finding again. Observed:
      the gap read "ai:grill will hand to ai:estimate · ai:estimate will hand to
      ready-for-proposal"; the end gap read "ready-for-proposal will hand to it"; dropping
      `ai:estimate` into the first gap produced the chain ai:grill → ai:estimate →
      ready-for-proposal, the header moved to "3 steps", the catalogue moved that row to
      "in workflow", and the preview grew a highlighted `ai:estimate` column between the other two.
      Dragging a chained step over its own gap refused with "ready-for-proposal cannot hand work to
      itself". Checked in both themes.
- [x] 5.2 **Two fixture defects found and fixed, both of which had been hiding real behaviour.**
      The mock had no update route at all, so every canvas gesture — including the human block
      since #137 — was undemonstrable there: the request 404'd and the picture never moved. And the
      first version of that route mutated the automations array in place, which made the chain show
      three steps while the catalogue still said "standalone" and the header still said "2 steps" —
      React Query keeps previous data when a refetch returns the same reference, so the change
      reached whichever component happened to re-render. ADR-0016's rule, met by a fixture that now
      replaces rather than mutates.
- [x] 5.3 Gates: tsc, ESLint, Prettier, the production build, the design-system validator's three
      stages, `openspec validate --strict`, and the 45 end-to-end tests against the rebuilt bundle.

## 6. Known gap

- [ ] 6.1 **The gesture itself is not under automated test, and this change does not close that.**
      Playwright cannot perform an HTML5 drag (#110) and this repository has no frontend unit
      runner, so `chainDrag.ts`'s pure functions — written to be testable precisely for this
      reason — have no tests. Adding a unit runner is a decision for the repository owner rather
      than a step to take inside a feature change, so it is left open here rather than quietly
      absorbed.
