# ADR-0025: Human time is recorded by a person, never derived from telemetry

- **Status:** Accepted
- **Date:** 2026-08-12
- **Deciders:** repository maintainer (solo, DEC-003)
- **Tags:** process, telemetry, retros

## Context

Every retro entry carries a **Time invested** figure, split into human time and agent time. Agent
time comes from telemetry: `claude_code.active_time.total{type=cli}`, joined to a change through
`session.id` in `.telemetry/sessions.jsonl`. The same metric has a `type=user` dimension, and the
obvious reading is that it measures the other half.

It does not, in any usable way. Measured across the whole persisted export at the time of this
decision: `type=cli` carries 1766 datapoints totalling ~36.8 hours; `type=user` carries 32
datapoints totalling **0.01 hours** — for every session ever recorded. For the two most recent
changes it carried none at all.

`verify-telemetry.mjs` reports all five checks green throughout, and it is right to: the exporter is
enabled, the collector is up, `usage.jsonl` is being written, and sessions are being mapped. Capture
is working. The `user` dimension simply does not carry the thing its name suggests.

This has now been observed twice, and each time it was rediscovered from scratch:

- `product-corpus-v1` (#318) recorded it as a per-change gap: *"No `type=user` datapoints on
  `active_time.total` for this session: human time is not measurable from telemetry for this
  change,"* explicitly distinguishing it from a `manual` time source because capture itself was
  verified healthy.
- `aio-commands-honour-the-hold` (#323) hit the identical result, re-derived it by probing the
  metric's attribute shape, and reached the same conclusion.

The repository already states the correct answer in one place — the pull-request template's Time
invested section: *"Tracked deliberately: telemetry cannot see human time, so it is recorded here or
nowhere."* That sentence is right, and nothing else in the process agrees with it. The `collect-usage`
skill treats absent telemetry as a defect to surface, which is correct for the agent half and wrong
for the human half; the retro convention offers `manual` as a time source without distinguishing
"nobody measured this" from "this is unmeasurable by construction". So each retro re-litigates it.

The second occurrence is the graduation rule.

## Decision

We will treat **human time as a manually recorded figure, by design**, and stop reading it out of
telemetry.

Concretely:

- `claude_code.active_time.total{type=user}` is **not** a source of human time. No skill, command, or
  retro may report human time from it, and its absence is **not** a telemetry defect.
- The retro's human-time figure is supplied by the person, at sync, and is an estimate. An estimate
  labelled as one is worth more than a precise-looking zero.
- `collect-usage` keeps surfacing missing **agent** telemetry as a defect — that half is genuinely
  captured, and a gap there means something is broken. The two halves are not symmetric and are no
  longer treated as though they were.
- A retro whose human time is absent says *not recorded*, never *unmeasurable* and never `0`.

The reasoning: a metric that reports 0.01 hours of human activity across 36.8 hours of agent
activity is not a noisy measurement of the right quantity, it is a measurement of a different one.
Continuing to consult it produces a figure that is confidently wrong, and a zero in a
time-invested field reads as "this cost nobody anything" — the single most misleading thing a
programme built to measure AI-assisted delivery could record about itself.

## Consequences

- **Positive:** the human-time question is settled once. Retros stop re-deriving it, and stop
  spending a paragraph explaining why a green `verify-telemetry.mjs` sits next to an empty figure.
- **Positive:** the distinction between *unmeasured* and *unmeasurable* becomes explicit, so a real
  telemetry regression in the agent half stays legible instead of blending into a familiar gap.
- **Negative:** human time is now openly an estimate, recalled at sync — less precise than the agent
  figure beside it, and subject to memory. Accepted: an honest estimate beats a false zero.
- **Negative:** a retro can no longer be completed entirely without the person, since one field is
  theirs alone. This is a small, deliberate cost.
- **Neutral:** should the vendor ever populate `type=user` meaningfully, this ADR is the thing to
  revisit — the decision is about the metric as it behaves, not about the idea of measuring humans.

## Alternatives considered

- **Keep reading `type=user` and report whatever it holds** — rejected because it reports zero, and a
  zero in a time-invested field is worse than a blank. It is the status quo that produced two
  identical retro paragraphs.
- **Treat the empty `type=user` dimension as a telemetry defect and fix capture** — rejected because
  nothing on our side is broken: `verify-telemetry.mjs` passes every check, the collector receives
  what the vendor emits, and the dimension's population is not ours to change.
- **Drop human time from retros entirely** — rejected because it is the number the programme exists
  to compare against. Losing it would make "estimated time without AI assistance" meaningless.
- **Infer human time from wall-clock session span minus agent active time** — rejected because a
  session left open overnight would report a working day. An inferred figure with no bound is a
  fabrication with extra steps.

## References

- Retro entries: `product-corpus-v1` (#318) and `aio-commands-honour-the-hold` (#323) in
  [`docs/process/retro-log.md`](../process/retro-log.md) — the two occurrences.
- [`.claude/skills/collect-usage/SKILL.md`](../../.claude/skills/collect-usage/SKILL.md) — the
  agent-half rule this decision leaves intact.
- [`.github/pull_request_template.md`](../../.github/pull_request_template.md) — "telemetry cannot
  see human time, so it is recorded here or nowhere", the sentence this ADR promotes to a decision.
- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — the discipline of exercising a
  claim before recording it, applied here to a claim about a metric.
