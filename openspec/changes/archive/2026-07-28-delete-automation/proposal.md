# Proposal: delete-automation

## Why

Issue #84. Create, edit and disable exist; removal does not. One click now seeds four
Automations (#76), so every project accumulates configuration nobody asked for, and disabling
leaves it in the list it was meant to leave.

## What Changes

- **`DELETE /api/projects/{projectId}/automations/{automationId}`** and a control beside the
  enable toggle.
- **Refused when any Run references it**, naming how many and pointing at disabling. Two rules
  make this the only defensible policy: BR-014 (Runs record their Automation and are never
  deleted, so removal decays the audit trail backwards) and #14's finding (the executor resolves
  the Automation *mid-Run*; a missing row kills the Run claiming it "is no longer enabled" —
  precisely the bug removing the `Enabled` filter fixed, in a form nobody can undo).
- **The Runs module publishes its first contract**: a read surface answering how many Runs
  reference an Automation. It has been a leaf until now — consuming Projects' and Backlog's
  contracts, depended on by nobody.

## Impact

- Affected specs: `automation-configuration` (removal and its refusal).
- Touched: new `AiOrchestrator.Modules.Runs.Contracts`, Projects module (the use case and one
  reference), frontend, tests, UC-006's text.
- Out of scope: deleting Runs (BR-014); archiving as a third state — rejected, it adds a flag
  beside `Enabled` and a vocabulary question to solve what the refusal already covers; bulk
  delete; undo.
