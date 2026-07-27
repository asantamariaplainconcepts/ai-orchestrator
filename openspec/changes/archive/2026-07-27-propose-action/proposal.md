# Proposal: propose-action

## Why

Issue #80. Between "ready" and "implemented" this repository puts a reviewable proposal, so the
expensive step starts from an agreed shape. The product jumps straight from ready Story to code
PR. The sixth catalogue action closes that gap — and it is what the grill's ready label is for:
`ai:grill` marks ready, ready triggers `ai:propose`, and the chain is two ordinary Automations
(DEC-048 licenses the growth; no new decision needed).

## What Changes

- **`AutomationAction.ProposeSpec`**: reads the ready Story and writes a proposal — why, what
  changes, impact, tasks — as documentation files in the code repository, opened as a PR through
  the same workspace pipeline implement uses. Different prompt, different PR framing, zero new
  publishing machinery.
- **Two refusals before any spend**: a Story with no body fails with "nothing to propose from" —
  the action never invents a requirement; a Story whose linked change already exists fails naming
  it — BR-001's spirit at the artifact level, one open change per Story.
- **Repo conventions win**: the prompt instructs the agent to follow the repository's own spec
  conventions where declared (AGENTS.md/CONTRIBUTING), defaulting to `docs/proposals/<story>/`.
  The product imposes no format on other people's repositories.

## Impact

- Affected specs: `agent-execution` (the action).
- Touched: action enum, executor routing + the two refusals, frontend lists, tests, UC-025 in
  the corpus, ARCHITECTURE.md one paragraph.
- Out of scope: sync/merge as an action; validating the proposal's content — the PR is the
  review; approval-gating by default (it writes only documentation, DEC-040's gate guards code).
