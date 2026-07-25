# Backlog-shaping rules

How work items must be written. The grill ceremony reads this rubric; the Definition of
Ready cites these IDs.

- **RULE-001 — Required fields.** Every issue has: capability-oriented title; value in
  product terms; main actor (ACT-xxx); priority; dependencies (issue links); given/
  when/then acceptance criteria (deterministic — no "should work correctly"); affected
  business rules (BR-xxx); affected use cases (UC-xxx); explicit out-of-scope; and the
  **change/spec-ID** correlation key (ties issue → PR → telemetry → retro).
- **RULE-002 — One capability per issue.** An issue delivers exactly one user-visible
  capability or one foundation concern. If the acceptance criteria need "and" between
  two behaviors, split it.
- **RULE-003 — Traceability.** Product issues cite ≥1 UC-xxx. Foundation issues cite
  the foundation entry they enable ([09](09-foundation-vs-product-split.md)) instead.
- **RULE-004 — Sequence dependent slices.** An issue whose surface overlaps another
  in-flight issue (in-progress *or* code-review) is blocked behind it, not parallel to
  it. Connector #2 (AzDO) and runtime #2 (opencode) always sequence behind their #1.
- **RULE-005 — Classification required.** Every issue is Product or Foundation
  ([09](09-foundation-vs-product-split.md)) — enabling work is never smuggled into a
  feature item.
- **RULE-006 — No work on open decisions.** An issue depending on an OPN-xxx is
  blocked behind a decision-closure task. No proposing on a guess.
- **RULE-007 — Anti-patterns (reject at grill).** Umbrella issues ("build the Agents
  module"); mechanism-grilled items (specifying *how* before *what*); speculative
  abstraction (a seam with zero consumers); acceptance criteria that restate the title;
  issues whose value sentence names a technology instead of an actor outcome.
