## Why

Reviewing a change usually ends in "please change X" — and today the product cannot act on that.
The loop it just gained (#274) shows the open changes waiting for review; acting on one still
means leaving the product and editing by hand, or waiting for a human with a checkout. Issue
[#275](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/275): from a change's
entry, a Member launches a Run with an instruction typed on the spot, and the Agent does the work
**on the change's own head branch and pushes to it** — the same PR updates in place. No new PR, no
new branch: the unit of review stays the unit of review.

This is the issue that introduces the **Run without a Story**, and its governing decisions were
made at grill by the product authority (DEC-003): concurrency is one active Run per change (the
BR-001 analogue, with the change taking the Story's place); cost lands on the project like any
Run's; the permission is Run now's (UC-012); and there is no approval phase — the launch is the
human intent, exactly UC-012's reasoning.

## What Changes

- A Run MAY target an open change instead of a Story: the Run aggregate carries an optional
  change target (number + URL), and its Story and Automation become optional together with it —
  one new kind of Run, not a second aggregate.
- The instruction is **ad-hoc text**, carried on the Run and readable in its detail afterwards —
  a Run's record, never a new Automation (decided at grill; the scratchpad precedent, #189, with
  execution).
- The workspace seam gains a push-without-publish: check out the change's existing head branch
  (the install path's `Prepare(branch)` already does this) and push to it, opening nothing.
- A new endpoint launches it; the Inbox's change entries offer it; the Runs list identifies a
  change-targeted Run by its change number the way story Runs show their Story id.
- **New business rule (id at proposal review): one active Run per change.** A second launch while
  one is active is refused naming the active Run. A Story Run and a change Run do not contend.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `run-orchestration`: one added requirement — a Run can target an open change with an ad-hoc
  instruction, with the concurrency rule, the no-approval-phase rule, the cost/permission
  inheritances, and the record-not-configuration rule for the instruction.
- `agent-execution`: one added requirement — the workspace ceremony can update an existing
  change: named-branch checkout plus push-without-publish, with the stage-named refusals the
  ceremony already speaks.

## Impact

- Runs domain + EF migration: optional change target, optional Story/Automation (invariant: a Run
  has exactly one target), instruction text.
- Dispatch + executor: a change-targeted path that skips prompt-file resolution (the instruction
  is the prompt), prepares the head branch, and ends in push, not publish.
- New use case `RunOnChange` (endpoint under the project, per BR-009's permission naming).
- Frontend: launch affordance + instruction dialog on the Inbox change entries; Runs list/detail
  identity for change Runs.
- Tests: functional (concurrency, no-contention with story Runs, refusal shapes, record of the
  instruction), E2E stub coverage as reachable.

No `OPN-*` open. The Story-less decisions are recorded in #275 rather than depending on an
unrecorded one.
