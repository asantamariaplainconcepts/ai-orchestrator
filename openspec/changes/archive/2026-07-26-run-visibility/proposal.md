# Proposal: run-visibility

## Why

Issue #20 (UC-021). #17 closed the loop — a labelled Story becomes a Run and a queue message —
but the product cannot show it: the Run exists only as a Postgres row. DEC-031 locks what run
visibility means (status + output link + fetched logs + cost, per project and per story);
BR-014 says what a Run records. This slice surfaces the subset of BR-014 that exists today and
renders the design system's empty value for what does not, rather than waiting for every
producer to land first.

## What Changes

- **Runs module gains its first read slice**: `GET /api/projects/{projectId}/runs` — the
  project's Runs newest-first, optional `vendorStoryId` filter, read-only, exactly the fields
  the Run records today (id, vendor story id, automation id, state, createdAt, dispatchedAt).
- **Portal: Runs section on the project page** (design-system components, i18n catalog):
  per-project list, per-Story filter reachable from a backlog row, automation columns joined
  client-side from the existing automations endpoint, em-dash empty values for output/logs/cost,
  empty state when no Runs exist.
- No schema change, no new Contracts surface, no mutation of any kind.

## Impact

- Affected specs: `run-orchestration` (adds the observation requirement).
- Touched: `AiOrchestrator.Modules.Runs` (one read endpoint + slice), frontend
  (`features/projects` Runs section + catalog entries), Runs functional tests (endpoint tier).
- Out of scope: streaming, cost (#25), output links (#19), Run now (#21), cancel (#23).
