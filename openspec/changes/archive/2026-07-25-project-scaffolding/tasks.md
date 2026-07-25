# Tasks — project-scaffolding

Ordered per doc-02 day-0 sequence; each task is small, verifiable, one-session.

## 1. Repo & build skeleton

- [x] 1.1 `src/` layout: `AiOrchestrator.slnx`, `global.json` (SDK 10.0.100,
      rollForward latestFeature), `Directory.Build.props` (TFM, Nullable,
      TreatWarningsAsErrors, EnforceCodeStyleInBuild, analyzer auto-attach to
      `*.Modules.*`, HuskyInstall target), `Directory.Packages.props` (CPM, transitive
      pinning, Roslynator as GlobalPackageReference), `.editorconfig` from kit refs.
- [x] 1.2 Verify: `dotnet build` succeeds; style violations fail the build (observed
      repeatedly during implementation — IDE0040/IDE0290/IDE0305 broke the build until fixed).

## 2. Kernel + reference module + host

- [x] 2.1 `AiOrchestrator.BuildingBlocks`: IModule/ModuleBase + runtime discovery, CQS
      (ICommand/IQuery, IAppCommandHandler/IAppQueryHandler, fixed decorator pipeline via
      `AddVsaCqsArchitecture()`), ErrorOr + ApiResults.Problem + GlobalExceptionHandler,
      Domain primitives (BaseEntity/Aggregate, GUID v7).
- [x] 2.2 `AiOrchestrator.ServiceDefaults` (OTel logs/metrics/traces, OTLP locally +
      Azure Monitor in cloud, health endpoints excluded from traces).
      *`Constants` was not created — it would have been an empty project.*
- [x] 2.3 `AiOrchestrator.Modules.Projects`: module class, `projects` schema DbContext +
      `InitialProjects` migration, exemplar command slice `CreateProject` (UC-003) and
      query slice `ListProjects` (UC-007) — the query side needed an exemplar too.
- [x] 2.4 `AiOrchestrator.Server` (module discovery, minimal API edge, health endpoints,
      SPA same-origin) + `AiOrchestrator.AppHost` (Postgres, Azurite, Server, Vite).
- [x] 2.5 Verify: `POST`/`GET /api/projects` round-trip in functional tests against real
      containers, and the whole composition boots under the E2E lane in CI. **`aspire run`
      itself still not executed by anyone** — see close-out note.

## 3. Guardrails (before any second module)

- [x] 3.1 `AiOrchestrator.ArchitectureAnalyzers`: MOD001–005 + CQS001 lifted from kit refs
      and renamed; auto-attached via Build.props.
- [x] 3.2 `AiOrchestrator.ArchTests`: module-boundary, no-controllers, interface naming,
      unique LoggerMessage event IDs, `Subject_Should_Constraint` regex.
- [x] 3.3 Verify, by probe (each reverted afterwards):
      - public domain entity → `error MOD003` + `MOD005`, build fails
      - public CQS handler → `error CQS001`, build fails
      - other module's type in a member signature → `error MOD002`, build fails
      - other module's type used only in a method body → **MOD002 does not fire** (by
        design: it inspects signatures) → caught by the ArchTest assembly-reference check
      - test named `TestThatNamingIsEnforced` → ArchTests fail

## 4. Test bases

- [x] 4.1 `AiOrchestrator.SharedFunctionalTests`: `ApiServiceFixtureBase`
      (WebApplicationFactory + Testcontainers Postgres/Azurite + Respawn), one stack per
      module via `ICollectionFixture`.
- [x] 4.2 Projects `UnitTests` (validator + aggregate, 5 tests) and `FunctionalTests`
      (create/list/duplicate/validation + SPA fallback and reserved prefixes, 7 tests).
- [x] 4.3 `AiOrchestrator.EndToEndTests`: DistributedApplicationTestingBuilder + Playwright,
      Session container lifetimes, host-log capture, smoke journey. The host is forced to
      `ASPNETCORE_ENVIRONMENT=E2E`, which also puts the journey on the production serving path
      (static `wwwroot` + fallback) rather than the dev proxy — so E2E proves what ships.
      **Green on CI**, after finding two real defects on its first run (below).

## 5. Frontend skeleton

- [x] 5.1 Vite + React + TypeScript + React Router, pnpm standalone; `features/`, thin
      `app/` routes, `shared/{http,query,i18n}`; TanStack Query; typed English i18n catalog;
      Projects list/create screen against the exemplar API.
- [x] 5.2 Same-origin wiring: dev proxy via Aspire service discovery (SpaServices);
      `pnpm build` output to Server `wwwroot` with index.html fallback + reserved prefixes.
- [x] 5.3 ESLint flat config `--max-warnings=0`, Prettier, i18n hardcoded-copy rule.
- [x] 5.4 Verify: `pnpm build` produces `wwwroot`; the host serves the shell and falls back
      for client routes (functional tests); a hardcoded JSX string **and** a hardcoded
      `title`/`aria-label` both fail `pnpm lint` (probed, reverted).

## 6. Hooks + CI

- [x] 6.1 `.husky/` pre-commit (CSharpier staged + lint-staged) and commit-msg (commitlint)
      from kit refs; `.config/dotnet-tools.json` (+ dotnet-ef) and `commitlint.config.js`;
      self-installing via the Build.props target (made race-safe: read-before-write, because
      a solution build ran it once per project and they collided on `.git/config`).
- [x] 6.2 `.github/workflows/`: `ci.yml` orchestrator with the paths-filter `changes` gate
      (job-level `if`, never `on.paths`), reusable `lint.yml`, `build-test.yml`, `e2e.yml`,
      `openspec-validate.yml` (package + version pinned); artifacts `if: failure()` only.
- [x] 6.3 Verify: `git commit -m "fixed stuff"` is rejected by commitlint and the commit does
      not land; a conventional message passes (probed, reverted). CI verified on PR #1 — the
      first run found three real defects, listed below.

## 7. Close-out

- [x] 7.1 `ARCHITECTURE.md` + `src/modules/Projects/context.md`.
- [x] 7.2 README quick-start replaces the placeholder.
- [x] 7.3 Verify sweep: Release build of 12 projects — 0 errors, 0 warnings;
      `dotnet csharpier check src` clean; frontend `format:check`/`lint`/`typecheck`/`build`
      clean; 18 tests pass locally (6 unit, 5 arch, 7 functional).
- [x] 7.4 **CI green on every lane** — changes, lint (commitlint / backend-format /
      frontend-lint), build-test, openspec-validate, and e2e — on the branch HEAD.

### What CI caught that local verification did not

The first PR run failed three ways, all genuine:

1. **commitlint lane** ran `pnpm exec` at the repo root, which has no package. Fixed by running
   it from `src/frontend` against the root config — the same arrangement `.husky/commit-msg`
   uses, so the hook and CI can no longer disagree about what a valid message is.
2. **Prettier** flagged the generated `pnpm-lock.yaml`. Fixed with `.prettierignore`.
3. **A flaky test of mine.** `Create_Should_AssignTimeOrderedIdentifier` compared ids with
   `Guid.CompareTo`, which orders field-by-field and does not reflect GUID v7's time ordering.
   It passed locally and failed in CI. Now it compares the leading 48-bit big-endian timestamp
   bytes, with the version nibble asserted separately — 5 consecutive local runs green.

4. **A stale-artifact dependency in my own verification.** The SPA-serving functional tests
   passed locally only because an earlier `pnpm build` had left `wwwroot` behind; the CI lane
   built the frontend *after* running the tests, so they failed there. Reproduced locally by
   deleting `wwwroot` (2 failures, exactly as CI), fixed by building the frontend before the
   tests in `build-test.yml`, re-verified green. A textbook instance of the kit's own lesson
   that a green local run and a working system are different facts.

CI also confirmed the container setup works against real registries: the functional tier runs
green on the runner with no mirror configuration.

### What the E2E lane found on its first real run

Doc 01 says a smoke E2E "proved login had never worked end-to-end and surfaced three latent
defects." The same thing happened here. Two real defects, neither visible to any other tier:

1. **The host had no `http` endpoint.** Aspire derives a project resource's endpoints from its
   `launchSettings.json` profile, and `AiOrchestrator.Server` had no such file. Nothing could
   resolve the server by endpoint name — which broke `aspire run` too, so the dev loop had been
   broken all along and nothing else would have told us.
2. **Nothing applied database migrations.** `GET /api/projects` returned 500 against a database
   with no schema. The functional tier hid this by migrating inside its own fixture — a path the
   application itself did not have. Modules now expose a `Migrate` hook on `IModule`; the host
   runs it at startup in every environment except Production, where schema changes belong to a
   deliberate deploy step. The fixture dropped its parallel migration and starts the host
   instead, so tests and app now exercise the same path.

The fixture also gained host-log capture: the first red run reported a bare 500, because the
ProblemDetails body says nothing by design. A red E2E run now explains itself.

### Close-out note — the environment constraint, and what remains unproven

Container-registry egress is blocked in the implementation environment: `docker pull` hangs for
Docker Hub *and* for the mirror. Stated plainly rather than assumed away:

- **Local functional tests pass** only because their images were already cached; they were run
  with `TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX=public.ecr.aws/docker/library/` and
  `TESTCONTAINERS_RYUK_DISABLED=true`. The image names in the fixture are canonical, and CI needs
  neither variable — confirmed by the green runner.
- **The E2E lane could not run locally** and its first execution was on CI, where it is now green
  — including the browser journey that loads the SPA and reads a string from the i18n catalog.
- **`aspire run` still has not been executed by anyone.** The endpoint defect above was one
  reason it would have failed; that is fixed and the same composition now boots under
  `DistributedApplicationTestingBuilder` in CI, which is strong evidence but not the same act.
  It remains the one item in this change that no run has directly proven.
