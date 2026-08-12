## MODIFIED Requirements

### Requirement: every screen renders inside the shared application shell

Every routed screen SHALL render inside the shared shell — sidebar navigation plus top bar —
provided by `shared/ui/`, and SHALL NOT compose its own header or navigation. Navigation between
screens SHALL be sidebar entries and breadcrumbs, not per-screen back-links. Feature components
SHALL declare no styles; the shell and its parts come from kit classes, and all copy resolves
through the typed i18n catalogue.

The sidebar's structure SHALL be a **tree**: every project the caller may see, with its live work
nested beneath it (`shell-projects-tree`). A flat list of top-level links SHALL NOT be the
sidebar's only structure, because "what is running right now" must not cost a navigation per
project.

From the medium breakpoint up, the sidebar SHALL be collapsible to an icon rail, and the choice SHALL
survive a reload. Collapsed SHALL mean a narrower rail, never a hidden panel: every navigation
destination SHALL remain reachable in one click and the inbox count SHALL remain visible without
expanding, for the same reason the shell keeps both reachable when it folds at phone width. Below that
breakpoint the control SHALL be absent, because the folded sheet already is the collapsed state.

A collapsed entry SHALL carry its name for assistive technology and on hover, since the label is no
longer rendered. This SHALL hold for **nested** entries as well as top-level ones: where the rail
cannot show a tree's children inline at `--sidebar-w-collapsed`, opening the parent SHALL reveal the
same children with the same destinations rather than dropping them.

Both widths SHALL come from the canonical design-system variables rather than from a value written into
the shell, so that the rendered sidebar cannot disagree with the token that describes it.

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

#### Scenario: the width is the token's

- **WHEN** the shell renders its sidebar, expanded or collapsed
- **THEN** the width comes from the canonical variable rather than from a literal in the shell

#### Scenario: an icon still has a name

- **WHEN** a navigation entry is shown collapsed
- **THEN** its name is available to a screen reader and on hover

#### Scenario: the sidebar names the work, not just the sections

- **WHEN** the sidebar renders for a caller with projects
- **THEN** each visible project is an entry with its live work nested beneath it, and no
  navigation into a project is required to see that the project has work in flight

#### Scenario: a nested entry survives the collapse

- **WHEN** the sidebar is collapsed and a project with nested children is opened from the rail
- **THEN** the same children with the same destinations are reachable, none dropped for want of
  width
