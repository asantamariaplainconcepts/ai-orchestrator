# Proposal: automation-editing

## Why

Issue #15 (UC-006). `Enabled` has been on the model since #14 with nothing able to toggle it,
and a misconfigured Automation currently needs a database edit. The interesting half is not the
CRUD: an **edit is another way to create a BR-003 overlap**, so it must face the same gate a
create does — while not refusing an Automation for colliding with its own former self.

## What Changes

- **`PUT /api/projects/{projectId}/automations/{automationId}`** — trigger, action, runtime,
  approval flag and timeout, validated exactly as a create is, excluding the subject from its
  own overlap comparison.
- **`POST .../automations/{automationId}/enable` and `/disable`** — enabling re-runs the overlap
  check, because a trigger that was safe while disabled may collide with one added since;
  disabling never does, since a disabled Automation is invisible to BR-003.
- **In-flight work is untouched by construction**: a Run records the Automation id it was
  created with and reads details at execution; editing changes what *future* matches do.
- **Portal**: edit and enable/disable on the Automations table, with refusals naming the rule.

## Impact

- Affected specs: `automation-configuration`.
- Touched: Projects module (the overlap rule's call site, three slices), frontend
  (Automations section + catalog), Projects functional tests.
- Out of scope: deletion (Runs reference Automations — an audit question of its own), edit
  history.
