# backend-architecture Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
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

### Requirement: secret resolution is composed by the host, per environment

The host's composition root SHALL select the `ISecretResolver` implementation by configuration:
when `Secrets:KeyVaultUri` is present it SHALL register the Key Vault resolver over Aspire's
Key Vault client integration (authenticating via `DefaultAzureCredential` — managed identity
when deployed, the developer's az login locally); otherwise the configuration-backed resolver
serves development and tests. Modules SHALL remain unaware of the backing store: no module
references a cloud SDK, and no call site changes when the implementation swaps.

#### Scenario: the same build runs in dev and in Azure

- **WHEN** the identical Server build starts with and without `Secrets:KeyVaultUri`
- **THEN** secret names resolve from Key Vault in the first case and from configuration in the
  second, with no code difference and no module involvement

#### Scenario: a secret is rotated in the vault

- **WHEN** a secret's value changes in Key Vault
- **THEN** the next resolution returns the new value without an application restart, because
  resolution happens per read (the seam's original contract)

