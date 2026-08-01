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

From the medium breakpoint up, the sidebar SHALL be collapsible to an icon rail, and the choice SHALL
survive a reload. Collapsed SHALL mean a narrower rail, never a hidden panel: every navigation
destination SHALL remain reachable in one click and the inbox count SHALL remain visible without
expanding, for the same reason the shell keeps both reachable when it folds at phone width. Below that
breakpoint the control SHALL be absent, because the folded sheet already is the collapsed state.

A collapsed entry SHALL carry its name for assistive technology and on hover, since the label is no
longer rendered.

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

### Requirement: the code-source surface renders only where the API offers it

The Connector settings SHALL offer a Repository / Local folder choice as a segmented control,
rendered only after a single per-page probe of the code-source surface succeeds; where the API
answers 404 (cloud posture), no code-source UI SHALL exist at all — no disabled control, no
explanatory stub. The path input SHALL be monospaced, validated live against the host with the
specific failing check named, and SHALL render loading, empty, error and success states. Recent
folders SHALL be offered as targets at least 44px tall, each naming the project that used it, and
selecting one SHALL re-run the same live validation typing does. A warning callout SHALL state the
pod constraint (a LocalFolder project cannot run in a pod).

#### Scenario: a cloud deployment shows nothing

- **WHEN** the Settings tab renders on a deployment whose code-source probe answers 404
- **THEN** no code-source UI exists at all

#### Scenario: an invalid path names its failing check

- **WHEN** live validation returns for an invalid path
- **THEN** the specific failing check is named and nothing is saved

#### Scenario: a recent folder is not trusted stale

- **WHEN** a recent folder is selected
- **THEN** the live validation runs exactly as if the path were typed

### Requirement: Run now states the locus choice where a choice exists

Run now SHALL dispatch exactly as today, with no dialog, on a project with no local folder. On a
LocalFolder project it SHALL open a dialog of radio cards (targets at least 48px) stating each
locus's consequences; the pod card SHALL be disabled carrying its reason; the primary button SHALL
repeat the chosen locus ("Run on this machine" / "Run in a pod"). Refusals — BR-001's conflict,
BR-013's rules, and the clean-tree refusal recorded by #210 — SHALL render inside the dialog,
announced aria-live polite, naming the folder where the folder is the reason.

#### Scenario: no choice, no dialog

- **WHEN** Run now is pressed on a project with no local folder
- **THEN** it dispatches exactly as today with no dialog

#### Scenario: the dialog states the constraint

- **WHEN** Run now opens on a LocalFolder project
- **THEN** the pod card is disabled with its reason and the primary button names the chosen locus

#### Scenario: a dirty tree refuses inside the dialog

- **WHEN** a local dispatch is attempted against a dirty working tree
- **THEN** the refusal renders in the dialog before any write, naming the folder

### Requirement: every Run says where it executed

The Run detail SHALL show a locus chip beside the state pill and an Execution block in the rail:
runtime and kind, the working folder for local Runs, the branch created, and the output as a local
branch name or a PR link by locus. The Changes card SHALL name the created branch for local Runs
rather than implying a readable working tree. The projects list SHALL mark LocalFolder projects
with a quiet outline "Local" badge — monitor glyph plus the word, the same chip vocabulary as the
Run locus chip. Locus SHALL never be conveyed by colour alone.

#### Scenario: a local Run names its folder and branch

- **WHEN** a local Run's detail renders
- **THEN** the locus chip reads Local and the Execution block shows the working folder and the
  created branch

#### Scenario: a pod Run links its output

- **WHEN** a pod Run's detail renders
- **THEN** the locus chip reads Pod and the output is the PR link as today

### Requirement: the local owner is guided from empty to a closed loop

Whenever the current principal is the `local-owner` sentinel, a persistent banner
(`role="status"`, warning family) SHALL state it on every screen; a signed-in principal SHALL
remove it. The Operate tab SHALL show a three-step "close the loop" checklist — Connector
configured, Automations exist, a Run reached a terminal state — derived live from the connector,
automations and runs read models with no stored progress, and SHALL never render again once any
Run in the project has reached a terminal state. The checklist's third step SHALL offer the
set-up-defaults action (#212) once that action exists; until then it SHALL guide without it.

#### Scenario: the banner keys on the principal

- **WHEN** the principal is the local-owner sentinel
- **THEN** the banner is present on every screen; with a signed-in principal it is absent

#### Scenario: a closed loop retires the checklist permanently

- **WHEN** any Run in the project reaches a terminal state
- **THEN** the checklist never renders again, with nothing stored to make it so

#### Scenario: progress is derived, not remembered

- **WHEN** the checklist renders
- **THEN** each step's state comes from the live connector, automations and runs data only
