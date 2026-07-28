# frontend-architecture Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
### Requirement: same-origin single web app

`src/frontend/` SHALL be a standalone pnpm Vite + React + TypeScript + React Router
project served same-origin by `AiOrchestrator.Server`: in dev via the Aspire service
discovery proxy, in prod as the static `pnpm build` output copied to the Server
`wwwroot` with `index.html` fallback. The reserved prefixes `/api`, `/openapi`,
`/scalar`, `/health` SHALL never be swallowed by the SPA fallback. API calls SHALL use
relative paths (no CORS configuration anywhere).

#### Scenario: SPA served by the host

- **WHEN** the production build is deployed and a browser requests `/projects/123`
- **THEN** the host returns `index.html` and the SPA routes client-side

#### Scenario: reserved prefixes win

- **WHEN** a browser requests `/api/health`
- **THEN** the API responds — the SPA fallback never intercepts it

### Requirement: vertical slices mirror the backend

Feature code SHALL live in `src/frontend/features/<feature>/` co-locating screen,
query hooks, typed API calls, local components, and types. `app/` SHALL hold thin
route files only; cross-cutting code SHALL live only under `shared/` (`http/`,
`query/`, `session/`). Generic `services/`, `hooks/`, or `utils/` directories SHALL
NOT exist. TanStack Query SHALL be the only server-state mechanism.

#### Scenario: the exemplar feature

- **WHEN** the Projects list/create screen is implemented
- **THEN** everything it needs sits under `features/projects/` except shared http/query
  plumbing

### Requirement: typed i18n from day 0

All user-facing copy SHALL live in a typed English catalog (DEC-021); JSX SHALL NOT
contain hardcoded user-facing strings, enforced by an ESLint rule failing at
`--max-warnings=0`.

#### Scenario: hardcoded copy fails lint

- **WHEN** a component renders `<Button>Save</Button>` with a literal string
- **THEN** `pnpm lint` fails

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
  labelled count, connector health) and nothing else. "Labelled" means carrying at least one
  label — it graduates to "trigger-labelled" only when Automations exist to define a trigger
  (#14); showing a trigger count before then would be an invented metric

#### Scenario: the three absences survive the redesign

- **WHEN** a project has no Connector, an empty repository, or a failed last poll
- **THEN** each state renders its own distinct copy and treatment, as before the redesign

### Requirement: the project page separates operating from configuring

The project page SHALL present its content as tabs — operate, runs, automations, settings —
where the operate tab carries the daily surface (attention, pulse, backlog with per-row
actions) and configuration forms are visible only on their own tabs or behind an explicit
action. The landing tab SHALL be derived from the project's state: configured projects open on
operate, unconfigured ones on settings with the connector form open. The active tab SHALL be
addressable in the URL and survive a refresh. Below the medium breakpoint the tabs SHALL
remain reachable and every action available on desktop SHALL remain available.

#### Scenario: a configured project opens on the work

- **WHEN** a configured project's page opens without a tab in the URL
- **THEN** the first screenful is the operate tab and no configuration form is visible

#### Scenario: an unconfigured project opens on setup

- **WHEN** a project without a connector opens
- **THEN** the settings tab is active with the connector form open

#### Scenario: a tab survives refresh

- **WHEN** a tab is addressed in the URL and the page reloads
- **THEN** the same tab is active

#### Scenario: small screens keep every action

- **WHEN** the page renders below the medium breakpoint
- **THEN** the tabs remain reachable and every desktop action remains reachable

