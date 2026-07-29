# Proposal: reap-from-the-phase

## Why

Issue #146, a defect in #140 that is already on `main`. The reaper measures a Run's deadline from
`Run.StartedAt`, and `MarkPlanning` sets that field — so for an approval-gated Run it is the moment
the *plan* began, before the human wait.

BR-006 says human waits are untimed. So a Run that planned on Monday, waited for its approval, and
began executing on Tuesday is already far past `StartedAt + timeout + grace` the instant it enters
`Executing`, and the next sweep fails it blaming a worker that never went missing. The reaper was
built to serve BR-005, which gives **each phase** a timeout; measuring the whole Run timed precisely
what BR-006 forbids timing.

Nothing has hit it yet only because no plan has been approved since #140 landed.

## What changes

- **The deadline is measured from the current phase's start** (design D1): `ApprovedAt` for a Run
  executing after approval, `StartedAt` otherwise. No new column — `ApprovedAt` already records when
  the wait ended.
- **The requirement's wording is corrected** (design D2): "the start of its current phase", not "its
  start". The sentence read correctly and was wrong, which is why the code that implemented it
  faithfully was wrong too.

## Impact

- Specs: `run-orchestration` — one MODIFIED requirement, one word that matters and one added
  scenario.
- Code: one expression in `RunReaping`, plus two tests.
- No schema change.

## Out of scope

- The replica-timeout contradiction and the rest of #144, which is the *cause* behind the symptom
  #140 addressed. Related, not overlapping.
- `AwaitingInput`, which the sweep already does not consider.
