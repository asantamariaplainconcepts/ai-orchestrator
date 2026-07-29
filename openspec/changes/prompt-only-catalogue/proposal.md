# Proposal: prompt-only-catalogue

## Why

Issue #162 (ACT-001 resets the product claim, ACT-003 executes; UC-005, UC-006 vocabulary; UC-016–UC-019,
UC-024, UC-025 become realisable via prompts). The owner's call, reaffirmed with the costs on the table
(DEC-003): every built-in action leaves and an Automation becomes **the repository's own prompt,
unbounded**. The agent holds the project PAT and a workspace and does whatever the prompt says.

**This deliberately inverts #150's central argument.** That change said the prompt is untrusted text and
*what it can do is decided here, not there* — one comment, no labels, no pull request. #162 replaces that
with *anything today; limitations arrive later as per-Automation grants*. The inversion is the point, and
it is recorded rather than smoothed over, because the safety reasoning #150 published is now wrong on
purpose and a future reader must not mistake this for drift.

## The scope question the issue did not settle, and its answer

The issue says the orchestrator "performs **no** vendor or repository write of its own afterward". Taken
literally that removes `HandOn` — the output-label write at `RunExecutor.cs:197` — which is named in
neither of the issue's lists.

**It stays**, and the reason is a separation this product already made. DEC-053 split the **catalogue**
(what one step does) from the **workflow** (how steps connect). #162 retires the catalogue: what a step
does becomes the repository's prompt, unbounded. It says nothing about retiring the workflow.

So the rule reads as it was meant: the orchestrator stops performing the **action's ceremony** — publishing
a pull request, setting a state, applying an estimate, posting the reply. When `HandOn` writes the next
trigger label it is not finishing the agent's job; it is executing what the Automation — this product's own
configuration, not the prompt's request — declared should happen on success.

That line keeps the canvas, the human-review block (#137) and the board's chain ordering (#128) alive,
which matters: all three were merged the same day, and deleting them here would have retired the workflow
as a side effect of retiring the catalogue.

## What changes

- **One action.** `RepositoryPrompt` is the only valid value; every other enum member goes, and so does
  the executor's entire ceremony — the workspace-and-publish path, the comment/state/estimate write
  switch, the grill's rubric conversation, the propose and sync procedures.
- **The agent gets the workspace and the PAT and finishes the job.** Outcome, log and usage come from
  the agent's result; the orchestrator writes nothing to the vendor afterward.
- **`OutputLabel` and `HandOn` stay**, with the canvas, the human block and the board's chain ordering —
  they are the workflow, and the workflow is not what this change retires.
- **The seeded defaults go**, to return as prompt+grant bundles.
- **The machinery that stays, stays whole:** triggers and matching (BR-003), one active Run and the cap
  (BR-001/BR-002), dispatch, timeouts (BR-005), cancellation's pre-start boundary, the live log (UC-027),
  usage (BR-011), terminal states (BR-004), and the two-phase approval routing (BR-007).
- **A decision records the inversion** — revising DEC-026, DEC-048 and DEC-057 — including the two
  promises that stop being code-enforced, and the grants follow-up by name.

## Impact

- Specs: `agent-execution` (five REMOVED, one MODIFIED), `automation-configuration` (three REMOVED, one
  MODIFIED). `backlog-mirror` is untouched, because the board's chains survive.
- Code: 41 files name a removed action, 31 of them tests. `RunExecutor` is 1035 lines and most of it
  goes. One migration deletes Automations naming a removed action; no column is dropped.
- Docs: a new DEC, and ARCHITECTURE.md's action section rewritten.

## What is deliberately accepted, in the issue's own words

Two promises become prompt-level rather than code-enforced until grants land: **"a plan phase publishes
nothing"** (the approval gate) and **"a cancelled Run produces no pull request"**. Both were true because
the executor did the publishing and could refuse to; with the agent publishing, neither is enforceable
here. Stated in the DEC, not buried.

## Out of scope

- The grants model — the named follow-up, and the thing that makes the two promises above enforceable
  again.
- Any change to Run states, dispatch, matching or the live log. `AwaitingInput` and its resume path stay
  in place even though nothing produces them once the grill is gone: deleting Run states is explicitly
  out of scope, and an unreachable path is cheaper than a state machine change made in passing.
- Seeding example prompt files into projects' repositories.
