# repo-structure

## ADDED Requirements

### Requirement: src/ is the solution root

All .NET solution configuration SHALL live under `src/` — `AiOrchestrator.slnx`,
`Directory.Build.props`, `Directory.Packages.props`, `global.json` — with the repo
root reserved for cross-cutting tooling and docs (`.husky/`, `.config/`, `.github/`,
`docs/`, `openspec/`).

#### Scenario: solution builds from src

- **WHEN** `dotnet build` runs against `src/AiOrchestrator.slnx`
- **THEN** it succeeds using only configuration files under `src/`

### Requirement: warnings are build failures

`src/Directory.Build.props` SHALL set `TreatWarningsAsErrors` and
`EnforceCodeStyleInBuild` for every project, and SHALL auto-attach
`AiOrchestrator.ArchitectureAnalyzers` as an analyzer reference to every project
matching `*.Modules.*`.

#### Scenario: a warning fails the build

- **WHEN** any project contains code producing a compiler or style warning
- **THEN** `dotnet build` fails

#### Scenario: analyzers attach without per-project opt-in

- **WHEN** a new `AiOrchestrator.Modules.<Name>` project is added with no analyzer
  reference of its own
- **THEN** MOD001–005/CQS001 diagnostics are active in it at build time

### Requirement: pinned toolchain

`src/global.json` SHALL pin the .NET SDK to `10.0.100` with
`rollForward: latestFeature`; `src/Directory.Packages.props` SHALL enable Central
Package Management with transitive pinning and `Roslynator.Analyzers` as a
`GlobalPackageReference`.

#### Scenario: version drift is explicit

- **WHEN** a package version is declared in an individual `.csproj`
- **THEN** the build fails until the version moves to `Directory.Packages.props`

### Requirement: line-ending hygiene

The repo root SHALL carry `.gitattributes` with `* text=auto eol=lf` from the first
commit.

#### Scenario: checkout on Windows

- **WHEN** the repo is cloned on Windows and a file is committed
- **THEN** the stored blob uses LF endings
