# Design: defaults-full-catalogue

## D1 — Seed propose on the grill's output, not on its own name

Two ways to connect them: teach propose to listen on `ready-for-proposal`, or seed the grill with
`readyLabel: ai:propose`. The first keeps one truth — the grill's ready label is what #79
documented, everywhere — while the second would mean the seeded grill quietly disagrees with the
documented default, and anyone debugging would have to know which layer won.

It also reads as the sentence the workflow actually is: when a Story is ready for proposal,
propose it.

## D2 — The chain stops before implement, deliberately

Propose applies no label, so nothing triggers implement. That is the design: a proposal exists to
be read, and the expensive irreversible step keeps a human at its gate (DEC-040's spirit even
where its mechanism does not apply). Automating grill→propose→implement end to end would remove
the only checkpoint the chain has.

## D3 — The upgrade path is free, and worth a test

No migration and no version marker: BR-003 already makes a second press additive. The value is
that a project seeded last week gets this week's catalogue by pressing the same button, so the
test asserts precisely that — seed four, extend the set, seed again, expect two created and four
already handled.
