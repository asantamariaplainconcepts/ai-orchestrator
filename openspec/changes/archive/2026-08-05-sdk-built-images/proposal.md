# sdk-built-images — proposal

Issue: #257 · Foundation · Actor: Developer (and the self-host operator) · UC-006 (indirectly)

## Why

The three hand-written Dockerfiles (server, migrations, dispatch worker) duplicate build
knowledge the toolchain already owns, and the duplication has bitten twice: the aspnet-vs-runtime
base image mistake was made independently in two images, and a stale `wwwroot` shipped once
before CI caught it. Meanwhile the operator's quickstart pays a ~20-minute local image build that
exists only because the images are built from source on their machine (#252 measured a 1.3GB
build context before the `.dockerignore` landed).

## What Changes

- **The three Dockerfiles are deleted.** .NET images are produced by SDK container publish
  (`/t:PublishContainer`) — base image inferred from the framework reference, non-root default.
- **The SPA rides into the server image through Aspire's JS publish path**
  (`PublishWithContainerFiles` or the nearest 13.4.x equivalent) instead of a hand-maintained
  Node build stage.
- **The pod launcher speaks `Docker.DotNet`, not the docker CLI** — the CLI binary was the last
  fact only a Dockerfile could express (`COPY --from=docker:29-cli`). Same socket, same named
  failure when the grant is absent; #246's stance is untouched.
- **CI publishes images to GHCR on merge to main**, tagged with the commit SHA.
  `selfhost/docker-compose.yaml` references them by tag: **the operator builds nothing**.
  DEC-049's litmus ("a stranger with Docker") is not weakened — it is strengthened, via the
  published-images path DEC-049 itself names.
- **BREAKING (operator):** the quickstart stops building images locally; `Dispatch__PodImage`'s
  default becomes the published dispatch-worker image (still operator-overridable).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dev-orchestration`: the "whole system runs from a clone on a Docker-only machine" requirement
  changes shape — images are pulled from the public registry rather than built from the clone,
  and the per-resource compose declarations describe image references instead of build contexts.
  The drift gate and the `.env` contract survive unchanged.

## Impact

- `src/root/AiOrchestrator.AppHost/AppHost.cs` — publish declarations per resource (#252's
  seam), image references instead of build contexts.
- `src/root/*/Dockerfile` ×3 — deleted; csproj gain `ContainerRepository`/publish properties.
- `src/shared/AiOrchestrator.ServiceDefaults/Dispatch/` (pod launcher) — docker CLI calls →
  `Docker.DotNet` against the mounted socket.
- `.github/workflows/` — a publish-images job on main; compose-drift gate adapts to tags.
- `selfhost/docker-compose.yaml` (generated), `selfhost/README.md`, `SELF-HOSTING.md` — the
  operator contract text.
- **Dependency to verify first:** the JS publish APIs against Aspire 13.4.x — if an upgrade is
  its own beast, it splits into a prior item (issue #257, Dependencies).
