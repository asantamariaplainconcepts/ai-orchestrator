# Proposal: archive-project

## Why

Issue #121 (ACT-001, Admin). A project that is over has no way to leave. It stays in the list,
its Connector keeps polling the vendor, and a label applied by somebody tidying an old repository
can still start a Run and spend money. The only exit today is deleting rows by hand.

Archiving rather than deleting, because BR-014 makes Runs immortal on purpose: the record of what
agents did to a repository *is* the audit trail, and a product that discards it to tidy a list
has traded the wrong thing.

## What changes

- **A project can be archived and restored**, from its Settings tab, the archive confirmed by
  typing the project's name.
- **Archiving stops the work, not just the listing** — the operative half. An archived project is
  not polled, does not match labels into Runs, and refuses a manual Run. A hidden project that
  keeps spending is worse than a visible one.
- **Runs in flight are left alone.** Archiving is not cancellation; a Run already executing
  finishes and records itself. UC-014 exists for the other intent.
- **Reading stays open, starting does not** (design D2): the project's Runs, logs and pulse
  remain reachable at their URLs. Archiving retires a project, it does not seal it.
- **The list hides archived projects behind a filter that states how many there are**, rather
  than pretending they never existed.
- **Other modules learn it through Contracts** (design D1): Backlog's poller and Runs' matching
  both need the state, and neither may keep a copy of it.

## Impact

- Specs: `project-management` (one ADDED requirement) and `backlog-mirror` (one MODIFIED, since
  polling gains a condition).
- Schema: one nullable timestamp on the project — when it was archived, which is also the fact
  the list shows.
- Code: Projects owns the state and publishes it on its Contracts surface; Backlog's poller and
  Runs' matching and manual dispatch read it.

## Out of scope

Deleting a project outright; bulk archiving; any retention or clean-up policy for archived data.
