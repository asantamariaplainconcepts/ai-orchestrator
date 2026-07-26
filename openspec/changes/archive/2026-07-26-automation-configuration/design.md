# Design — automation-configuration

## D1 — What "intersects" means (BR-003)

BR-003 rejects an Automation "whose trigger condition intersects an existing enabled
Automation's" but never says when two conditions intersect. Left undefined it would be decided
accidentally, by whatever the first implementation happened to do. The rule:

> Two **enabled** Automations in one Project overlap when **some Story could match both**.
>
> | A | B | Overlap |
> |---|---|---|
> | label `L`, state `S` | label `L`, state `S` | yes |
> | label `L`, state `S1` | label `L`, state `S2` (S1≠S2) | no |
> | label `L`, **any** state | label `L`, state `S` | **yes** — the unconstrained one subsumes it |
> | label `L1` | label `L2` (L1≠L2) | no |

Disabled Automations are ignored, per BR-003's own wording. Two consequences worth naming: an
Admin cannot add a narrow rule "underneath" a broad one, and the *order* Automations are created
in changes which save fails — the second one always loses. That asymmetry is the price of a rule
evaluated at write time rather than a priority scheme at read time, which DEC-033 chose
deliberately so runtime stays deterministic.

**Rejected:** treating a state-less trigger as "matches nothing extra" and allowing the pair. It
would let two rules match one event, which is exactly the non-determinism BR-003 exists to
prevent.

## D2 — Vendor states are strings, and stay strings

The Mirror stores whatever the vendor calls a state (`open`, `closed`) without normalising —
design D9 of the connector change, still open as OPN-003. The trigger's state field therefore
compares as an opaque string against `Story.State`. Normalising here would invent a vocabulary
the Backlog module does not have, and would have to be undone when OPN-003 closes.

## D3 — The action catalogue ships whole, and says what is inert

DEC-026 puts all four actions in MVP; only Implement→PR has an implementation this quarter.
Two honest options: ship one action and add three later, or ship four and mark three as not yet
executable. The corpus chose the full catalogue, so the interface does too — but an Automation
whose action cannot run is a trap, so the copy names it. Silence would be the dishonest choice.

## D4 — Overlap is checked in the handler, not by a database constraint

The rule is not expressible as a unique index: "no Story could match both" depends on the
subsumption case, not on equality. A partial index on `(ProjectId, TriggerLabel, TriggerState)`
would catch exact duplicates and silently miss the any-state case, which is the interesting one.

That leaves a race: two concurrent creates can both pass the check and both commit. Deliberately
accepted for now — Automations are configured by one Admin at human pace, and the alternative is
a serialisable transaction for a form submission. **Recorded rather than hidden**, and worth a
test the day it matters. This is the same shape as the Connector's `(ProjectId, VendorId)` race
in #7, which *was* index-guarded because there it was one query away.

## D5 — Where it lives

The `Projects` module, because BC-001 says Automations and their validation belong to Project
Configuration. Not a new module: the corpus already assigned the responsibility, and a module
drawn anywhere else would need a cross-boundary read on every save to enforce D1.
