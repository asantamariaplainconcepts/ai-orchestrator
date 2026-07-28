# Design: dashboard-tabs

## D1 — Tab state lives in the URL query

`?tab=runs|automations|settings` (Operate is the unmarked default). A query parameter over a
route segment because the tabs are views of one resource, not four resources — breadcrumbs,
data hooks and the route table stay untouched, and an unknown value falls back to the default
instead of a 404.

## D2 — The page migrates whole, and the strip pays its promised second restyle

adopt-foundations locked one-screen-one-system, and this change rebuilds every section the
page renders — so the whole page moves to the Platform theme in one change, exactly the recipe
the projects list established. The #108 strip's kit markup is replaced by shadcn here, the
double-restyle its design D3 explicitly bought. Story/Run/Inbox screens are separate screens
and stay on the kit until their own changes.

## D3 — The landing tab is derived, never stored

Configured → Operate; unconfigured → Settings with the form open. No persisted preference, no
localStorage: the right landing is a fact about the project, and a stored preference would go
stale the moment the connector's state changed. An explicit `?tab=` always wins — a deep link
is the user saying where they want to be.

## D4 — Mobile is the same component, placed differently

One shadcn Tabs instance; below the medium breakpoint the tab list docks to the bottom
(thumb-reachable), and stories/runs collapse from rows to stacked cards with responsive
utilities. No conditional rendering of different trees — one tree, two layouts — so desktop
and mobile cannot drift apart in behaviour (acceptance criterion 4's "every action reachable").
