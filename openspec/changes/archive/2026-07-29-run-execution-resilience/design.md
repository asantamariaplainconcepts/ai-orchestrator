# Design: run-execution-resilience

## D1 — Three numbers, one contract, and a comment that makes drift visible

The phase timeout lives in three places: a default in code, a per-Automation value an Admin sets,
and a replica timeout in Terraform. Two of them contradicted each other, and nothing in either file
mentioned the other.

So the timeout gains a **ceiling of 60 minutes**, refused at save, and Terraform's replica timeout is
set to the ceiling plus a drain margin. The ceiling is what makes the relationship expressible at
all: without an upper bound there is no number Terraform could be set to that is provably enough,
and "Admin-configurable" quietly meant "Admin-configurable up to whatever infrastructure happens to
allow".

The margin exists because a worker needs time after a phase ends — writing the outcome, flushing the
log, acknowledging the message. A replica timeout equal to the phase timeout would kill a worker
mid-write, which is the failure this change is about.

Each of the three sites carries a comment naming the other two. That is deliberate and it is the only
mechanism available: no test can span a C# constant, a Terraform value and a business rule, so the
guard is that a reader changing one is told where the others are.

## D2 — A worker that cannot finish a phase does not start one

Raising the replica timeout narrows the window but does not close it: a worker can always claim work
with less budget left than the phase needs, and then die mid-phase.

So before claiming, the worker compares its remaining replica budget against one full phase timeout
and stops claiming when it is short, exiting cleanly. The queue still holds the message, KEDA sees a
non-empty queue and starts a fresh job with a full budget.

This is the one part of the change that prevents the failure rather than recovering from it. The
sweeper (#140) exists because prevention cannot be complete — a container can be evicted at any
moment — but a worker knowingly starting work it cannot finish is a choice, not an accident.

## D3 — Five minutes of grace, because the cost is asymmetric

#144 asked for the phase timeout plus five minutes; the implementation shipped with two. Both are
guesses, and the asymmetry decides: too short and the sweeper races a worker that was about to
finish, ending a Run that had produced real work. Too long and a hostage Story stays hostage a few
minutes more. The second is recoverable and the first is not, so the default moves to 300s.

It stays configurable, so a deployment that measures something different can say so.

## D4 — The cursor is bounded by what is running, and one Run's pushes do not interleave

`RunLogNotifier` keeps the last sequence it sent per Run and never removes an entry, so the map grows
for the life of the process — slowly, invisibly, and forever. A Run that reached a terminal state
will never produce another line, so its entry is evicted when its terminal notification is handled.

The same map is read and written from concurrent notification handlers, so two notifications for one
Run can both read the same cursor and push overlapping frames. Serialising per Run fixes it; a global
lock would also fix it and would make every Run wait behind every other, which is the opposite of
what a live window is for.

## D5 — Subscribe, then read

A page opening mid-Run reads the log and then subscribes to the hub. Lines committed between those
two steps belong to neither: the read missed them and the subscription started after them. Today
they surface on the 30-second reconciliation poll, which is outside the ≤5s the product promises.

Reversing the order closes it. Subscribing first means those lines arrive as pushes, and the read
that follows overlaps them — an overlap the client already handles by sequence, because it must
handle a redelivered push anyway. An overlap is cheap; a gap is a lie about the lag budget.

## D6 — A decision that disagrees with the code is worse than no decision

DEC-050 says "2s flush"; the code flushes at 500ms. Anyone reasoning about latency from the decision
reasons wrongly, and anyone changing the code has no idea a decision constrains them.

The decision is corrected to the code, not the reverse: 500ms is what shipped, what #106 measured,
and what the ≤5s budget was verified against. And `ARCHITECTURE.md`'s crash story stops saying a lost
message is "recovered by *Run now*" — it is not, because BR-001 holds the Story and *Run now* answers
`AlreadyActive`. It names the sweeper and the human re-trigger that follows it.
