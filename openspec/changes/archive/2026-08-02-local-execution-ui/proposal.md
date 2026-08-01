## Why

Issue #211. #210 landed the self-host flavour's backend — a folder as code source, per-run
execution locus, live path validation, the clean-tree refusal, the `local-owner` principal — and
none of it is visible: no settings surface offers the folder, Run now cannot choose a locus, a
Run's detail does not say where it executed, and a fresh self-host owner gets no path from empty
project to closed loop. The capability is UC-004 (configure), UC-012 (Run now) and UC-021 (view
Runs) growing their local halves; every consequence (BR-001/BR-013 refusals, the clean-tree
refusal) must surface verbatim where the gesture happened.

## What Changes

One UI change, five surfaces — all consuming #210's landed API, no new backend:

- **Connector settings (mock 3a):** Repository / Local folder segmented control, rendered only
  where the API offers the surface (probe once; on cloud the whole control does not exist — the
  API answers 404 there). Mono path input with live validation naming the failing check; recent
  folders (≥44px targets, "used by {project}"); a warning callout stating the pod constraint.
- **Projects list (3e):** quiet outline "Local" badge (monitor glyph + word) on LocalFolder
  projects — same chip vocabulary as the Run locus chip.
- **Run now (3b):** grows into a dialog only when a genuine choice exists; radio cards (≥48px)
  stating consequences; the primary button repeats the choice; the dirty-tree refusal renders
  inside the dialog, aria-live polite, naming the folder.
- **Run detail (3c):** locus chip beside the state pill; an Execution block in the rail
  (runtime · kind, working folder, branch created, output = local branch vs PR link); the Changes
  card names the branch for local Runs instead of pretending to read a working tree.
- **Onboarding (3d):** persistent local-owner banner (`role="status"`, warning family) whenever
  the principal is the `local-owner` sentinel; a three-step "close the loop" checklist on the
  Operate tab derived live from connector/automations/runs — no stored progress — gone permanently
  once any Run reaches a terminal state. Step 3 invokes the set-up-defaults action (#212, in
  code-review) — that step's wiring is sequenced behind #212's merge.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `frontend-architecture`: the project page's configure/operate split gains the code-source
  surface, the Run now locus dialog, the Run detail execution block, the Local badge, and the
  local-owner banner + onboarding checklist — with the probe-once/hide-on-cloud rule and the
  states/accessibility bar stated as requirements.

## Impact

- **Frontend only**: `src/frontend/features/backlog` (settings, project list, Run now),
  `src/frontend/features/runs` (Run detail), shell (banner), plus the typed i18n catalogue.
  Zero legacy-kit classnames; light + dark from theme tokens; severity/locus never colour alone.
- **Backend**: none beyond consuming #210's endpoints and (for checklist step 3) #212's action.
- **Dependencies**: #210 merged (hard, satisfied); #212 in code-review — checklist step 3's
  action binds to its validated contract and its implementation task is sequenced behind the merge.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
