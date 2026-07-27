# Proposal: automation-defaults

## Why

Issue #76 (ACT-001, UC-005 at scale, enabling UC-008). Configuring a project today means four
manual forms, and inventing three of the four trigger labels while filling them in: only
`ai:implement` is conventional anywhere in this product. Two Admins setting up two projects will
name the other three differently, and neither will match what any document says.

The deeper gap is that a label an Admin has not yet applied **does not exist in the repository**,
so a Member cannot pick it from the vendor's own issue UI. The product can apply labels but has
never been able to create one.

## What Changes

- **A default set, in code.** Four Automations — `ai:implement` → ImplementToPullRequest,
  `ai:refine` → RefineOrComment, `ai:estimate` → Estimate, `ai:transition` → TransitionState —
  on the free opencode runtime (DEC-044), approval required only on the one that writes code
  (DEC-040).
- **One action to apply them**, from the project's Automations section. Idempotent by
  construction rather than by a flag: BR-003 already refuses an overlapping trigger, so a second
  press creates nothing and the response says what already existed.
- **`EnsureLabel` on the Connector seam**, so the labels exist in the backlog to be chosen.
  GitHub creates the label; Azure DevOps cannot and must not pretend to — tags there come into
  existence when first applied to a work item, so its implementation is an explicit no-op.

## Impact

- Affected specs: `automation-configuration` (the default set and its partial-success
  semantics), `connector-seam` (one write).
- Touched: Projects module (the use case), Backlog module (seam + both vendors + the Contracts
  surface the Projects module reaches it through), frontend, tests, ARCHITECTURE.md.
- Out of scope: configuring the Connector (UC-004 owns that); editing the default set from the
  portal — the set is code, and changing it is a commit; reconciling or removing defaults later,
  because this creates and does not manage; environment gating, since nothing here is
  destructive and a flag is one more thing to misconfigure.
