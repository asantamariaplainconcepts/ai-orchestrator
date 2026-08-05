# ADR-0011: A worktree session carries its own telemetry, or the retro says which check failed

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** repo owner (DEC-003)
- **Tags:** process, telemetry, tooling

## Context

Retro entries carry measured time, and the measurement comes from the OTel Collector's persisted
export joined to `.telemetry/sessions.jsonl` on `session.id` (the `collect-usage` skill). A
session in a **git worktree** produces neither half:

- `OTEL_EXPORTER_OTLP_ENDPOINT` is unset there. The exporter is enabled, so it ships to the OTLP
  default port rather than this project's collector — the data is not dropped visibly, it is
  delivered somewhere nobody reads. Project `.claude/settings.json` does not deliver `OTEL_*` to
  every client; the variable has to live in the shell profile the app inherits.
- The `SessionStart` hook that maps `session.id → change` has never fired in a real session, so
  even data that arrived could not be attributed to a change.

Three consecutive changes have now paid for this: `local-code-source` recorded the gap and set a
graduation rule ("if a second worktree change loses its measurements the same way, that is the
graduation point for an ADR"); `default-automations-setup` was that second occurrence and said
the ADR was due; `prompt-picker` is the third. `node .config/otel/verify-telemetry.mjs` names all
of it in five checks, three of which fail — the collector is up and answering, and nothing has
ever been exported or mapped.

Nothing recovers telemetry that was never written. Each individual retro looked complete, because
"(manual)" reads as a legitimate time source rather than as a broken instrument.

## Decision

We will treat a worktree session's telemetry as **the session's own responsibility, verified
before the work rather than mourned after it**, and we will never let a broken instrument pass as
a footnote:

1. A change built in a worktree runs `node .config/otel/verify-telemetry.mjs` at the point
   `collect-usage` would read telemetry. Its failing checks are named in the retro entry, verbatim.
2. `manual` is a legitimate time source **only** when telemetry was working and the change
   predates it. When capture is broken, the entry says so and names the failing check — that
   wording is the difference between a measurement we chose not to take and one we lost.
3. Fixing the plumbing (the endpoint in the inherited shell profile, and the `SessionStart` hook
   reaching worktree sessions) rides with the next change that touches telemetry or worktree
   tooling. It does not block product changes, and it does not get quietly deferred forever
   either: this ADR is the record that it is owed.

## Consequences

- **Positive:** a retro can no longer look complete while the programme's measurements are being
  lost. The failing check is in the permanent record, which is what makes the third occurrence
  visible as a pattern rather than as three unrelated footnotes.
- **Negative:** every worktree change pays a verification step it cannot fix in place, and the
  retro entries carry a paragraph of instrument diagnostics that is not about the change itself.
- **Neutral:** the fix remains unscheduled by design. If a fourth and fifth change accumulate the
  same paragraph, that is the signal to stop deferring — and the entries will say so out loud.

## Alternatives considered

- **Fix the plumbing now, inside this change** — rejected because the change under way is a
  product feature and the fix touches shell profiles and hook delivery; bundling them would put
  two unrelated review surfaces in one squash commit, which the workflow exists to prevent.
- **Keep writing "(manual)" and move on** — rejected: that is precisely the documented shrug this
  ADR exists to end. Four changes used it before anyone noticed the programme had stopped
  measuring anything.
- **Disable the exporter in worktrees** — rejected because it makes the loss silent instead of
  loud, which is the opposite of what a broken instrument needs.
- **Backfill times by hand from session transcripts** — rejected: an estimate recorded in a field
  meant for a measurement is worse than an honest gap, and BR-011's rule about unmeasured cost
  ("unknown is never zero") is the same principle one level up.

## References

- Retro entries: `local-code-source` (rule set), `default-automations-setup` (second occurrence,
  ADR declared due), `prompt-picker` (third).
- Verifier: `.config/otel/verify-telemetry.mjs`; skill: `.claude/skills/collect-usage/SKILL.md`.
- **The plumbing landed** with `telemetry-worktree-attribution` (after `compose-per-resource`'s
  retro became the fourth and fifth entries carrying the paragraph): the verifier resolves the
  main repo root via `git rev-parse --git-common-dir` instead of its own location, and the
  mapping hook also fires on `UserPromptSubmit`, so a session that switches to the change branch
  after starting gets re-mapped on its next prompt. What that change's evidence corrected in this
  ADR's context: worktree sessions were exporting to the right collector and being mapped all
  along — the losses were the verifier misreading worktrees and the start-only mapping going
  stale on branch switch.
- Related: ADR-0009 (a claim about existing behaviour cites where it lives) — the same instinct,
  applied to claims rather than to measurements.
