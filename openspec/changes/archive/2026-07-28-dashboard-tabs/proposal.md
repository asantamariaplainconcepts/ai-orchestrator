# Proposal: dashboard-tabs

## Why

Issue #109 (ACT-002 daily, ACT-001 occasionally). The project page is one long scroll where
configuration dominates operation: two permanently-expanded forms (automation, connector) sit
above the backlog a Member actually works with. A form that is always open tells the user the
page is for configuring — false on every day but the first. RULE-005 bounds this change hard:
it moves furniture, it may not add rooms.

## What changes

- **Four tabs on shadcn Tabs**: Operate (default — attention + pulse strip + backlog with
  per-row actions; the connector present only as its health pill), Runs (the full list as
  today), Automations (the list; creation behind a "New automation" button), Settings (the
  connector, collapsed to one line when configured, the full form only when absent or editing).
- **The landing tab is a fact, not a preference**: a configured project opens on Operate; an
  unconfigured one on Settings with the form open — the one day configuration IS the job.
- **Deep links**: `?tab=` in the URL; refresh preserves the tab.
- **Mobile**: the same tabs fixed to the bottom below the medium breakpoint; stories and runs
  render as stacked cards through responsive utilities — no bespoke kit class.
- **The page migrates off the kit** (adopt-foundations D2: one screen, one system — and this
  change rebuilds every section the page renders, the #108 strip included). Stat cards retire
  in favour of the strip; cost and health move to the header line.

## Impact

- Specs: `frontend-architecture` (one ADDED requirement).
- Code: frontend only — no endpoint, no rule, no schema changes.
- E2E: the reachability assertions (defaults button, runtime picker, vendor picker) must keep
  passing with relocated controls — ADR-0006 watches exactly this change.
