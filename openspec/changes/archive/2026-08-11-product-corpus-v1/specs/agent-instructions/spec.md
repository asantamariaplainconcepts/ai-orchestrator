# agent-instructions Specification (delta)

## MODIFIED Requirements

### Requirement: one canonical instruction file

`AGENTS.md` at the repo root SHALL be the only place agent-facing project rules are written. It
SHALL be a router — what the project is, a lookup table of where things live, the workflow loop
with its gates, and short house rules — and SHALL NOT restate specs, product facts, or design
decisions that have a home elsewhere; it links to them instead.

#### Scenario: a fact has exactly one home

- **WHEN** `AGENTS.md` needs to state a product rule, a spec'd behaviour, or a decision
- **THEN** it links to `docs/product/v1/` (product truth), `openspec/specs/` (behaviour),
  `docs/adr/` (decisions), or the decision log at `docs/product/mvp/10-locked-mvp-decisions.md`,
  rather than copying the content
