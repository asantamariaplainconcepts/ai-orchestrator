---
name: collect-usage
description: Summarize a change's Claude Code usage telemetry (human-vs-agent time, cost, tokens) from the persisted OTel data. Use when reporting time invested for a change, e.g. from /ds:refine.
---

Produce one per-change usage summary from persisted telemetry — one responsibility, read-only. The durable source is the OTel Collector's export (see `.telemetry/`) — dashboards are disposable viewers.

## Metrics used (reference)

- `claude_code.active_time.total` — split by `type`: `user` (human keyboard/reading) vs `cli` (agent processing).
- `claude_code.cost.usage` — USD; `claude_code.token.usage` — tokens by `type`.
- Attribution: **join on `session.id`**. `.telemetry/sessions.jsonl` maps each session id to its `change` (written by the `map-session-change.mjs` SessionStart hook). The `change=<name>` resource attribute exists only where the legacy env tag happened to apply — treat it as a supplement, never the only filter.

## Steps

1. **Resolve the change's sessions.** Read `.telemetry/sessions.jsonl` and collect the `session_id`s whose `change` equals the change name.
   - Done when: the set of session ids for the change is in hand (possibly empty).
2. **Locate the data.** Read the Collector's persisted export (`.telemetry/usage.jsonl`); select metric datapoints whose `session.id` attribute is in that set, plus any carrying the `change=<name>` resource attribute.
   - Done when: the telemetry for the change's sessions is in hand.
3. **Aggregate.** Sum `active_time.total{type=user}` (human) and `{type=cli}` (agent) into hours; sum cost and tokens. Values are raw deltas — sum them directly.
   - Done when: human hours, agent hours, cost, and tokens are computed for the change.
4. **Emit.** Return a compact summary: human time, agent time, cost (USD), tokens — ready for the retro-log entry. If sessions are mapped but their records predate the export's history (data lost to truncation before `append: true` landed), report that explicitly.
   - Done when: the summary is handed back to the caller (e.g. `/ds:refine`).

## Do not

- Read from the Aspire dashboard as the source of truth (it loses data on restart).
- Fabricate figures if the telemetry is missing — report it missing so the retro entry can note it.
- Include per-person identity in the summary beyond what the retro needs.
