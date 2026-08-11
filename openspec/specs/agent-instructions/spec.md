# agent-instructions Specification

## Purpose
TBD - created by archiving change ai-delivery-layer. Update Purpose after archive.
## Requirements
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

### Requirement: every runtime pointer resolves to AGENTS.md

Each runtime in DEC-018 SHALL have a pointer file that directs the agent to `AGENTS.md`:
`CLAUDE.md` (Claude Code), `.github/copilot-instructions.md` (GitHub Copilot), and `opencode.json`
(opencode). A pointer that names any other document SHALL be treated as a defect.

#### Scenario: pointer audit

- **WHEN** the pointer files are inspected
- **THEN** each one names `AGENTS.md` as the instruction source, and none names
  `CONTRIBUTING.md`, `README.md`, or a copy of the rules

#### Scenario: adding a runtime

- **WHEN** a fourth runtime is adopted
- **THEN** it gets a pointer file and `AGENTS.md` is unchanged

### Requirement: instructions are runtime-neutral

`AGENTS.md`, skills, and commands SHALL be written as natural-language instructions and SHALL NOT
name runtime-specific tools or UI affordances, so the same text is executable by any of the three
runtimes.

#### Scenario: no runtime-specific vocabulary

- **WHEN** a skill or command instructs the agent to ask the user something or invoke another
  capability
- **THEN** it describes the action in plain language rather than naming a specific tool

