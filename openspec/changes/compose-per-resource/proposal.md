## Why

Issue #252. The AppHost's `ConfigureComposeFile` block is ~60 lines of after-the-fact file
surgery written when Aspire had no better seam (#99). Two of its entries are already dead — the
builds dictionary and the depends_on loop still name `dispatch`, removed at #225 — which is the
tell: a global patch block drifts because nothing ties a patch to the resource it describes.
Aspire 13.4 has per-resource idioms for everything the block does.

## What Changes

- Every compose fact moves to the resource it describes, via
  `PublishAsDockerComposeService((resource, service) => …)`: the server's build context and
  `${SERVER_PORT}` mapping, migrations' build context, postgres's database, healthcheck and the
  dependents' `service_healthy` conditions.
- `ConfigureComposeFile` is deleted entirely (grilled: zero — where an idiom is missing, another
  route is found), and the dead `dispatch` entries go with it.
- The generated compose is **equivalent, not identical**: the drift gate regenerates once, and
  the proof is a real `docker compose up` to a healthy, answering server (the #99 lesson).
- The operator's `.env` contract (`POSTGRES_PASSWORD`, `SERVER_PORT`) is unchanged.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dev-orchestration`: the compose output is declared per resource; the global patch block is
  retired.

## Impact

- `src/root/AiOrchestrator.AppHost/AppHost.cs` and the regenerated
  `selfhost/docker-compose.yaml`. No product code, no habitat declaration changes (#246/#247/#250
  untouched).
