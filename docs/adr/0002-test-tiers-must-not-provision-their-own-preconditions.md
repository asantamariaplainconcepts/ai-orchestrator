# ADR-0002: A test tier must not hide a precondition the application lacks

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** repo owner (solo path, DEC-016)
- **Tags:** testing, architecture

## Context

Twice, a green test suite concealed a defect that reached the application, and in both cases the
concealment was a property of how the tests were built rather than of what they asserted:

1. **The functional fixture migrated the database itself.** `ApiServiceFixtureBase` called the
   module's `DbContext.Database.MigrateAsync()` in its own setup. Every functional test passed
   against a correctly migrated schema — while the application had **no startup migration path at
   all**. The fixture was not testing the app's path; it was providing a parallel one, and the
   difference was invisible until the E2E lane booted the real host and got a 500.
2. **An all-sequential suite could not observe a concurrency bug.** `Sender` was registered as a
   singleton, so it resolved handlers from the root provider and scoped `DbContext`s degraded to
   root-cached instances — one context shared across concurrent requests. No sequential test can
   produce that failure, regardless of coverage. It surfaced only when the E2E lane's browser
   journey raced the API test, and even then intermittently.

The two share a root: a test tier that supplies its own preconditions, or that exercises only one
shape of traffic, measures itself rather than the application. Coverage numbers say nothing about
either failure.

## Decision

We will build test tiers so that they exercise the application's own paths and cannot substitute
for them:

- A fixture SHALL bring the system up the way the application does. Where the application performs
  startup work (migrations, seeding, registration), the fixture triggers that startup rather than
  reproducing its effect.
- Where a fixture must diverge from production (real containers instead of managed services,
  an E2E environment instead of Production), the divergence is deliberate, named, and does not
  extend to substituting application logic.
- At least one test exercises **concurrent** traffic, because a suite that only runs requests one
  at a time is structurally blind to lifetime and sharing bugs.

## Consequences

- **Positive:** the functional tier now starts the host and lets it migrate, so it covers the same
  path production uses — and would have failed on the missing migration instead of hiding it. A
  16-parallel-reads functional test pins the scoping regression that no sequential test could see.
- **Negative:** fixtures become slightly slower and more coupled to startup behaviour; a change to
  startup can now break the functional tier. That is the intended trade — the coupling is the
  point.
- **Neutral — the checks that would have caught each:**
  - private provisioning → the fixture starts the host (`CreateClient()`) instead of migrating,
    so the app's own path is the only path
  - sequential blindness → `ConcurrentRequests_Should_Constraint` fires parallel reads; scope
    validation (`ValidateScopes`/`ValidateOnBuild`) is now unconditional in every environment, so
    this bug class fails at startup rather than under load
- Reviewers should treat "the fixture sets this up" as a question, not a reassurance: *does the
  application do this too?*

## Alternatives considered

- **Keep fixture-provided setup and add an assertion that the app also does it** — rejected: two
  mechanisms that must agree will drift, and the assertion would itself be a claim nobody
  exercises (see [ADR-0001](0001-verify-claims-by-exercising-them.md)).
- **Rely on E2E to catch this class** — rejected as the primary defence: E2E did catch both, but
  it is the slowest and least frequent tier, and both defects had already been "green" for a full
  change before it ran. Cheap tiers should not lie.
- **Enable scope validation only in Development** — rejected: that is the framework default, and
  it is exactly why the scoping bug survived to E2E. A guard that runs in only one environment
  cannot protect the others.

## References

- Retro entries: `docs/process/retro-log.md` — 2026-07-25 `project-scaffolding` and
  2026-07-25 `ai-delivery-layer`
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md)
