# Default automations setup — proposal

## Why

A new project goes from empty to a working pipeline through six hand-filled forms today; #190's
starter catalogue ships the prompts to copy but leaves every Automation to be created by hand.
Issue #212 (design review mock 3d, checklist step 3) makes it one action — the last manual
stretch between a fresh self-host install and a first Run.

## What Changes

- The starter manifest (the #190 catalogue) gains **default Automation wiring** per portable-tier
  prompt: trigger label, `requiresApproval`, output labels. Wiring is catalogue content beside
  the prompts it belongs to — the product hardcodes no methodology.
- A new action, `POST /api/projects/{id}/automations/set-up-defaults` (Admin, UC-005 in bulk):
  creates the wired starter Automations, **skips existing triggers by name** (the BR-003
  comparison — case-insensitive), and reports three lists: created, skipped, and the prompt
  files the created Automations name that the repository does not yet contain.
- The action never writes to the user's repository (#190's design D1 kept) and is idempotent —
  running it twice creates nothing the second time.
- Not breaking: no schema change, no new module, no queue/API contract altered — one new
  endpoint, additive.

## Capabilities

### New Capabilities

- `default-automations`: a project's starter Automations created in one conflict-proof action,
  with the wiring carried by the starter catalogue.

### Modified Capabilities

- `automation-configuration`: none of the existing requirements change; the delta adds the bulk
  creation requirement beside them (BR-003 remains the arbiter of overlap).

## Impact

- **Backend**: Projects module only — `StarterCatalogue` (manifest shape + wiring), one new use
  case, tests. The manifest's own enumeration test extends to the wiring.
- **Frontend**: none here — #211's checklist consumes the endpoint.
- **Traceability**: realises issue #212; UC-005; BR-003, BR-009; actor ACT-001.
