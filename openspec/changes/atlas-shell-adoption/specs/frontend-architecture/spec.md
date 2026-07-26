# frontend-architecture

## ADDED Requirements

### Requirement: every screen renders inside the shared application shell

Every routed screen SHALL render inside the shared shell — sidebar navigation plus top bar —
provided by `shared/ui/`, and SHALL NOT compose its own header or navigation. Navigation between
screens SHALL be sidebar links and breadcrumbs, not per-screen back-links. Feature components
SHALL declare no styles; the shell and its parts come from kit classes, and all copy resolves
through the typed i18n catalogue.

#### Scenario: a new screen is added

- **WHEN** a new route is registered
- **THEN** its screen renders inside the shared shell without declaring layout or navigation of
  its own, and the design validator passes unchanged

#### Scenario: current location is visible

- **WHEN** the user is on any screen
- **THEN** the sidebar marks the active section with the brand-soft treatment and the top bar's
  breadcrumbs name the location

### Requirement: backlog data surfaces show only facts from the live response

The backlog screen's stat cards and table SHALL derive every displayed value from the current
API response. A metric that cannot be computed from the response SHALL NOT be displayed. Absent
values render as `—` (`.empty-value`), and the three absences — no Connector, no Stories, poll
failed — SHALL remain visually and textually distinct in the new layout.

#### Scenario: stat cards

- **WHEN** the backlog renders with a configured Connector
- **THEN** each stat card shows a fact computable from the response (story count, open count,
  trigger-labelled count, connector health) and nothing else

#### Scenario: the three absences survive the redesign

- **WHEN** a project has no Connector, an empty repository, or a failed last poll
- **THEN** each state renders its own distinct copy and treatment, as before the redesign
