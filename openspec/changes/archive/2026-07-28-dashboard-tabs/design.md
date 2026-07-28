# Design: dashboard-tabs

## D1 — Tab state lives in the URL query

`?tab=operate|runs|automations|settings`. A query parameter over a route segment because the
tabs are views of one resource, not four resources — breadcrumbs, data hooks and the route table
stay untouched, and an unknown value falls back to the derived landing instead of a 404.

**No tab is the unmarked default.** Leaving operate unmarked was tried and is a trap: on an
unconfigured project, clearing the parameter handed control back to the derived landing (D3),
which sent the user straight back to settings — operate was unreachable. The absence of the
parameter must mean exactly one thing, "the user has not chosen yet", so every choice writes it.
The E2E reachability suite caught this on its first run, which is the failure mode ADR-0006
exists for.

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

## D4 — Native `<select>`, not the Radix one

shadcn's Select renders a listbox of divs; the E2E reachability assertions read `option`
elements out of `#vendor` and `#runtime` (ADR-0006, acceptance criterion 5). Swapping in Radix
would have quietly broken the tests whose entire job is to catch this change relocating a
control — so the selects stay native, styled with the theme's tokens through one shared
`NativeSelect`. A native picker is also the better mobile control: the OS wheel beats a
custom popover on a phone.

## D5 — Lists, not tables

Every migrated collection (stories, runs, automations) renders as a list of rows that reflow
into stacked cards below the medium breakpoint. A `<table>` cannot reflow without either
horizontal scrolling or a duplicate mobile tree, and a duplicate tree is how a control ends up
existing at one width only — the failure acceptance criterion 4 forbids. shadcn's Table
primitive was generated and then deleted unused: no component enters this repository ahead of a
screen that needs it.

## D6 — Mobile is the same component, placed differently

One shadcn Tabs instance; below the medium breakpoint the tab list docks to the bottom
(thumb-reachable), and stories/runs collapse from rows to stacked cards with responsive
utilities. No conditional rendering of different trees — one tree, two layouts — so desktop
and mobile cannot drift apart in behaviour (acceptance criterion 4's "every action reachable").
