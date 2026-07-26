# Design — run-now

## D1 — One creation path, two voices

`RunCreator` returns a discriminated outcome (`Dispatched`, `QueuedAtCap`, `AlreadyActive`,
`TwoPhaseRefused`) instead of throwing or logging: the event handler keeps translating
outcomes to its at-least-once semantics (silence where silence is correct), while the endpoint
translates the same outcomes to answers a human can act on. The rules live exactly once; only
the voice differs. This is the refactor BR-013 forces, and it is behaviour-preserving for
matching — the existing functional suite is the proof.

## D2 — The Automation is validated through the same catalog matching uses

`IAutomationCatalog.EnabledAutomations` already returns id + lane flag; the endpoint selects by
id from that list. A disabled or foreign Automation is simply absent — one source of truth for
"what can run", no new Contracts surface.

## D3 — BR-001 refusal is a 409, not a silent success

Matching ignores a second match because nobody asked a question. A Member clicking Run now
asked; answering "created" while creating nothing would be a lie. The pre-check gives the
common case a clean 409; the unique index still decides races, and the race loser returns the
same 409.

## D4 — No Failed-specific machinery

BR-004's re-run path needs nothing here: BR-001 blocks only *active* states, so the day a Run
can be `Failed`, this endpoint already re-runs it. Building a "re-run" affordance now would
invent semantics for a state that cannot occur.
