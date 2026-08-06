# Proposal: select-setup-steps

## Why

[#262](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/262). #233 made the setup
card say what it would create before the button — and then offered one button for the whole list. An
Admin who wants eight of the nine steps has no way to say so. The only route to a catalogue they
actually want is to accept the plan and then delete or disable what they did not ask for, which
means the product writes the Automation first and asks second.

That is the wrong order for the same reason #233 already established for the preview: a plan you can
read but not change is a notice, not a decision. UC-005 in bulk should start the project's catalogue
at the team's own choice, not at the catalogue's.

## What Changes

- **Every row in "What this will create" becomes selectable, and every row starts selected.** Both
  kinds: a row that would wire a file already in the repository, and a row that would install a
  starter. Deselecting the first means no Automation is wired for that step and the file is left
  alone; deselecting the second means no Automation *and* no file written.
- **The confirm sends the selection, and only selected steps are created.** Starters are installed
  only for selected gaps; where no selected gap remains, no branch and no pull request are opened —
  the existing empty-gap short-circuit already does exactly this, and selection filters upstream of
  it.
- **A broken hand-off is marked, and never blocks.** Where a deselected step was handing work to a
  selected one, the plan says so — a person hands on there instead. The manual already states the
  rule this makes visible: *"Where nobody hands on, a person must."* Confirm stays enabled.
- **The report distinguishes excluded-by-choice from skipped-because-existing.** They are different
  facts: one is the Admin's decision, the other is what the project already had.
- **Deselection is per-invocation.** Nothing is stored. Reopening the panel, or running setup again
  later, proposes every missing step again with every row selected — which is what keeps the action
  idempotent and convergent, as `default-automations` already requires.
- Not breaking. `Steps` is a new optional field; omitting it means every step, so the bodyless #212
  call and every existing caller behave exactly as before. `InstallMissing` is deliberately kept —
  see design D3.

## Honest note on scope

The catalogue has exactly two hand-off edges today: `ai:implement → ai:tests` and
`ai:tests → ai:review` (`Starter/manifest.json`). Every other entry has empty `outputLabels`. So the
issue's own example — deselecting `ai:triage` — correctly marks nothing, because `ai:triage` hands
work to nobody. The marker earns its place on `ai:tests`, whose removal orphans `ai:implement`'s
hand-off and cuts `ai:review` off the chain. The capability is written for the general case; only
that one case is reachable with today's manifest.

## Capabilities

### New Capabilities

None. This extends two capabilities that already exist.

### Modified Capabilities

- `automation-configuration`: the plan the setup card shows before the button becomes selectable —
  rows carry a selection, all selected by default, and a hand-off broken by deselection is marked
  without blocking the confirm.
- `default-automations`: the setup action accepts the selection, creates only the selected steps,
  installs starters only for selected gaps, and reports what was excluded by choice as a fact
  distinct from what was skipped because it already existed.

## Impact

**API** — `POST /api/projects/{id}/automations/set-up-defaults` gains an optional `steps` array on
its request and an `excluded` array on its response. Additive: an absent `steps` means every step.
`GET .../discover-pipeline` gains `outputLabels` on each plan row, so the card can compute a broken
hand-off without a round-trip per click.

**Code** — `DiscoverPipeline` (`PlannedStep` gains output labels),
`SetUpDefaultAutomations` (`Request`/`Command`/`Response`, and the filter ahead of the wiring loop),
`WorkflowSetupSection.tsx` (selection state, row controls, the gap marker, the confirm),
`useWorkflowSetup.ts` (both mirrored types), `en.ts` (new copy), `shared/http/mock.ts` (whose
discovery stub predates `plan` entirely and renders an empty plan today).

**Not affected** — no migration, no new column, no new permission (BR-009's existing
`ManageAutomations` gate covers it), no change to the queue message schema, Aspire wiring, or CI. No
`OPN-*` decision is open.
