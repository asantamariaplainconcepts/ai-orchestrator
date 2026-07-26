# ADR-0004: A verification asserts the observable artifact, not a proxy signal

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** repo owner (solo path, DEC-016)
- **Tags:** process, testing, observability

## Context

Twice now, a verification step reported success while the thing being verified did not work,
because what was checked was a *proxy* for the outcome rather than the outcome:

1. **fix-telemetry-collector-port.** The collector container ran, the port was free, the config
   was correct — and telemetry was still not captured, because the optional Grafana exporter's
   retry queue was silently disabling the file sink. "The container is running" was read as
   "telemetry is captured". The change was only truly finished when a synthetic payload was
   pushed through and the bytes read back out of `usage.jsonl`. That retro recorded the lesson
   as a first occurrence.
2. **github-connector-backlog-mirror.** While verifying the migration-bootstrap fix, a
   `GET /api/projects` returning HTTP 200 was read as "the API works". It did not: `UseSpa` was
   swallowing every request and the Vite dev server was answering `200 index.html` for `/api/*`
   too. The 200 was real; the claim it was taken to prove was false. The check that settled it
   was one Vite cannot fake — a `POST` returning `201` with the created entity.

The shared shape: a component in the wrong state can still emit a healthy-looking signal, and a
check that accepts that signal converts "broken" into "verified". This is distinct from
[ADR-0001](0001-verify-claims-by-exercising-them.md) (exercise the path instead of reading its
configuration): both failures above *did* exercise the path. The defect was in what the exercise
**asserted**.

Per the graduation rule (an ADR is written on the second occurrence, not the first), this is the
second occurrence.

## Decision

We will make every verification assert the **observable artifact of the claim**, chosen so that a
wrong-but-healthy component cannot produce it:

- Verifying data capture means reading the captured bytes back, not observing the capturer alive.
- Verifying an API means asserting the response *body* (or a state change, e.g. a `POST` → `201`
  with the created entity), never a bare status code — a fallback page, a proxy, or a cache can
  all answer 200.
- Verifying a schema step means querying the schema, not watching the migrator exit 0.
- In tests, prefer assertions that would fail loudly under substitution: exact JSON shape over
  "is 200", a row read back over "no exception thrown".

When a check cannot be made unfakeable, the verification must say so explicitly rather than
letting the proxy signal stand in silently.

## Consequences

- **Positive:** false-positive greens — the most expensive failures we have had, because they end
  investigations — become structurally harder. Review can ask one question of any verification:
  *"could a wrong component have produced what you asserted?"*
- **Negative:** verifications get longer to write; some need seed data or a write path where a
  ping would have felt sufficient.
- **Neutral:** existing tests are not retrofitted wholesale; the rule applies to new
  verifications and to any old one that is touched.

## Alternatives considered

- **Keep it as a retro lesson.** Rejected: it recurred within one change cycle of being written
  down; a lesson that must be remembered is weaker than a rule review can point at.
- **Mandate body assertions only for HTTP checks.** Rejected as too narrow: the telemetry
  occurrence was not HTTP, and the next one may not be either — the rule is about artifacts vs
  proxies, not about protocols.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — exercising the path is
  necessary; this ADR is about what the exercise must assert.
- Occurrence 1: retro entry *2026-07-25 — fix-telemetry-collector-port*.
- Occurrence 2: retro entry *2026-07-26 — github-connector-backlog-mirror*; PR #10.
