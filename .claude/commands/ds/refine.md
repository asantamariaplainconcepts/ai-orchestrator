---
name: "DS: Refine"
description: Append a follow-up retro-log entry after a change has merged (post-merge findings or a backfill)
category: Workflow
tags: [workflow, ds, retro, refine]
---

Append a retro entry for a change that has **already merged**. The normal retro is captured
*inside* `/ds:sync`, on the branch before the merge. Use `/ds:refine` only for what that
pre-merge retro couldn't see:

- a **post-merge finding** — something that surfaced during or after the merge (a deploy
  failure, a check breaking on `main`, a regression noticed later); or
- a **backfill** — a retro for a change synced before the retro step existed.

**Input**: the change name (or issue number). If omitted, ask.

**Steps**

1. Invoke **`collect-usage`** for the change (join on `session.id` via
   `.telemetry/sessions.jsonl`).
2. **Propose** what to record and have the human confirm or edit — for a backfill, draft the
   three reflection points from the change's history and telemetry; for a post-merge finding,
   draft the entry from what actually happened (the failure, its cause, the fix). Lead with a
   concrete draft, not a cold question.
3. Invoke **`retro-entry`** to append a new dated entry to `docs/process/retro-log.md`.
4. If a reflection point is structural, invoke **`write-adr`** and link it from the entry —
   second occurrence is the graduation rule.

**Guardrails**
- Append-only: if `/ds:sync` already wrote a pre-merge entry for this change, add a new dated
  follow-up entry — never rewrite the original.
- Time invested comes from telemetry via `collect-usage`; if telemetry is missing, the entry
  says so (manual).
- This lands as its own commit (the change is already merged) and gates nothing.
