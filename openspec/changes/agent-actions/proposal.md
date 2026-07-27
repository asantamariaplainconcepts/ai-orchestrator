# Proposal: agent-actions

## Why

Issues #26 (UC-017), #27 (UC-018) and #28 (UC-019). DEC-026 shipped the action catalogue whole
so an Admin could configure all four, and #19 made exactly one of them executable — the other
three fail with "not executable yet", which is honest but is the last place the product tells a
user it isn't finished.

Proposed as one change because they are one mechanism: the executor's action dispatch, plus a
single Connector write each. Three bundles would be three copies of the same design.

## What Changes

- **The executor dispatches on the action** instead of gating to one: each action builds its own
  instruction and consumes the Agent's answer its own way.
- **`RefineOrComment`** — the Agent's answer is posted as a Story comment (UC-017).
- **`TransitionState`** — the Agent's answer names a target state, written through the seam
  (UC-018). The Automation's configured target is not yet a field, so the Agent proposes and
  the connector validates; an unknown state is a stated failure, never a guess.
- **`Estimate`** — an `estimate:<n>` label plus the reasoning as a comment (UC-019, owner
  decision). Any prior `estimate:*` label is removed first, so a Story carries exactly one.
- **Seam writes**: `AddComment` and `SetState`, alongside the existing label writes.

## Impact

- Affected specs: `connector-seam` (two writes), `agent-execution` (dispatch by action).
- Touched: Backlog module (seam + GitHub impl + Contracts write surface for the Runs module),
  Runs module (dispatch), tests, ARCHITECTURE.md.
- Out of scope: a configured transition target on the Automation (the Agent proposes for now);
  estimate scales or units beyond a number; AzDO mappings (OPN-003, #29).
