# Proposal: defaults-full-catalogue

## Why

Issue #86. The one-click set-up seeds four Automations; the catalogue has six. The two missing
ones (#79 grill, #80 propose) are precisely the ones that make this a workflow rather than a
menu, and seeding them naively would leave them unconnected — the grill's ready label is
`ready-for-proposal` while propose's own documented trigger is `ai:propose`.

## What Changes

- **Two entries in the default set**: `ai:grill` → GrillToReady, and `ready-for-proposal` →
  ProposeSpec. Neither requires approval; `ai:implement` stays the only default that waits for a
  human, because DEC-040's gate guards code and these write comments, labels and documentation.
- **The chain works on the first press.** Seeding propose to listen on the grill's ready label
  is what connects them, and it keeps a single truth about what that label is rather than
  overriding it inside the seeded Automation.
- **The button becomes an upgrade path.** Nothing new is needed for that: BR-003 refuses the
  overlaps, so a project on the old set gets exactly the two additions and hears that the rest
  were already handled.

## Impact

- Affected specs: `automation-configuration` (the set is the full catalogue, and it chains).
- Touched: the default set (a table), its tests, ARCHITECTURE.md one sentence.
- Out of scope: either action's own defaults — only what the button seeds; chaining propose into
  implement, because the proposal PR is a human's decision point by design.
