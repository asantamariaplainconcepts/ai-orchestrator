# sdk-built-images — design

## Context

Every image today is a hand-written multi-stage Dockerfile building from the repository root.
The server's is the load-bearing one: it installs Node to build the SPA into `wwwroot`, and it
copies the docker CLI binary out of `docker:29-cli` for the pod launcher (#246). The other two
are plain `dotnet publish` wrappers whose only non-obvious content is the aspnet-base lesson,
learned twice. The generated compose (#252) declares a `build:` context per service, so the
operator's `docker compose up` builds all of it locally.

The SDK's container publish cannot run arbitrary commands (no Node install, no COPY from another
image) — so the two hard facts in the server Dockerfile must move somewhere else before any
Dockerfile can be deleted: the SPA build moves to Aspire's JS publish machinery, and the docker
CLI dependency is dissolved in code by talking to the socket directly.

## Goals / Non-Goals

**Goals:**
- Zero Dockerfiles; images produced by `aspire publish`/SDK container publish.
- CI publishes server, migrations and dispatch-worker images to GHCR on merge, tagged by SHA.
- The generated compose references published images; the quickstart builds nothing.
- The pod launcher works without a docker CLI in the image.

**Non-Goals:**
- `aspire deploy` as the operator's path (DEC-049's stranger keeps `docker compose up`).
- Multi-arch manifests (linux/amd64 first; arm64 is its own item if wanted).
- Touching the Azure deployment path or any habitat declaration.

## Decisions

**D1 — verify the Aspire surface before anything else.** The JS publish APIs the docs describe
(`PublishWithContainerFiles`, Dockerfile builder) are documented against current Aspire; this
repo pins 13.4.4. Task 1 exercises `aspire publish` with an SDK-published project and the JS
resource on 13.4.x for real. If the needed APIs are missing, the change stops and an
Aspire-upgrade item is filed first (issue #257 records this escape valve). Evidence, not docs.

**D2 — the SPA is built by CI and carried into the server image as container files, not by a
Node stage.** `pnpm build` already exists as a first-class CI step (E2E depends on it); the
publish path reuses that output via the JS resource's publish integration
(`PublishWithContainerFiles` onto the server, or the closest 13.4.x idiom). The server keeps
serving `wwwroot` — the production serving path E2E already proves.

**D3 — Docker.DotNet replaces the CLI, same seam.** The launcher behind `IAgentPodsMonitor`/the
dispatch pod arrangement changes transport (CLI → socket API); its contract does not. The named
failure when the socket grant is absent must survive verbatim — that failure message is the
operator's documentation (#246). The functional tests that fake the seam stay green untouched;
what changes is the adapter behind it.

**D4 — GHCR, tagged by commit SHA, referenced by tag in the generated compose.** The deploy
workflow already logs into a registry to push (the ACA path); a `publish-images` job does the
GHCR side on main. The compose's image references carry an overridable tag variable in `.env`
(default: the release the clone is at), so the drift gate stays deterministic: regeneration
depends on committed state, never on "latest".

**D5 — the operator contract change is loud, not silent.** `selfhost/README.md` and
`SELF-HOSTING.md` rewrite the quickstart (pull instead of build), and the boot proof (D6) runs
the documented quickstart verbatim on fresh volumes.

**D6 — equivalence is proven by booting, as always.** Fresh volumes, published images pulled by
tag, postgres healthy → migrations exit 0 → server HTTP 200 serving the SPA — plus one
dispatched Run with the socket granted, because D3 changed the launcher's transport and only a
real pod start proves it (#99's lesson, twice confirmed by #252).

## Risks / Trade-offs

- [13.4.x lacks the JS publish APIs] → D1 fails fast into an explicit Aspire-upgrade item; no
  half-migrated state lands.
- [SDK-published image differs from the Dockerfile's (user, ports, env)] → the boot proof plus
  the E2E production-path suite compare observable behaviour; image internals may differ freely.
- [Docker.DotNet behaves differently than the CLI for pod start/exit semantics] → D6's real
  dispatched Run; the DispatchTests already cover the seam's contract.
- [A stranger's first `docker compose up` now needs network to GHCR] → they needed network to
  Docker Hub for base images anyway; the README says what is pulled and from where.
- [CI publish job adds a main-branch failure surface] → it rides the existing deploy workflow's
  pattern and its failure is visible in the merge-watch step /aio:sync already performs.

## Migration Plan

One change, ordered: D1 spike output recorded in the change → launcher to Docker.DotNet (tests
green) → csproj publish properties + AppHost publish declarations → CI publish job → compose
regenerated to tags → docs → boot proof. Rollback is `git revert` of the squash commit; the old
Dockerfiles come back with it.

## Open Questions

(none — the Aspire-version question is D1's task, with a named escape valve)
