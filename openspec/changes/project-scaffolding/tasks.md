# Tasks — project-scaffolding

Ordered per doc-02 day-0 sequence; each task is small, verifiable, one-session.

## 1. Repo & build skeleton

- [ ] 1.1 `src/` layout: `AiOrchestrator.slnx`, `global.json` (SDK 10.0.100,
      rollForward latestFeature), `Directory.Build.props` (TFM, Nullable,
      TreatWarningsAsErrors, EnforceCodeStyleInBuild, analyzer auto-attach to
      `*.Modules.*`, HuskyInstall target), `Directory.Packages.props` (CPM, transitive
      pinning, Roslynator as GlobalPackageReference), `.editorconfig` from kit refs.
- [ ] 1.2 Verify: `dotnet build` succeeds on the empty solution; a deliberate warning
      fails the build.

## 2. Kernel + reference module + host

- [ ] 2.1 `src/shared/AiOrchestrator.BuildingBlocks`: IModule/ModuleBase discovery,
      CQS (ICommand/IQuery, IAppCommandHandler/IAppQueryHandler, decorator pipeline
      Logging → Validation → Caching → Handler → InvalidateCaching via
      `AddVsaCqsArchitecture()`), ErrorOr + ApiResults.Problem + global exception
      handler, Domain primitives (Aggregate/BaseEntity, GUID v7), Diagnostics
      (source-generated logging, shared ActivitySource).
- [ ] 2.2 `src/shared/AiOrchestrator.ServiceDefaults` (OTel logs/metrics/traces,
      health endpoints excluded from traces) + `AiOrchestrator.Constants`.
- [ ] 2.3 `src/modules/Projects/AiOrchestrator.Modules.Projects`: module class,
      `projects` schema DbContext + migration, exemplar slice
      `Features/Projects/UseCases/CreateProject.cs` (sealed IUseCase, AddRoutes,
      internal records, Validator, internal sealed Handler, ErrorOr) — UC-003 shape only.
- [ ] 2.4 `src/root/AiOrchestrator.Server` (module discovery, minimal API edge,
      `/api/health` + `/api/alive`) + `src/root/AiOrchestrator.AppHost` (Aspire:
      Postgres, Azurite, Server, frontend dev server).
- [ ] 2.5 Verify: `aspire start` boots everything; `POST /api/projects` round-trips.

## 3. Guardrails (before any second module)

- [ ] 3.1 `src/shared/AiOrchestrator.ArchitectureAnalyzers`: MOD001–005 + CQS001 from
      kit refs, renamed; auto-attached via Build.props.
- [ ] 3.2 `src/tests/AiOrchestrator.ArchTests`: module-boundary, no-controllers,
      interface naming, unique LoggerMessage event IDs, `Subject_Should_Constraint`
      test-name regex.
- [ ] 3.3 Verify: a deliberate cross-module impl reference fails the build (MOD002);
      a deliberately misnamed test fails ArchTests. Revert both probes.

## 4. Test bases

- [ ] 4.1 `src/tests/AiOrchestrator.SharedFunctionalTests`: ApiServiceFixtureBase
      (WebApplicationFactory + Testcontainers Postgres/Azurite + Respawn reset),
      ICollectionFixture per module stack.
- [ ] 4.2 `src/tests/modules/Projects/{AiOrchestrator.Modules.Projects.UnitTests,
      .FunctionalTests}`: unit tests for the exemplar handler; functional test for
      `POST /api/projects` against real containers.
- [ ] 4.3 `src/tests/AiOrchestrator.EndToEndTests`: DistributedApplicationTestingBuilder
      harness + Playwright, `ASPNETCORE_ENVIRONMENT=E2E` appsettings, Session container
      lifetimes, artifact export on failure. One placeholder journey: app serves the SPA
      shell.

## 5. Frontend skeleton

- [ ] 5.1 `src/frontend/`: Vite + React + TypeScript + React Router, pnpm standalone;
      `features/`, `app/` thin routes, `shared/{http,query,session}`; TanStack Query;
      typed i18n catalog (`en`) with one seeded string; Projects list/create screen
      against the exemplar API.
- [ ] 5.2 Same-origin wiring: dev proxy via Aspire service discovery; `pnpm build`
      output copied to Server `wwwroot` with index.html fallback + reserved prefixes.
- [ ] 5.3 ESLint flat config with `--max-warnings=0`, Prettier, i18n hardcoded-JSX-copy
      rule enabled and failing.
- [ ] 5.4 Verify: `pnpm build` + `dotnet build` serve the SPA from the host;
      a hardcoded JSX string fails `pnpm lint`.

## 6. Hooks + CI

- [ ] 6.1 `.husky/` pre-commit (CSharpier staged, lint-staged Prettier/ESLint) +
      commit-msg (commitlint conventional) from kit refs; `.config/dotnet-tools.json`
      + `commitlint.config.js`; self-install via the Build.props target.
- [ ] 6.2 `.github/workflows/`: orchestrator `ci.yml` with paths-filter `changes` gate
      (job-level `if`, never `on.paths`); reusable lint (CSharpier check, Prettier
      check, ESLint, tsc --noEmit, commitlint), build-test (dotnet build Release +
      all test tiers except E2E-cloud), e2e (local emulator mode), spec-validate
      (`npx @fission-ai/openspec@1.6.0 validate --changes`); artifacts `if: failure()`
      only.
- [ ] 6.3 Verify: a bad commit message is rejected locally AND by CI on a probe
      branch; doc/spec-only diff skips build lanes while required checks report.

## 7. Close-out

- [ ] 7.1 `ARCHITECTURE.md` (bird's-eye) + `src/modules/Projects/context.md`.
- [ ] 7.2 README quick-start replaces placeholder (aspire start, test commands).
- [ ] 7.3 Full verify sweep: checklist Phase 1 verify items all true; CI green on PR.
