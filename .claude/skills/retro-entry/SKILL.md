---
name: retro-entry
description: Append one entry to docs/process/retro-log.md after a change is synced. Use when closing the loop on a change (e.g. from /ds:refine).
---

Append one retro-log entry — one responsibility.

## Steps

1. **Gather.** Take the change name, date, and the human-vs-agent time/cost summary (from `collect-usage`, supplied by the caller — do not call it yourself). Ask the human for "what worked / what didn't / one change next time" if not provided.
   - Done when: change, date, time/cost, and the three reflection points are in hand.
2. **Append.** Add a new dated entry to `docs/process/retro-log.md` in the documented format; do not rewrite existing entries (append-only).
   - Done when: the new entry is the last block in the file and existing entries are untouched.
3. **Flag graduation.** If a reflection point is a structural workflow change, note it and suggest `write-adr`.
   - Done when: any structural finding links to an ADR to be written.

## Do not

- Call other skills — the caller supplies the usage summary.
- Edit or reorder prior entries.
