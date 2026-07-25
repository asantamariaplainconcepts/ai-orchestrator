# testing-strategy Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
### Requirement: four tiers exist from day 0

The solution SHALL ship all four test tiers wired and green: unit
(`src/tests/modules/<M>/*.UnitTests`, xUnit + Shouldly), functional
(`*.FunctionalTests` on the shared fixture), E2E
(`src/tests/AiOrchestrator.EndToEndTests`), and ArchTests
(`src/tests/AiOrchestrator.ArchTests`).

#### Scenario: all tiers run in one command

- **WHEN** `dotnet test src/AiOrchestrator.slnx` runs with Docker available
- **THEN** unit, functional, and Arch tiers execute and pass (E2E excluded by trait
  unless explicitly requested)

### Requirement: functional tests run against real infrastructure

Functional tests SHALL use a shared `ApiServiceFixtureBase`
(`src/tests/AiOrchestrator.SharedFunctionalTests`): `WebApplicationFactory` +
Testcontainers (PostgreSQL, Azurite) + Respawn DB reset per test, with **one container
stack per module** via `ICollectionFixture`. In-memory database substitutes SHALL NOT
be used.

#### Scenario: state never leaks between tests

- **WHEN** two functional tests in the Projects collection run in sequence
- **THEN** the second observes a Respawn-reset database, same containers

### Requirement: E2E drives the real app

E2E tests SHALL boot the real AppHost via `DistributedApplicationTestingBuilder` and
drive it with Playwright under `ASPNETCORE_ENVIRONMENT=E2E` (own appsettings, Session
container lifetimes — never Persistent), exporting screenshots/traces on failure only.

#### Scenario: the smoke journey

- **WHEN** the placeholder E2E journey runs
- **THEN** the real host serves the SPA shell and the journey passes against it

### Requirement: architecture rules are tests

ArchTests SHALL fail when: a module assembly references another module's
implementation assembly; a controller exists; an interface name lacks the `I` prefix;
`[LoggerMessage]` event IDs collide across the solution; or a `[Fact]`/`[Theory]`
name does not match `^\w+_Should_\w+$`.

#### Scenario: bad test name fails the suite

- **WHEN** a test named `TestCreateProjectWorks` is added anywhere
- **THEN** `AiOrchestrator.ArchTests` fails naming enforcement

