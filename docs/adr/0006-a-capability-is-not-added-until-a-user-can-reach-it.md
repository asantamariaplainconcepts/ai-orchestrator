# ADR-0006: A capability is not added until a user can reach it

- **Status:** Accepted
- **Date:** 2026-07-27
- **Deciders:** owner (solo path, DEC-016)
- **Tags:** process, backend, frontend

## Context

Twice now a change has added a second implementation behind a working seam, verified it
thoroughly, and shipped it unreachable — because a *first* implementation had left behind a
"there is only one of these for now" constant that nothing in the new change's tests touched.

- **#30 (opencode runtime).** `AgentRuntime.OpenCode` was implemented, registered, selected by
  `IAgentRuntimeSelector`, and exercised against a real CLI in a clean container. The portal's
  runtime picker was still `<select … disabled>` with the comment *"One runtime until OPN-004
  closes"*. OPN-004 had closed in that very change. No Automation could be created with the new
  runtime, so no Run could ever use it.
- **#29 (Azure DevOps connector).** Every seam method implemented, 23 translation unit tests
  green, and — the headline result — the guardrail suite passing with two vendor implementations
  present. `ConfigureConnector` still read
  `const BacklogVendor vendor = BacklogVendor.GitHub;`, so no Azure DevOps Connector could be
  configured. This one was caught before merge, but only by inspecting the configure slice on a
  hunch; nothing in the change's own tests would have failed.

The common shape: the new code is correct, its tests pass, the architecture guardrails pass, and
the feature does not exist from outside. Every signal the change produced was green, because
every signal was pointed at the new code. The defect was in code the change did not modify.

Existing rules do not cover this. ADR-0004 says a verification asserts the artifact rather than a
proxy — but here the *artifact under test* was itself the proxy: the connector works; the product
cannot use it. ADR-0001 says exercise the claim — and the claim "the connector translates work
items" was exercised honestly. The missing claim was never stated: *a user can select this.*

## Decision

We will treat a capability as added only when the path from a user-facing entry point to the new
code is unbroken, and we will prove it at that entry point.

Concretely, a change that adds an implementation behind an existing seam SHALL, before writing
the implementation, trace the path from the outermost caller — the HTTP request, the form control
— to the seam, and SHALL list every place the previous implementation's uniqueness was assumed:
hardcoded constants, single-option or disabled controls, defaults that silently substitute,
enumerations with one member. Each is a task in `tasks.md`.

The change SHALL then carry at least one test that fails if the entry point cannot select the new
implementation. That test asserts at the entry point, not at the seam. Where the entry point is
the API, it is a functional test naming the new option; where it is a form control, it is the
existing lint/typecheck plus an explicit reviewer check that the control is enabled and its "only
one for now" comment is gone.

## Consequences

- **Positive:** the class of bug where a fully-verified feature is unreachable stops being
  invisible. The trace step is cheap — minutes — and it runs when the design is still malleable.
  It also finds the *stale comment* that documents a closed decision as open, which is how #30's
  picker survived review.
- **Negative:** it adds a mandatory step to a category of change that already feels finished when
  the seam is implemented, which is exactly when discipline is hardest. It cannot be fully
  automated: "a disabled select" is not a pattern a compiler flags.
- **Neutral:** the reachability tests added under this rule are the first tests in the project
  that exist to catch omission rather than error. Expect them to look trivial and to be the ones
  that fail.

**The check this names.** Every "add a second implementation" change gets a `tasks.md` entry
enumerating the first implementation's uniqueness assumptions, and a test at the entry point that
selects the new implementation by name. `/aio:implement` treats a missing enumeration the way it
treats a missing spec delta. The retro at `/aio:sync` asks, in one line: *could a user have
reached this before the change merged?*

## Alternatives considered

- **Rely on E2E coverage.** Rejected: E2E tests exercise the paths someone thought to write, and
  nobody writes an E2E test for an option they have forgotten is unselectable. #30 had a green
  E2E suite.
- **A lint rule against `disabled` controls and single-branch constants.** Rejected as the
  primary mechanism: both are legitimate in the interim state this project deliberately passes
  through (a seam shipped before its second implementation). Banning them would push the same
  staleness somewhere less visible. It may be worth adding later as a *reminder* — a warning that
  names the OPN/DEC in the comment and asks whether it has closed.
- **Require the second implementation to ship in the same change as the seam.** Rejected: it
  contradicts DEC-011 and DEC-012, which sequence vendors and runtimes deliberately, and it would
  make every seam twice as expensive to introduce.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md),
  [ADR-0004](0004-a-verification-asserts-the-artifact-not-a-proxy-signal.md)
- Incidents: [#30](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/30)
  (runtime picker), [#29](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/29)
  (connector vendor constant)
