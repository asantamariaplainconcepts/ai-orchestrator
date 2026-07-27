# Proposal: local-agent-loop

## Why

Issue #50. Every piece of the product is merged and tested at its seams, and **none of it has
ever executed for real**. The deployed path waits on operator steps; the local path is broken —
the AppHost gives the `dispatch` resource a queue but no database, so since #18 gave the worker
Run semantics it throws at startup: _"Connection string 'aiorchestratordb' is missing"_. Nobody
noticed because the resource is `WithExplicitStart()` and nobody pressed the button.

With #30's free opencode model, a working local loop costs nothing to run: `aspire run`, label a
Story, watch a Run appear and a pull request open — no Azure, no API key.

## What Changes

- **The worker gets what it needs**: database and Key Vault-less secret configuration references
  alongside the queue, so it composes the modules it has composed since #18.
- **Auto-start with restart-on-exit** (owner decision): the worker is a drain-and-exit batch job,
  so Aspire restarts it and a queued Run is picked up within seconds. The divergence from KEDA —
  which scales on queue length, where Aspire restarts unconditionally — is written into the spec,
  not implied away.
- **A local-only seeder** (owner decision): on first boot of the run composition, a demo project,
  its Connector and an OpenCode Automation exist, so the loop is clickable immediately. It is
  idempotent, reads the repository it points at from configuration, and is structurally
  impossible to reach from a deployed host.
- **Documented for what it proves**: the local loop exercises the queue contract, matching,
  execution and PR publication. KEDA scaling and Key Vault are _not_ exercised — their only
  proof is Azure, and a green local run must not read as a working scale rule.

## Impact

- Affected specs: `dev-orchestration` (the one-command loop grows to include the worker and the
  agent).
- Touched: AppHost, worker composition, a dev-only seeder, README/CONTRIBUTING.
- Out of scope: running the agent in a container locally (the CLI comes from the developer's
  PATH; the container path is proven by the image build and the first deployed run), KEDA
  emulation, seeding stories into the vendor repository.
