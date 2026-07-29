# Design: unique-automation-triggers

## D1 — The rule lives in the schema, because a handler cannot see a concurrent handler

An in-memory check reads, decides, and writes. Two of them interleaved both read a world without the
other's row and both decide yes. No amount of care in the handler fixes that; only the database can,
because only the database sees both writes.

This is BR-001's lesson applied where it was missed. Its index carries a comment recording that a
hand-written copy of the same rule drifted from it twice — and the fix there was to generate the index
filter from the same array the code checks, so the two cannot disagree.

**The NULL trap is the part to get right.** `TriggerState` is nullable, and Postgres treats NULLs as
distinct in a unique index: a naive index would permit two rows with the same label and no state,
which is precisely the duplicate this change exists to prevent. So the index normalises the state to
a non-NULL value in its own expression. `NULLS NOT DISTINCT` would also work and is Postgres 15+;
`COALESCE` works everywhere and states the intent in the index itself.

## D2 — Losing the race produces the refusal, not a 500

Once the database enforces the rule, one of two concurrent saves fails at the write. That failure is
not an internal error — it is the same conflict the guard reports, discovered later. So it maps to the
same `TriggerOverlaps` refusal.

Keeping the in-memory guard as well is deliberate, not redundant: it produces a message naming the
conflicting Automation, which a constraint violation cannot. The guard is for the caller and the
index is for the truth.

## D3 — Exact duplicates are refused whether or not they are enabled; subsumption is not

BR-003 is about *matching*, and a disabled Automation matches nothing — so subsumption correctly
ignores it. That stays.

But two rows with the same label and state are the same trigger whether either is enabled, and
allowing them means the conflict is discovered at enable time, by somebody who did not create it. Two
different questions, so two rules rather than one weakened: `Overlaps` for subsumption among enabled
siblings, and exact duplication independent of it. The index enforces the second, which is why it is
total rather than partial.

## D4 — One comparison, or the guard and the matcher disagree

GitHub's label names are case-insensitive; `AI:Implement` and `ai:implement` are one label there. The
guard used `Ordinal`, so it allowed both; matching used `Ordinal`, so a Story labelled `ai:implement`
never fired an Automation triggered on `AI:Implement`.

That second half is the dangerous one, because it fails silently: no error, no Run, and an Admin
looking at a correct-seeming configuration. Fixing only the guard would leave existing wrong-cased
rows permanently inert, so both move together — and the same comparison is used in both places, so
they cannot drift apart again.

## D5 — A rule's meaning changed, so it is recorded

BR-003 said "no overlapping triggers" without saying what makes two triggers the same. It is now
case-insensitive and it now covers disabled exact duplicates. Both are changes in meaning rather than
in enforcement, so they go into the rule's text and into a locked decision — the same reason DEC-052
was written when BR-010's mechanism changed.
