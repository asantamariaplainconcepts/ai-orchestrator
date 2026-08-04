# dev-orchestration — delta for compose-per-resource

## ADDED Requirements

### Requirement: the compose output is declared beside the resource it describes

Each fact in the generated compose SHALL be declared on the resource it describes — a build
context on the project it builds, a healthcheck on the container it probes, a wait condition on
the dependent that waits — and there SHALL be no global compose-file patch block. A compose fact
that cannot be expressed per resource SHALL move to the operator's override layer with its
reason documented, never back to a global block.

The operator's contract SHALL NOT change: the same `.env` variables, the same `docker compose
up`, a server that answers on `SERVER_PORT`.

#### Scenario: the generated compose boots

- **WHEN** the regenerated compose is brought up with fresh volumes
- **THEN** postgres reaches healthy, migrations complete, and the server starts and answers —
  exercised for real, never inferred from the YAML

#### Scenario: no dead service names

- **WHEN** a service is removed from the composition
- **THEN** no patch site keeps naming it, because every declaration lives on a resource that
  either exists or is gone
