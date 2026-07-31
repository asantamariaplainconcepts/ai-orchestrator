# Proposal: output-label-set

## Why

[#165](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/165). The output label is the
workflow's edge (#115/#116): apply it on success and whatever listens on it fires. One label per
Automation means one hand-off and nothing else — no way to also mark the Story (`ai:done`, a category,
a milestone tag), and no way to wire a second listener.

UC-005/UC-006 configure it; UC-008 is the write path it rides; ACT-001 configures and ACT-002 reads the
result on the Story. BR-008 keeps the mirror read-only and the write vendor-first (DEC-027); BR-003 is
untouched, because this changes what an Automation *emits*, never what it matches.

The dependency named in the issue holds: #162's carve-out is that the orchestrator keeps applying
output labels, and it does — that branch was abandoned rather than merged, so `HandOn` is still where
the edge lives.

## What changes

An Automation's output becomes a **set** of labels rather than one. On success every one of them is
applied through the ordinary Connector write, and the canvas draws one edge per label that matches
another enabled Automation's trigger.

- `Automation.OutputLabel` becomes `OutputLabels`, a set — in the domain, in the Contracts detail the
  Runs module reads, in the API and in the form. Existing single-label Automations become one-element
  sets by migration and behave exactly as before.
- The form's single textbox becomes a **picker that also accepts free text**: it suggests the trigger
  labels of the project's other enabled Automations, because picking one is precisely how a sequential
  prompt workflow is wired, and it never offers the Automation's own trigger.
- #115's self-trigger refusal applies to **every** label in the set.
- A label the vendor could not ensure is reported, never silently skipped — and the Run still fails,
  as it does today when its single label does not land.

## What does not change

- **BR-001's ceiling.** Two output labels matching two triggers draw two edges, and at execution the
  second simultaneous match is *ignored, not queued*. Fan-out is wiring; execution serializes. Real
  parallel branches would be a BR-001 revision with its own decision, and this proposal does not make
  one — it states the ceiling where the edges are explained, so the canvas cannot imply otherwise.
- Success-only. No per-outcome labels: a failed or cancelled Run applies nothing, as today.
- The Azure DevOps `EnsureLabel` asymmetry stays reported rather than swallowed, and stays unfixed.
- Matching, triggers and BR-003's overlap rule.

## Impact

- **Breaking within the repository:** `AutomationDetail.OutputLabel` becomes `OutputLabels`. One
  consumer — `RunExecutor.HandOn` — plus the two Automation use cases, the catalog, the canvas, the
  board and the form.
- **Data:** a migration that rewrites the column into an array, preserving every existing value.
- **Specs:** `automation-workflow` gains the set, the picker and the stated ceiling.
