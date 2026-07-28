# Proposal: connector-health

## Why

Issue #97. A dead PAT announces itself only after navigating into a project — or worse, as a
backlog that quietly stops moving, because BR-008's mirror keeps serving stale data by design.
That design choice makes the staleness *signal* more important, not less. The data already
exists on the Connector; what is missing is the ambient view from the projects list.

## What Changes

- **`GET /api/connectors`** in the Backlog module: one row per configured Connector — project,
  vendor, last sync, last failure. The projects list joins client-side, the same pattern the
  runs list uses for automation details.
- **Four states on the projects list, not a boolean**: healthy / failing / never-synced /
  not-configured, with the failure sentence reachable without navigating and the sync age
  visible on healthy ones — stale-but-not-failing is a state a Member can notice.

## Impact

- Affected specs: `connector-configuration` (the ambient read).
- Touched: Backlog module (one read slice), projects screen, tests.
- Out of scope: any new probe (the poller stays the only prober); alerting (DEC-037); history.
