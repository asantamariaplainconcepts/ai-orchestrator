# frontend-architecture

## MODIFIED Requirements

### Requirement: every screen renders inside the shared application shell

Every routed screen SHALL render inside the shared shell — sidebar navigation plus top bar —
provided by `shared/ui/`, and SHALL NOT compose its own header or navigation. Navigation between
screens SHALL be sidebar links and breadcrumbs, not per-screen back-links. Feature components
SHALL declare no styles; the shell and its parts come from kit classes, and all copy resolves
through the typed i18n catalogue.

From the medium breakpoint up, the sidebar SHALL be collapsible to an icon rail, and the choice SHALL
survive a reload. Collapsed SHALL mean a narrower rail, never a hidden panel: every navigation
destination SHALL remain reachable in one click and the inbox count SHALL remain visible without
expanding, for the same reason the shell keeps both reachable when it folds at phone width. Below that
breakpoint the control SHALL be absent, because the folded sheet already is the collapsed state.

A collapsed entry SHALL carry its name for assistive technology and on hover, since the label is no
longer rendered.

The widths SHALL be defined as design-system variables and consumed through the token adapter, so the
adapter's bound names resolve to real values.

#### Scenario: a new screen is added

- **WHEN** a new route is registered
- **THEN** its screen renders inside the shared shell without declaring layout or navigation of
  its own, and the design validator passes unchanged

#### Scenario: current location is visible

- **WHEN** the user is on any screen
- **THEN** the sidebar marks the active section with the brand-soft treatment and the top bar's
  breadcrumbs name the location

#### Scenario: the work gets the width

- **WHEN** a Member collapses the sidebar at or above the medium breakpoint
- **THEN** the content area gains that width and every navigation destination is still one click away

#### Scenario: the count survives the collapse

- **WHEN** the sidebar is collapsed and items are waiting in the inbox
- **THEN** the count is still visible without expanding

#### Scenario: the choice is remembered

- **WHEN** the page is reloaded
- **THEN** the sidebar is in the state the Member last chose, collapsed or expanded

#### Scenario: the phone is unaffected

- **WHEN** the viewport is below the medium breakpoint
- **THEN** no collapse control is offered and the folded bar behaves as it did before

#### Scenario: an icon still has a name

- **WHEN** a navigation entry is shown collapsed
- **THEN** its name is available to a screen reader and on hover
