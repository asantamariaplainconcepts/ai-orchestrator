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
      containers. **`aspire run` not executed in this environment** — see close-out note.

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
      Session container lifetimes, smoke journey. The host is forced to
      `ASPNETCORE_ENVIRONMENT=E2E`, which also puts the journey on the production serving path
      (static `wwwroot` + fallback) rather than the dev proxy — so E2E proves what ships.
      **Written and compiling; first real execution is the CI e2e lane** — see close-out note.

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
      clean; 17 tests pass (5 unit, 5 arch, 7 functional).

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

CI also confirmed the container setup works against real registries: the functional tier ran
7/7 green on the runner with no mirror configuration.

### Close-out note — what was NOT verified locally, and why

Container-registry egress is blocked in the implementation environment: `docker pull` hangs for
Docker Hub *and* for the mirror. Consequences, stated plainly rather than assumed away:

- **Functional tests pass** only because their images were already cached; they were run with
  `TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX=public.ecr.aws/docker/library/` and
  `TESTCONTAINERS_RYUK_DISABLED=true`. Image names in the fixture are canonical; CI needs no
  such variables.
- **`aspire run` was not executed.** Aspire's Postgres resource pulls an image that is not
  cached. The composition compiles and its pieces are covered by functional tests, but the
  one-command dev loop is unproven until someone runs it on a machine with registry access.
- **The E2E lane was not executed.** It needs both an uncached container image and a Playwright
  browser download. It compiles; the CI e2e lane is its first real run.

These three are the honest gaps in this change. The CI run on the PR is what closes the last two.
