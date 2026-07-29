# Proposal: unique-automation-triggers

## Why

Issue #147 (ACT-001 configures; UC-005, UC-006, UC-011). BR-003 — no overlapping triggers — is
enforced by one `OverlapGuard` at create, edit, enable and defaults, and by nothing else. The
`automations` table's only index is a non-unique `IX_automations_ProjectId`, so two concurrent saves
of the same trigger both pass the in-memory check and both insert, producing exactly the two enabled
twins the rule exists to prevent.

The Runs module already learned this: BR-001 is *"a partial unique index, not a handler convention"*,
and its comment records that a hand-written copy of the rule drifted from the index twice. The
Automations catalogue never applied the lesson.

Two more lanes let duplicates in without any race:

- A **disabled** exact duplicate is invisible to the rule — `Overlaps` returns false when either side
  is disabled — so it sits there until somebody trips over it at enable time.
- Labels compare `Ordinal` while GitHub treats label names case-insensitively. `AI:Implement` and
  `ai:implement` coexist here and are one label at the vendor. Worse, matching uses the same `Ordinal`
  comparison, so the wrong-cased Automation silently never fires: no error, no Run, nothing to see.

`Automation.Overlaps` even says *"Labels are the vendor's; compare them the way the vendor does"*
directly above the `Ordinal` comparison. The comment is right and the code does not do it — the same
shape as DEC-050 stating a flush interval the code contradicted.

## What changes

- **The database enforces the rule** (design D1): a unique index over the project, the normalised
  label and the normalised state. Total rather than partial, and it handles the NULL trap that would
  otherwise make it useless.
- **The refusal survives losing the race** (design D2): a unique violation maps to the same
  `TriggerOverlaps` refusal an in-memory conflict produces, never a 500.
- **Exact duplicates are refused whether or not they are enabled** (design D3), while subsumption
  stays enabled-only so BR-003's meaning is unchanged.
- **Labels and states compare the way the vendor compares them** (design D4), in the guard *and* in
  matching, so the two can never disagree about what "the same label" means.
- **Recorded as DEC-056** (design D5), because trigger identity becoming case-insensitive changes what
  BR-003 means.

## Impact

- Specs: `automation-configuration` — one MODIFIED requirement (overlapping triggers rejected when
  saved), carrying its scenarios.
- Docs: BR-003 reworded; DEC-056 recorded.
- Code: `Automation.Overlaps` and a new exact-duplicate rule; `OverlapGuard`; the unique-violation
  mapping; `StoryChangedHandler`'s comparison; one migration with raw SQL for the index.
- No new column: the index normalises in its own expression rather than storing a second copy of the
  label.

## Out of scope

- A `name` field for Automations; they have none.
- Dedup of existing rows beyond what the index requires at migration time — dev holds none today.
- Azure DevOps tag-casing semantics beyond the same comparison; that connector stays the hypothesis
  ADR-0005 recorded.
