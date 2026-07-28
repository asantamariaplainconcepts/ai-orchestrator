# Proposal: automation-output-label

## Why

Issue #115 (Foundation; ACT-001 configures, ACT-002 sees the effect). Exactly one action can
hand work to the next: `GrillToReady` writes its `ReadyLabel`, and that single field is the only
reason the grill→propose chain exists. Every other action ends silently, so an Admin who wants
one Automation to feed another labels the Story by hand in between.

Generalising that field turns chaining from a property of the grill into a property of **any**
Automation — and gives `workflow-canvas` (#116) somewhere to store a reconnection, which is why
it is blocked behind this.

## What changes

- **`Automation.OutputLabel`** (nullable, default null): the label applied to the Story when a
  Run of that Automation succeeds. Null means what five of the six actions do today — end
  silently.
- **`ReadyLabel` becomes that field** (design D1). Two fields meaning "the label I write when I
  finish" would drift; the grill's documented default survives as the default *value* for that
  action, in code, exactly as today.
- Written through the same licensed write UC-008 already offers, from one place in the executor
  (design D2), **only on success** — a failed or cancelled Run hands nothing on.
- **Self-triggering is refused at save** (design D3): an Automation whose output label is its own
  trigger would re-fire itself, and BR-001 would refuse the second Run, leaving a labelled Story
  and no work.
- One optional field in the Automations form.

## Impact

- Specs: `automation-configuration` (one ADDED requirement, one MODIFIED).
- Schema: the `ReadyLabel` column is renamed, not dropped — configured grills keep working
  unchanged (acceptance criterion 4).
- Code: Projects domain + contract + form; Runs executor writes it for every action instead of
  only the grill.

## Out of scope

Conditional edges, more than one output label per Automation, and any canvas UI (#116).
