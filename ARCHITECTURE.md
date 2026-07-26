# Architecture

Bird's-eye map. Behaviour lives in `openspec/specs/`; product truth in
[docs/product/mvp/](docs/product/mvp/00-product-brief.md); decisions in `docs/adr/` and the
[locked-decision log](docs/product/mvp/10-locked-mvp-decisions.md). This file links, it does not
restate.

## Shape

```
src/
├── AiOrchestrator.slnx  Directory.Build.props  Directory.Packages.props  global.json  .editorconfig
├── shared/
│   ├── AiOrchestrator.BuildingBlocks/        # Modules, CQS, Domain, Api — primitives only
│   ├── AiOrchestrator.ServiceDefaults/       # OpenTelemetry, health, service discovery, resilience
│   └── AiOrchestrator.ArchitectureAnalyzers/ # MOD001–005 + CQS001, auto-attached to every module
├── modules/
│   ├── Projects/AiOrchestrator.Modules.Projects/   # the reference module (BC-001)
│   └── Backlog/AiOrchestrator.Modules.Backlog/     # Connector + mirrored Stories (BC-002)
├── root/
│   ├── AiOrchestrator.Server/                # BFF host: module composition + SPA same-origin
│   ├── AiOrchestrator.MigrationService/      # the migration step; Server waits on its completion
│   └── AiOrchestrator.AppHost/               # Aspire: Postgres + Azurite + migrations + host + Vite
├── frontend/                                 # Vite + React SPA, standalone pnpm project
└── tests/
    ├── AiOrchestrator.ArchTests/             # runtime complement to the analyzers
    ├── AiOrchestrator.SharedFunctionalTests/ # Testcontainers + Respawn fixture base
    ├── AiOrchestrator.EndToEndTests/         # real AppHost + Playwright
    └── modules/{Projects,Backlog}/{...UnitTests, ...FunctionalTests}
```

Outside `src/`: `infra/` holds the Terraform for the Azure dev environment and the
bootstrap/deploy scripts.

`src/` is the solution root; the repo root holds only cross-cutting tooling and docs.

## Backend

A modular monolith with enforced seams. The host discovers `AiOrchestrator.Modules.*.dll` at
startup ([ModuleRegistration](src/shared/AiOrchestrator.BuildingBlocks/Modules/ModuleRegistration.cs)),
so adding a module needs no host edit. Each module owns a PostgreSQL schema, its migrations, and
its feature slices.

**The Server never migrates — in any environment.** Migrations are a separate resource in the
AppHost graph (`AiOrchestrator.MigrationService`, which runs every module's `IModule.Migrate`
and exits); the Server starts only after it completes. The in-process predecessor was gated on
`!IsProduction()` and under `aspire run` the environment silently defaulted to Production —
fresh database, no schema. In production the same executable runs as a deliberate deploy step
(#8); the functional-test fixture calls the same `MigrateModules` itself. Backlog was the first module to test that claim rather than assert it: adding
it changed the solution file and nothing in `Program.cs`.

There are two modules, and the boundary between them cost something worth knowing about. The
Connector is configuration of a Project but lives in **Backlog**, because everything that reads
or writes one — verification, polling, failure recording — is a Backlog concern. The price is
that `Connector.ProjectId` carries no foreign key, since a cross-schema constraint is the
coupling the boundary exists to prevent. [The Backlog context](src/modules/Backlog/context.md)
records the reasoning and the deletion debt that follows from it.

A use case is **one file**: route + request/response + command + validator + handler, nested and
`internal` — see the exemplar,
[CreateProject.cs](src/modules/Projects/AiOrchestrator.Modules.Projects/Features/Projects/UseCases/CreateProject.cs).
Requests travel a fixed decorator pipeline owned solely by
[`AddVsaCqsArchitecture()`](src/shared/AiOrchestrator.BuildingBlocks/CQS/AddVsaCqsArchitecture.cs):

```
Logging → Validation → Caching → Handler → InvalidateCaching
```

Two error channels, deliberately distinct:

- **Domain errors** are values: handlers return `ErrorOr<T>`, endpoints map them through
  [`ApiResults.Problem`](src/shared/AiOrchestrator.BuildingBlocks/Api/ApiResults.cs).
- **Input-validation failures** short-circuit the pipeline as an exception, rendered by the one
  [`GlobalExceptionHandler`](src/shared/AiOrchestrator.BuildingBlocks/Api/GlobalExceptionHandler.cs).

Both emit RFC 7807 `application/problem+json`. Nothing else writes an error body.

## Deployment

The dev environment is Terraform in `infra/dev/` (northeurope, `aio-dev-*`): resource group, Log
Analytics, a Container Apps environment, ACR, PostgreSQL Flexible Server, Key Vault, the portal
container app, and the migration job. `infra/bootstrap.sh` creates the remote-state backend once;
`infra/deploy.sh` performs a release.

**Who applies what.** Humans apply Terraform and run deploys with their own Azure identity. CI
validates (`fmt`, `validate`, `shellcheck`) and holds no credentials — a federated CI identity is
a later, deliberate decision, not a default. The configuration refuses the wrong target: a guard
compares the resolved subscription against a committed SHA-256 and fails at plan time, which is
how the subscription id stays out of a public repository while still being checked.

**Credentials flow one way.** Terraform generates the database password *into* Key Vault. Both
the app and the migration job carry a system-assigned identity with read-only vault access and
pull-only registry access; their configuration holds a vault URI and nothing secret. The
application resolves names through `ISecretResolver` at the moment of use — the same seam that
reads user-secrets locally (BR-010).

**Release ordering.** `deploy.sh` pushes images, runs the migration job, waits for exit 0, and
only then moves the app revision. A failed migration leaves the previous revision serving. The
Server never migrates, in any environment — locally the AppHost's `migrations` resource does it,
in Azure the job does.

## Dispatch

A Run reaches an Agent through an Azure Storage Queue (DEC-013) that KEDA watches: a message
arrives, a Container Apps Job starts, it drains the queue and exits. `IRunDispatcher` is the
seam; the queue implementation sits in ServiceDefaults beside the Key Vault resolver, so no
module reaches a cloud SDK.

**The message is a Run id and nothing else.** The worker reads the Run, its Story and its
Automation from Postgres — one source of truth, and nothing to go stale between enqueue and
execution.

**A claimed message is deleted before any work happens, and that is load-bearing.** Storage
Queues are at-least-once: a consumer that dies leaves its message to reappear, and KEDA starts
another job — an automatic retry, which [BR-004](docs/product/mvp/05-business-rules.md) forbids.
The rule wins. The cost is stated rather than hidden: a job killed by infrastructure is
indistinguishable from an Agent that failed, and both need a human to re-trigger via *Run now*
(BR-013). **Do not "fix" this back to at-least-once** — the deletion is the rule.

Agent jobs run under a **different identity from the portal's**: they will clone repositories
with project PATs, and one compromise should not reach both.

**What the local path does and does not prove.** Azurite exercises the full enqueue → claim →
delete contract on every machine and in the functional tier. KEDA has no local equivalent, so
the scale rule is only ever verified in Azure — a green local run says nothing about it.

## Integration events

Modules never call each other. A module announces a fact — `StoryChanged` — through
`IIntegrationEventPublisher`, and other modules react through `IIntegrationEventHandler<T>`.
Both interfaces live in BuildingBlocks and speak product vocabulary only; the implementation
(DotNetCore.CAP: Postgres outbox + in-memory transport) sits in ServiceDefaults, exactly like
the Key Vault resolver and the dispatch queue. No module references CAP, directly or
transitively — the ArchTests pin it.

**Publish is transactional, and that is the entire point.** The publisher's `BeginTransaction`
spans the module's own writes and its staged events: a Story change and its `StoryChanged`
event commit or roll back together, so a consumer never reacts to a write that didn't happen
and a write never goes unannounced. The functional suite asserts the rollback case against the
outbox itself.

**Delivery is at-least-once; every handler must be idempotent.** A process that dies mid-handle
redelivers after restart (observed in the change's spike, not assumed). Retries are a
deliberate small ceiling (3), and an exhausted message is dead — loudly logged, never silently
dropped. Automatic *re-running of Runs* is still forbidden (BR-004); retrying an event handler
is not a Run retry.

**Events carry identity, never state.** `StoryChanged` says *which* Story changed and *how*
(added/updated/removed) — a consumer reads current truth through the owning module's
`.Contracts` assembly rather than trusting a payload that may be stale by the time it arrives.
The wire name is versioned (`backlog.story-changed.v1`); an unrecognised name is dropped
explicitly rather than misread.

**`.Contracts` assemblies are the only cross-module surface.** They hold events, enums, and
read interfaces — no implementation types (the ArchTests verify both directions). Module
discovery skips them; the owning module registers any implementations itself. The `cap` schema
is created by the MigrationService like every other schema — the Server migrates nothing.

## Frontend

One React SPA (Vite, React Router, TanStack Query) served **same-origin** by the host: proxied to
the Vite dev server in development, served from `wwwroot` with an `index.html` fallback everywhere
else. Consequently there is no CORS configuration and no API base URL — calls are relative paths.
Reserved prefixes (`/api`, `/openapi`, `/scalar`, `/health`) are matched by routing before either
fallback branch.

Feature code is co-located in `features/<feature>/`; `app/` holds thin routes; only cross-cutting
plumbing lives in `shared/`. All user-facing copy comes from the typed catalog in
`shared/i18n/` — hardcoded JSX copy fails lint.

## Guardrails

| Where | What |
|---|---|
| Compile | MOD001–005, CQS001 (Error severity), `TreatWarningsAsErrors`, Roslynator, `.editorconfig` style rules |
| Test | ArchTests: cross-module assembly refs, no controllers, `I`-prefix, unique `LoggerMessage` IDs, `Subject_Should_Constraint` naming |
| Commit | Husky (self-installing): CSharpier + lint-staged; commitlint |
| CI | Same gates mirrored, so `--no-verify` is still caught; plus build/test, E2E, spec validation |

MOD002 and the ArchTest assembly check are **complementary, not redundant**: the analyzer catches
another module's type in a member *signature*; the ArchTest catches the assembly reference however
it arises, including use only inside a method body. Both are needed — verified with probes.

## What is deliberately absent

Terraform and the deploy lane (their own change), authentication (blocked by
[OPN-002](docs/product/mvp/07-open-decisions.md)), the design system (bootstrap Phase 4), and every
product capability beyond the exemplar slice: Connectors, Automations, dispatch, Agent execution.
Those arrive one reviewed change at a time.
