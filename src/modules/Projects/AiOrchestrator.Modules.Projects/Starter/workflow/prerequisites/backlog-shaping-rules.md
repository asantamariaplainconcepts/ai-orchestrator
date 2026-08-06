# Backlog-shaping rules

> **This is a starter, and it is now yours.** The spec-first workflow's grill prompt reads the
> definition of ready, which cites these rules by id. Renumber, cut or rewrite them — just keep the
> ids and the citations in step, because that is the whole mechanism.

How work items must be written. The grill ceremony reads this rubric; the
[definition of ready](./definition-of-ready.md) cites these ids.

- **RULE-001 — Required fields.** Every issue has: capability-oriented title; value in product terms;
  main actor; priority; dependencies (issue links); given/when/then acceptance criteria (deterministic
  — no "should work correctly"); affected business rules; affected use cases; explicit out-of-scope;
  and the **change/spec-ID** correlation key (ties issue → branch → PR → retro).
- **RULE-002 — One capability per issue.** An issue delivers exactly one user-visible capability or
  one enabling concern. If the acceptance criteria need "and" between two behaviours, split it.
- **RULE-003 — Traceability.** Product issues cite at least one use case. Enabling issues cite what
  they unblock instead.
- **RULE-004 — Sequence dependent slices.** An issue whose surface overlaps another in-flight issue
  is blocked behind it, not parallel to it.
- **RULE-005 — Classification required.** Every issue is product or enabling — enabling work is never
  smuggled into a feature item.
- **RULE-006 — No work on open decisions.** An issue depending on an unresolved question is blocked
  behind a decision-closure item. No proposing on a guess.
- **RULE-007 — Anti-patterns (reject at grill).** Umbrella issues ("build the reporting module");
  mechanism-grilled items (specifying *how* before *what*); speculative abstraction (a seam with zero
  consumers); acceptance criteria that restate the title; issues whose value sentence names a
  technology instead of an actor outcome.
