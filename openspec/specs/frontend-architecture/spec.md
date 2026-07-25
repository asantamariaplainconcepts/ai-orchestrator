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

