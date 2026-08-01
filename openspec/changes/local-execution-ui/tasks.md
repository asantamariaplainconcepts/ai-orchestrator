## 1. Foundations

- [x] 1.1 Load the `aio-design` skill before any surface work; add the locus chip component
      (monitor glyph + word; icon + text, never colour alone) to the kit vocabulary
- [x] 1.2 i18n catalogue entries for all five surfaces (settings, dialog, chips, execution block,
      banner, checklist) — no hardcoded JSX copy

## 2. Connector settings (mock 3a)

- [x] 2.1 Probe the code-source surface once per project page; on 404 render no code-source UI at
      all (design D1)
- [x] 2.2 Segmented control Repository / Local folder; mono path input with live validation naming
      the failing check; loading/empty/error/success states
- [x] 2.3 Recent folders (≥44px targets, "used by {project}"); selection re-runs live validation
- [x] 2.4 Warning callout stating the pod constraint

## 3. Run now dialog (mock 3b)

- [x] 3.1 No local folder → dispatch exactly as today, no dialog
- [x] 3.2 LocalFolder project → dialog with radio cards (≥48px) stating consequences; disabled pod
      card carries its reason; primary button repeats the chosen locus
- [x] 3.3 BR-001 / BR-013 / clean-tree refusals render inside the dialog, aria-live polite, naming
      the folder

## 4. Run detail and projects list (mocks 3c, 3e)

- [x] 4.1 Locus chip beside the state pill; Execution block in the rail (runtime · kind, working
      folder, branch created, output by locus)
- [x] 4.2 Changes card names the created branch for local Runs
- [x] 4.3 Quiet outline "Local" badge on LocalFolder projects in the list

## 5. Onboarding (mock 3d)

- [x] 5.1 Persistent local-owner banner (`role="status"`, warning family) keyed on the sentinel
      principal
- [x] 5.2 Three-step checklist on Operate, derived live (connector / automations / terminal Run),
      no stored progress; retired permanently by any terminal Run
- [x] 5.3 Step 3 wires the set-up-defaults action — **sequenced behind #212's merge**; until then
      the step guides without the button (design D4)

## 6. Proof

- [x] 6.1 Browser-preview verification of all five surfaces in light + dark, including the
      cloud posture (no code-source UI) and the dirty-tree refusal inside the dialog
- [x] 6.2 Zero legacy-kit classnames; full gates (build, tests, lint) and spec validation
