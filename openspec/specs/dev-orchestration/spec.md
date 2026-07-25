# dev-orchestration Specification

## Purpose
TBD - created by archiving change project-scaffolding. Update Purpose after archive.
## Requirements
### Requirement: one-command dev loop

`src/root/AiOrchestrator.AppHost` (Aspire) SHALL compose the full inner loop —
`AiOrchestrator.Server`, PostgreSQL, Azurite (the queue emulator, present from day 0
per DEC-013/D3), and the frontend Vite dev server — such that `aspire start` is the
only command needed to run everything locally.

#### Scenario: cold start

- **WHEN** a fresh clone runs `aspire start` after `dotnet build`
- **THEN** the website is reachable through the Server origin with live API, database,
  and queue emulator

### Requirement: OpenTelemetry from day 0

`AiOrchestrator.ServiceDefaults` SHALL wire OTel logs, metrics, and traces for every
service, with exporters selected by environment (OTLP locally, Azure Monitor in cloud
per DEC-023). `/api/health` and `/api/alive` SHALL be excluded from traces.

#### Scenario: traces flow locally

- **WHEN** the exemplar endpoint handles a request in the dev loop
- **THEN** its trace is visible in the Aspire dashboard, and health probes produce none

