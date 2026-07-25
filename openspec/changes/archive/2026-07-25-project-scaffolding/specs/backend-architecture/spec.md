# backend-architecture

## ADDED Requirements

### Requirement: self-registering modules

The host (`src/root/AiOrchestrator.Server`) SHALL discover and compose modules at
startup from assemblies matching `AiOrchestrator.Modules.*.dll` (excluding
`*.Contracts`), via the `IModule`/`ModuleBase` contract in
`src/shared/AiOrchestrator.BuildingBlocks`. Adding a module SHALL require no host
edits.

#### Scenario: new module joins without host changes

- **WHEN** a new `AiOrchestrator.Modules.<Name>` assembly is present in the output
- **THEN** its routes and services are registered at startup with zero `Server` diffs

### Requirement: vertical slices, one file per use case

Each use case SHALL be a single `sealed class <UseCase> : IUseCase` under
`Features/<Feature>/UseCases/`, exposing a static `AddRoutes` (minimal API), nested
`internal sealed` request/response/command records, a FluentValidation `Validator`,
and an `internal sealed Handler` returning `ErrorOr<T>`. Controllers SHALL NOT exist.

#### Scenario: the exemplar slice

- **WHEN** `POST /api/projects` is called with a valid body
- **THEN** `src/modules/Projects/.../Features/Projects/UseCases/CreateProject.cs`
  handles it end-to-end (route → validation → handler → 201 with the created id)

#### Scenario: invalid input becomes ProblemDetails

- **WHEN** `POST /api/projects` is called with an empty name
- **THEN** the response is an RFC 7807 ProblemDetails produced by
  `ApiResults.Problem`, not an exception page

### Requirement: fixed CQS decorator pipeline

Command/query dispatch SHALL flow through the decorator order
Logging → Validation → Caching → Handler → InvalidateCaching, registered solely by
`AddVsaCqsArchitecture()` (plain-dotnet-guardrails, DEC-015).

#### Scenario: pipeline order is not configurable per call site

- **WHEN** a handler executes
- **THEN** its logging scope and validation run in the fixed order regardless of
  registration order in the module

### Requirement: schema-per-module persistence

Each module SHALL own its `DbContext`, EF Core migrations, and seed data in a
PostgreSQL schema named after the module (`projects` for the Projects module), with
GUID v7 identifiers. Cross-schema access SHALL NOT occur.

#### Scenario: migrations are module-scoped

- **WHEN** the Projects module migration runs
- **THEN** all its tables land in the `projects` schema and no other schema changes
