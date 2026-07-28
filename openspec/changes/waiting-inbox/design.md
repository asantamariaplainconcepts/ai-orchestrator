# Design: waiting-inbox

## D1 — One endpoint, list and count are the same truth

The badge shows the length of the same list the page renders. A separate count endpoint would be
a second query to keep in step with the first, and the number that matters is small by nature —
humans are the bottleneck the inbox exists to feed, and they do not scale to thousands of waits.

## D2 — "Waits on nobody" is derived, never stored

A Failed Run is inbox-relevant until a newer Run exists for the same Story. That is a query
(`NOT EXISTS` newer), not a flag: a dismissed-marker would go stale the moment someone re-runs
from the vendor side (BR-013 says Run now and re-labelling are both legitimate re-triggers, and
neither would remember to update a flag).

## D3 — The wait's age comes from the state that defines it

`AwaitingInput` has `WaitingSince` (#78's watermark), `Failed` has `EndedAt`, `AwaitingApproval`
has the phase-1 start. The entry exposes one `waitingSince` chosen per state rather than a new
column: the timestamps already exist because each state already needed them.

## D4 — Story titles are read through Contracts, per entry

The inbox joins Runs (its own) with story titles (Backlog's, via the existing `IStoryReader`)
and project names (Projects', via the list the portal already has). N lookups for N entries is
correct here precisely because N is human-scale (D1); a denormalised title on the Run would be
BR-008's mirror-of-a-mirror.
