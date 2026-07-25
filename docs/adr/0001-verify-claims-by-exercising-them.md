# ADR-0001: Verify claims by exercising them, never by reading configuration

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** repo owner (solo path, DEC-016)
- **Tags:** process, testing, infrastructure

## Context

Four times across the first two changes, something was believed to work because its configuration
said so, and was found broken only when something finally ran it:

1. **The host had no HTTP endpoint.** Aspire derives a project resource's endpoints from its
   `launchSettings.json` profile, and `AiOrchestrator.Server` had none. Nothing could resolve the
   server by endpoint name — which had also been silently breaking `aspire run`. Found by the E2E
   lane's first execution, not by the code that depended on it.
2. **Nothing applied database migrations.** A migration existed, the DbContext was registered, and
   `GET /api/projects` returned 500 against an empty schema. The application had no startup
   migration path at all.
3. **Health meant liveness, not usability.** `/api/health` reported healthy while the database was
   unusable, so "the host is up" and "the host can serve requests" were different facts that the
   check conflated.
4. **The E2E log watch returned silence.** It compiled, it ran, and it produced nothing — because
   it keyed on the resource's declared name when the stream is keyed by runtime `ResourceId`. Two
   consecutive red runs were diagnosed blind because the diagnostic itself was assumed to work.

The common shape is not carelessness about any one of these. It is that reading a configuration,
or seeing code compile, was treated as evidence of behaviour. This is the same failure the
framework's own post-mortem records as its second-most-frequent family: a CI step existing does
not mean it has ever succeeded.

## Decision

We will treat a claim about runtime behaviour as unverified until something has **exercised** it
and we have observed the result. Reading configuration, seeing a type compile, or noting that a
step exists is not verification.

Concretely, in this repository:

- Infrastructure claims entering a `design.md` are exercised first, and the design says so.
- A capability the application is supposed to have (serving an endpoint, applying migrations,
  reporting health) is covered by a test that drives it through the application's own path.
- A diagnostic — a log capture, an error body, a health check — is itself verified by making it
  report a known failure before it is trusted to explain an unknown one.

## Consequences

- **Positive:** the four defects above were each caught by exercising rather than reading, and
  each fix is now covered by something that runs. The E2E lane in particular has found a real
  defect on every first encounter with a new surface.
- **Negative:** exercising is slower than reading, and some things can only be exercised in CI
  (registry egress is blocked locally, so `aspire run` still has not been executed by anyone —
  recorded honestly rather than assumed working).
- **Neutral — the checks that would have caught each class, and where they now live:**
  - endpoint resolution and migrations → the E2E smoke journey drives the real AppHost
  - health-means-usable → the module registers a `DbContext` health check, so health fails when
    the database does
  - a silent diagnostic → the E2E failure path asserts the log tail is non-empty and says so
    explicitly when it is not
  - unverified infra in a design → `/aio:propose` requires claims to be exercised before they
    enter the proposal

## Alternatives considered

- **Trust configuration, verify only on failure** — rejected: this is the status quo that produced
  all four incidents, and three of them were invisible until a lane that had never run before ran.
- **Require every claim to be exercised locally** — rejected as unachievable here: container
  registry egress is blocked on the development machine, so CI is the only place some things can
  run. The honest rule is "exercised somewhere, and stated where", not "exercised locally".

## References

- Retro entries: `docs/process/retro-log.md` — 2026-07-25 `project-scaffolding` and
  2026-07-25 `ai-delivery-layer`
- Related: [ADR-0002](0002-test-tiers-must-not-provision-their-own-preconditions.md)
