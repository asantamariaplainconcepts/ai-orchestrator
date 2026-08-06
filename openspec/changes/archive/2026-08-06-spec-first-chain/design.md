## Context

#269 shipped the spec-first tier with every `outputLabels` empty and recorded the wiring as a
follow-up methodology decision. The canvas, the graph derivation, the HandOn step and the setup
action all already do their part — an edge exists wherever a label equals a trigger — so the whole
change is deciding the values and writing them where the catalogue keeps content: the manifest.
Backend conventions are not engaged (no endpoint, no schema); the design decisions were made at
grill by the product authority (DEC-003) and are recorded in #273.

## Goals / Non-Goals

**Goals:** the consented setup produces a drawn, gated chain; the decision lives as catalogue
content; #262's broken-hand-off marker is demonstrable again.

**Non-Goals:** re-wiring existing projects (the canvas's connect controls already serve them);
branching; changing which steps are gated; touching dispatch, HandOn or the canvas.

## Decisions

### D1 — Full chain, gates as the human waits

`grill → propose → implement → sync`, with `requiresApproval` kept on propose, implement and sync.
A gate stops the Run in the Inbox after planning (BR-007), so each hand-off still waits for a
person — the same HITL shape as the `/aio:*` loop this tier encodes.

*Rejected — breaks at the HITL points (no labels between propose/implement/sync).* A break makes a
person apply the next label by hand, which is the state #273 exists to end; the gate carries the
same authority with one press in the Inbox instead of a label edit on the vendor.

*Rejected — wiring only `grill → propose`.* Leaves the loop unwired exactly where it does its work.

### D2 — refine and status stay standalone

`refine` appends a post-merge retro entry; `status` reports. Wiring either after sync would run it
on every pass of the chain, turning an occasional and a query into ceremony.

### D3 — Content, not code; new setups only

The values live in `manifest.json` `automation` blocks — the wiring-as-content requirement already
says the product hardcodes no methodology, and this keeps it true. Setup already skips existing
Automations, which gives the existing-projects guarantee for free.

## Risks / Trade-offs

- **An autonomous chain can run four agent passes from one label.** → Every executing step is
  gated: nothing executes without a person approving its plan in the Inbox. The chain automates
  hand-offs, not approvals.
- **The mock and `planHandoff.ts` carry prose claiming no edges exist.** → Both updated in the same
  commit as the manifest, so the mock cannot disagree with the catalogue (the failure mode its own
  comment warns about).
- **BR-001 note:** the chain is linear; no step carries two labels, so the serialization warning
  stays dormant and correct.

## Migration Plan

None. Catalogue content read at setup time; rollback is reverting the manifest values.

## Open Questions

None.
