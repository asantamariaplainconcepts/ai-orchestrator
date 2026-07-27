# Proposal: grill-action

## Why

Issue #79. This repository starts every item at `/aio:grill` — an interrogation until the
Definition of Ready is met, so nothing is proposed on a guess. The product has no equivalent: a
Story goes from vague to implemented carrying whatever gaps it had. #78 built the machinery a
conversation needs; this is its first consumer, and the fifth catalogue action (revising DEC-026,
recorded as a locked decision at sync).

## What Changes

- **`AutomationAction.GrillToReady`**: each pass evaluates the re-fetched Story plus the
  conversation so far against the project's own readiness bar, and either asks — the specific
  unmet criteria, via #78's wait — or declares the bar met: a configurable ready label plus a
  verdict comment.
- **The rubric is the project's**, read live from the connected repository (default
  `docs/process/definition-of-ready.md`, path configurable per Automation). No document, no
  grill: the Run fails naming the path, before any comment or label is written.
- **Two nullable settings on Automation** — rubric path and ready label — with defaults in code,
  shown in the portal form only for this action.
- **Contracts grow what the executor lacks**: `ApplyLabel` on the write surface and a
  default-branch document read. Both are thin delegations to seam methods that already exist.

## Impact

- Affected specs: `agent-execution` (the action), `automation-configuration` (the settings).
- Touched: Projects module (action enum, settings, migration, detail record), Backlog contracts
  + implementations, executor, frontend form, tests, ARCHITECTURE.md (the ADR-0006 notice on
  #78's machinery comes off), DEC-026 revision.
- Out of scope: propose/sync actions (#80 next); adding `ai:grill` to the defaults button;
  editing the Story on the human's behalf — the Agent asks, the human writes.
