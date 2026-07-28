# Proposal: self-host-distribution

## Why

Issue #99, executing DEC-049's operative half. The system runs under `aspire run` and in the
owner's subscription; "anyone can run this" needs a third habitat: **a machine with only Docker
and git** — clone, build locally, one command, zero Azure. Owner decision: no registry, no
published images — the multi-stage Dockerfiles carry the SDK, so `docker compose up --build` is
the whole distribution story.

## What Changes

- **The compose is generated, never written** (ADR-0003): `aspire publish` emits
  `selfhost/docker-compose.yaml` from the same AppHost `aspire run` uses. A CI job regenerates
  it and fails when the committed artifact drifts from the composition.
- **Publish-mode forks exactly two resources**: the Vite dev server exists only in run mode
  (published, the SPA is a build artifact the Server serves), and Azure Storage becomes an
  Azurite container, because `AddAzureStorage` in publish emits Azure provisioning and the
  output must contact zero Azure. Azurite's published well-known dev credential is a documented
  emulator constant, not a secret.
- **SELF-HOSTING.md**: clone, two values in `.env`, `docker compose up --build`, label a Story,
  watch a Run. The default runtime is opencode's free model — the demo costs nothing and needs
  no AI key (DEC-044); the only credential anywhere is the user's own GitHub PAT.

## Impact

- Affected specs: `dev-orchestration` (the third habitat).
- Touched: AppHost (compose environment + the two forks + deterministic volume name), the
  generated artifact, the drift-check workflow, SELF-HOSTING.md, README pointer.
- Out of scope: auth for internet-exposed hosts (OPN-002; the doc says trusted networks, like
  Orbion's daemons); Kubernetes outputs; any registry.
