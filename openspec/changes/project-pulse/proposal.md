# Proposal: project-pulse

## Why

Issue #108 (ACT-002, Member). Everything a project overview needs is already persisted — BR-014
forces every Run to carry its timestamps, state, automation and cost — and none of it is
aggregated anywhere. An Admin cannot answer "is this project healthy this week?" without reading
run lists by hand. One read slice unlocks it all: no schema change, no new collection, the same
derived-never-stored shape the inbox (#94) proved.

## What changes

- **`GET /api/projects/{id}/pulse`** (Runs module, Observation): over a 7-day window — runs
  started, success rate over terminal runs, total cost with the unknown-usage count stated
  (BR-011), mean queue wait (DispatchedAt→StartedAt), mean duration (StartedAt→EndedAt),
  per-automation fire/failure counts including zero-run automations (absence is the signal),
  stories never run, the project-scoped waiting summary, and the oldest unanswered question age
  (BR-006 made visible).
- **The Operate strip** on the project page: attention row (waiting + executing-now, linking to
  the run pages), metric cards, and the automation mini-table with an explicit unused row state
  (actionable since #84's delete). Every number links to the list it summarises — a metric that
  cannot be audited is decoration.
- The strip keeps the project page's current styling system (see design D3); its move to the
  Platform theme lands with `dashboard-tabs` (#109), which migrates the whole page.

## Impact

- Specs: `run-orchestration` (one ADDED requirement).
- Code: Runs module read slice + endpoint; project page strip; mock routes; i18n keys.
- No schema change, no new module boundary — cross-module reads stay behind the existing
  Contracts surfaces.
