# Roadmap

What we intend, reconciled with every open issue at the time of writing (2026-08-11). Not a
backlog: each entry still owes a grill ([RULE-001](08-backlog-shaping-rules.md)..007) before
it becomes one. Ordered by intent, not by date.

## Now — in flight or ready to grill

The open issues, each already traced:

| Issue | What it closes | Traces to |
|---|---|---|
| #306 | A Member answers a waiting Run from the portal instead of the vendor | UC-026, ADR-0008's named follow-up |
| #308 | A human types into a self-host Run agent | UC-029, DEC-065 — blocked on BR-005's stated rule |
| #313 | A self-host conversation runs in a sandbox, not in the portal's process | DEC-061/DEC-065 |
| #314 | A ceiling on how many sandboxes one machine holds | UC-029's surface, BR-002's sibling for machines |
| #305 | A grill actually asks its questions instead of guessing | UC-028 |
| #307 | Telemetry capture at session start instead of at report time | UC-020, BR-011 |
| #223 | Close OPN-006: self-host backlog read with the host's own credentials | [RULE-006](08-backlog-shaping-rules.md) blocker for self-host backlog work |
| #183 | Data Protection key ring not wrapped with Key Vault | BR-010, deployment habitat |
| #243 / #245 | Runs on Copilot models; per-Automation model choice from the runtime's catalogue | business goal 3, behind #244 |

## Next — the intended capabilities (grill in this order)

1. **UC-030 — the Run shows what the agent is doing right now.** The status channel is the
   runtime's own hook mechanism reporting into the Run — never log inference. Grill must
   settle: which states persist vs stream, hooks as part of the runtime seam (DEC-012), the
   no-hooks fallback. Source: [Orca study §1](../studies/2026-08-11-orca.md).
2. **UC-031 — the repository declares how its sandbox is prepared.** Setup file in the code
   source, trust-gated per version by an Admin, bounds-checked, refusal before the agent
   starts. Grill must settle: where trust state lives, what "changed" means, refusal wording.
   Source: [Orca study §2](../studies/2026-08-11-orca.md). Unblocks real implement-and-test
   stories — arguably the highest product value on this page.

## Later — differentiators with an open decision in front

3. **UC-032 — tournament runs.** One dispatch, sibling Runs across runtimes/models, PRs and
   cost side by side. **Blocked by a decision, on purpose**: BR-001 must learn what a sibling
   group is (exception vs new aggregate) — RULE-006 forbids proposing before that closes.
   Also sequenced behind #244/#245. Source: [Orca study §3](../studies/2026-08-11-orca.md).
   The payoff: business goal 3 stops being "we support several runtimes" and becomes "we can
   tell you which one earns its cost on your backlog, with receipts".

## Conditional — behind a decision we will not slip past

4. **Notifications.** DEC-037 says the website is the only surface, and it still holds.
   The revisit trigger is evidence, not appetite: once UC-030 makes waits visible, measure
   approval latency — if Runs sit blocked for hours because nobody looked, that is the case
   for a push channel, and the shape is already studied (web push for the Inbox,
   [pr-dashboard study](../studies/2026-08-03-pr-dashboard.md); Orca's pairing design noted
   and declined there — we have a server).

## Standing debts this corpus acknowledges

- The **UC-024 collision** the old corpus carried is resolved here (grill → UC-028); issues
  citing the old number predate v1 and are read accordingly.
- **OPN-006** stays open (#223) — the only open product decision.
- The old corpus (`../mvp/`) is history, not authority; the decision log there remains the
  live, append-only record for DECs.
