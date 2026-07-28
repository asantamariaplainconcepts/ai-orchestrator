# Design: automation-output-label

## D1 — One field, renamed, not a second one beside it

`ReadyLabel` already means "the label I write when I finish"; the grill is simply the only action
that had a reason for it. Adding `OutputLabel` next to it would leave two fields with one meaning
and an inevitable question about which wins — the exact shape of drift this repository keeps
retiring. So the column is renamed and the concept widens.

What stays specific to the grill is the **default**: `ready-for-proposal` when the field is null
and the action is `GrillToReady`. That default lives in code (grill design D5) and does not move
into data, because a product-wide default output label would silently chain every Automation
someone created without thinking about it.

## D2 — Written once, at success, by the executor

The write goes where the grill's already is: the executor, after the outcome is recorded, only
on `Succeeded`. One call site for every action rather than a per-action decision, so a new action
inherits chaining without knowing it exists.

Failure and cancellation write nothing. A chain is a claim that the previous step *worked*, and
BR-004 already says a failed Run is terminal until a human intervenes — handing work onward would
contradict it.

## D3 — A self-trigger is refused at save, not discovered at runtime

An output label equal to the Automation's own trigger label is a loop with a very confusing
symptom: the first Run succeeds, writes the label, and matching declines the second because
BR-001 sees an active Run — leaving a labelled Story and no work, with nothing anywhere saying
why. Refusing at save costs one comparison and turns an invisible dead end into a sentence.

Chains longer than one hop (A→B→A) are **not** checked: detecting them means walking the graph at
save time, the graph changes as other Automations are edited, and BR-001 stops the runaway anyway.
The cheap check catches the mistake a human actually makes; the expensive one would buy a promise
this change cannot keep.
