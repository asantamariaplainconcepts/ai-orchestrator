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

The host SHALL compose the storing abstraction the same way and in the same place: Key Vault
where a vault is configured, a protected local store where the habitat is configured with one,
and — where neither can accept a value — an implementation that refuses with a reason naming what
to do instead. A habitat SHALL never be left with a store that appears to write and does not. The
protected local store SHALL use the framework's own data-protection implementation, holding
values outside the application database and key material apart from the values; the host SHALL
refuse to start when configured with one of those two locations and not the other.

#### Scenario: the same build runs in dev and in Azure

- **WHEN** the identical Server build starts with and without `Secrets:KeyVaultUri`
- **THEN** secret names resolve from Key Vault in the first case and from configuration in the
  second, with no code difference and no module involvement

#### Scenario: a secret is rotated in the vault

- **WHEN** a secret's value changes in Key Vault
- **THEN** the next resolution returns the new value without an application restart, because
  resolution happens per read (the seam's original contract)

#### Scenario: a habitat with no vault stores locally

- **WHEN** the system starts with no `Secrets:KeyVaultUri` in a habitat configured to store
  locally
- **THEN** values supplied through the product are written protected, with the key material held
  apart from them and outside the application database

#### Scenario: protected values without a separate key location

- **WHEN** the system is configured to store values locally but given nowhere separate for the
  key material
- **THEN** it refuses to start, saying that one location holding both is not protection

#### Scenario: a habitat that cannot store says so

- **WHEN** the system starts where no store can accept a value
- **THEN** attempts to store are refused with a reason naming what to do instead, rather than
  appearing to succeed

### Requirement: the caller's identity is composed by the host, per habitat

The system SHALL expose the current caller as a principal carrying an id and a display name,
resolved through one seam whose implementation the host composes. The principal SHALL NOT carry a
role: BR-009's roles are scoped to a project, so "this caller's role" is not a fact without naming
one, and a single field could only hold an invented answer (#13). There SHALL be no state
in which a caller has no principal, and no consumer of the seam SHALL branch on whether identity
is configured. On a machine-local deployment the principal SHALL be the machine's owner, requiring no
configuration, and SHALL hold every permission on every project — one person owning the machine is
the whole authorization model there. The host SHALL refuse to start when the local-owner
identity is configured together with provisioned infrastructure or a publicly reachable address,
naming which it found. A hosted deployment with neither the local owner nor an identity provider
SHALL announce that state at startup.

When identity-provider configuration is present, a hosted deployment SHALL compose the provider
instead of the announced stopgap, and the principal SHALL be the signed-in user: the provider's
stable object id as the id, the name claim as the display name. Composition SHALL key on the
presence of that configuration, never on an environment name. What a signed-in user MAY DO SHALL
come from their project roles (UC-002), replacing the interim rule under which every signed-in user
held Admin.

The machine-local mode and the provider mode SHALL coexist as alternatives: adding the provider
SHALL NOT change what a clean local checkout does, and a deployment with no provider
configuration SHALL keep the announced stopgap, because the self-host habitat has no tenant to
sign into.

#### Scenario: a clean local start has an owner

- **WHEN** the system starts from a clean checkout with no identity configuration
- **THEN** every request runs as the local owner, who holds every permission

#### Scenario: the local owner cannot reach provisioned infrastructure

- **WHEN** the local-owner identity is configured alongside provisioned infrastructure or a
  publicly reachable address
- **THEN** the host refuses to start and states which condition it found

#### Scenario: an unauthenticated hosted deployment says so

- **WHEN** a hosted deployment starts with neither the local owner nor an identity provider
- **THEN** it announces that it authenticates nobody, naming the open decision

#### Scenario: attribution names somebody

- **WHEN** work is recorded on a machine-local deployment
- **THEN** it is attributed to the local owner rather than to nothing

#### Scenario: a provider-configured deployment authenticates

- **WHEN** identity-provider configuration is present and a signed-in user calls the API
- **THEN** the principal is that user — their stable id and display name, and no role — and the
  startup warning about authenticating nobody does not fire

#### Scenario: the provider does not change the local mode

- **WHEN** the local-owner mode starts on a machine with no provider configuration
- **THEN** it behaves exactly as before the provider existed

### Requirement: feature state is composed from configuration alone

The Server SHALL compose `Microsoft.FeatureManagement` so that a feature's state is read from
`IConfiguration` and from nothing else. No Azure App Configuration client, endpoint or credential
SHALL be required to start, in any habitat — DEC-049 holds that a stranger with Docker can still run
this, and a managed configuration service would put a cloud dependency in the start path of a
self-hosted install.

A habitat that declares no features SHALL start exactly as it does today: composing the feature
manager is not itself a behaviour change, and nothing in this change consumes it.

This requirement is recorded as a seam with no consumer, knowingly and against
[RULE-007](../../../../docs/product/v1/08-backlog-shaping-rules.md)'s speculative-abstraction
anti-pattern. The owner decided (#331) that the plumbing lands here so the follow-on capability —
choosing a Run's isolation substrate per Automation — arrives against composition that already
exists. The reason is written down here rather than left to be re-derived, because the next reader
will otherwise correctly identify it as an abstraction nobody asked for.

#### Scenario: the feature manager resolves in every habitat

- **WHEN** the Server starts in the dev loop, in a compose self-host install, or in a deployment
- **THEN** `IVariantFeatureManager` resolves from the container, and no Azure App Configuration
  connection is attempted

#### Scenario: no declared features changes nothing

- **WHEN** the Server starts with no `FeatureManagement` section in configuration
- **THEN** startup succeeds and no behaviour observable to any existing scenario differs

#### Scenario: a declared feature is readable from configuration

- **WHEN** configuration declares a feature and the feature manager is asked for its state
- **THEN** the answer reflects the configured value, resolved without any external service

