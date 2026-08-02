## Why

Issue #229. A connected project reaches a working pipeline through three separate gestures:
install each starter as its own draft pull request (#214), press *Set up defaults* to create the
Automations (#212), and then read that endpoint's report to learn which prompts are still missing.
The report names the gap; nothing closes it.

For a repository that already has a pipeline, the sequence is not merely tedious — it is wrong.
`ds-connect` carries `.claude/commands/ds/` with `grill`, `propose`, `implement`, `refine`, `sync`
and `status`: the same steps this product seeds, written for that team. *Set up defaults* there
installs a second, weaker copy under `ai/prompts/` and wires the Automations to the copy. The
product would overwrite a team's own conventions with its opinion of them, which is the failure
DEC-048 already named for the grill's rubric — "the rubric is always the project's own document,
because a product-wide readiness bar would impose one team's standards on every repository".

## What Changes

- One action **adopts what the repository has and installs only what it lacks**: it discovers the
  prompt files a project already carries, wires Automations to them, and offers starters for the
  steps with no file.
- **Discovery proposes; the human confirms.** The conventional locations are searched — the
  configured directory, `ai/prompts`, `.claude/commands` and its immediate subdirectories — and
  what was found is shown *before* anything is written. More than one candidate means all are
  offered and none is chosen silently.
- **Wiring is by name, and a trigger is never invented.** A file whose name matches a pipeline
  step gets that step's trigger and hand-off labels; a file that matches nothing is reported
  found-but-not-wired.
- The code source is read from the Connector rather than asked (#210/#211).
- One report at the end: directory chosen, Automations created, Automations skipped and why, files
  found but not wired, starters installed and their pull request.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `automation-configuration`: setting a project up becomes adoption-first — the seeded pipeline is
  the fallback for what a repository lacks, not the thing imposed on it — and the prompts
  directory gains a discovery step that proposes rather than assumes.

## Impact

- **Backend**: the Backlog seam's directory listing (#215) is called for several candidate paths;
  a discovery query returns what each holds. `SetUpDefaultAutomations` gains the adoption path and
  its report; `InstallStarterPrompt`'s publish pipeline is reused for the gaps, in **one** pull
  request rather than one per starter.
- **Frontend**: the Automations tab's setup action becomes propose → confirm → report.
- **Unchanged**: BR-003's skip-never-collide (#212's convergence rule), the starter catalogue
  (#190), what a Run does with a prompt (#150/#162), and the deployed habitat.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
