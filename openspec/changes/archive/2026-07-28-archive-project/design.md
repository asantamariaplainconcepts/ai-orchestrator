# Design: archive-project

## D1 — Projects owns the state; the others ask, and never keep a copy

Two modules act on it — Backlog's poller decides whether to synchronise, Runs' matching and
manual dispatch decide whether to start — and neither owns the project. So it joins the Projects
Contracts surface beside `IAutomationCatalog`, and both callers read it per decision.

Per decision, not cached, for the same reason `ISecretResolver` resolves per read: a project is
archived while the application runs, and a poller holding a snapshot would keep polling something
an Admin just retired — a failure that reads as "archiving does not work" rather than "the
process is stale".

A copy of the flag in the Backlog schema would be the same mistake the mirror already refuses to
make about Stories (BR-008): one owner, everyone else asks.

## D2 — Reading stays open; starting does not

The line is not "archived projects are inaccessible". It is that an archived project **begins no
new work**: no poll, no match, no manual Run. Everything already recorded — Runs, their logs,
their cost, the pulse — stays readable at the URL it always had.

This is what makes archiving safe to choose. If retiring a project also hid what its agents did,
nobody would archive anything, and the list would stay full for exactly the reason the feature
exists to fix.

## D3 — A timestamp, not a boolean

`ArchivedAt` rather than `IsArchived`: the list wants to say *when*, restoring is clearing it, and
a boolean would need a second column the moment anybody asked how long ago. One nullable
timestamp answers both questions and cannot disagree with itself.

## D4 — Typing the name is the guard, and it is the only one

No rule refuses the archive — not "only if it has no Runs", which would make every project that
was ever used unarchivable, and not an approval gate. Archiving is reversible; the protection
needed is against doing it by accident, and typing the project's name is exactly proportionate to
that. Restoring needs no ceremony at all, because nothing is lost by restoring something.
