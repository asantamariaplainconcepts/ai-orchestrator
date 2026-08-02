# Tasks: guided-automation-form

## 1. Kit (design D3)

- [x] 1.1 `shared/ui/switch.tsx` — same border, focus ring and `aria-invalid` treatment as `input`
      and `textarea`.
- [x] 1.2 `shared/ui/radio-group.tsx` — same, with a visible checked state that survives the theme
      toggle in both directions.

## 2. The form (design D1, D4, D5)

- [x] 2.1 Replace the four-column grid with three numbered sections; every existing field keeps its
      id, its handler and its place in the request.
- [x] 2.2 Question one: trigger label, trigger state.
- [x] 2.3 Question two: action (stays — D5), prompt file with its datalist and degradation hint
      untouched, runtime and timeout on one row, approval as a consequence-stating toggle.
- [x] 2.4 Question three: a two-option choice that reveals or hides the existing chips-and-datalist
      control. "Stop" stores the same empty array it stores today.
- [x] 2.5 The live sentence (D2), above the questions, updating as state changes.
- [x] 2.6 Copy in `shared/i18n` for the section headings, the approval consequence, the two
      after-options, and the sentence's fragments.

## 3. Tests

- [x] 3.1 **Not a component test — there is no frontend test runner.** No vitest, no jest, no test
      files: the assertion had nowhere to live. Covered end-to-end instead, against the artifact:
      fill the form, submit, read the Automation back from the API. That is stronger than a
      component assertion would have been, since it witnesses what was stored rather than what was
      sent.
- [x] 3.2 E2E: the three sections are present, the approval control states its consequence, and
      choosing "stop" hides the label control while "hand on" shows it.
- [x] 3.3 `EveryImplementedRuntimeAndVendor_Should_BeSelectableFromTheForm` still passes untouched —
      the reachability guarantee D5 is about.
- [x] 3.4 Two mutations, and **both findings mattered**:
      - Reverting the payload to `withDraft()` and hiding nothing — the suite stayed green, because
        **the E2E serves the built bundle from `wwwroot`, not the source.** A frontend mutation check
        that does not re-run `pnpm build` tests the previous bundle. Rebuilt; the control-visibility
        mutation then reddened.
      - The payload mutation *still* passed. `Stopping_Should_StoreTheEmptyLabelSetItHasAlwaysMeant`
        typed no label, so `withDraft()` returned `[]` under both behaviours — the exact #189 shape,
        an assertion the old code also satisfied. Rewritten to type a label, then choose stop, which
        is the only path where the two differ. Now reddens with "should be 0 but was 1".

## 4. Gates

- [x] 4.1 `tsc`, eslint, prettier, the design-system validator, and the full non-E2E suite.
- [x] 4.2 `docker build` of the portal image — #207 shipped an unbuildable image past every other
      gate, and no workflow builds the images yet.

## 5. Found during implementation

- [x] 5.1 The live sentence said "the chain stops there" after choosing *hand on* but before naming
      a label — D2 promises to name what is missing, and this was the one gap it did not name. Found
      in the browser, not by a test. Now renders "hands on to … (name a label to hand on to)".
- [x] 5.2 No new dependency was needed: `radix-ui` 1.6.7 already exports `Switch` and `RadioGroup`,
      so both kit additions are wrappers in the house style rather than a package decision.
