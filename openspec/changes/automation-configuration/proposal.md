# Proposal: automation-configuration

## Why

Issue #14. An Automation is the product's central noun — "a Story labelled X makes an Agent do Y"
is the whole pitch — and nothing can express one today. #17 cannot match a story event against
rules that do not exist, so this is the gate on the rest of the dispatch spine.

The corpus had already decided most of it, and the grill's job was to find out how much: BC-001
owns "Automations and their validation (overlap rejection)", DEC-026 fixes the action catalog,
DEC-039 makes approval a per-Automation toggle, BR-005 sets the timeout default. What it did
**not** decide is what BR-003's word *intersects* means — see design D1, the one real call here.

## What changes

- **Automations live in the existing Projects module.** BC-001 owns them; there is no new seam
  here, and a module drawn where the corpus already put the responsibility is not a module drawn
  at a real seam.
- **An Automation is** a trigger (label, plus an optional Story state), an action from the locked
  catalogue, a runtime, `requiresApproval`, and a timeout defaulting to 30 minutes (BR-005).
- **Overlap is rejected at save time** (BR-003, DEC-033) with the precise rule in design D1 — a
  domain error naming the Automation it collides with, not a generic validation failure.
- **The action catalogue ships whole but mostly inert.** All four of DEC-026's actions are
  selectable; only Implement→PR will have an implementation (#19). The UI says so rather than
  letting an Admin configure something that will silently never run.
- **Runtime is Claude Code only.** opencode depends on the still-open OPN-004, and RULE-006
  forbids proposing scope behind an open decision. The field exists as an enum so #30 adds a
  value, not a column.
- **A configuration screen** on the project page, composed from existing kit classes.

## Impact

- `Projects` module: `Automation` aggregate, a migration in the `projects` schema, two use-case
  slices (create, list). No new module, no cross-module reference.
- Frontend: an Automations section on the project page.
- Specs: a new `automation-configuration` capability.

## What this deliberately does not do

Editing and disabling (#15 — same validation, different capability, and BR-003's "existing
enabled" clause only becomes interesting once disabling exists). Matching or Run creation (#17).
Anything an Agent does (#18, #19). Per-project concurrency caps (BR-002) — configured with the
Runs that honour them.
