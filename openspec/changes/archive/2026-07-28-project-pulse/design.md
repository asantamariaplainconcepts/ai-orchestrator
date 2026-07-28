# Design: project-pulse

## D1 — Derived, never stored

The pulse is computed at read time from the Runs table plus the Contracts surfaces, exactly like
the inbox (#94). The mirror keeps no history, so the window is honest: it describes the runs that
exist, not a snapshot series. A snapshots table is its own future decision (out of scope in #108)
— building one now would freeze aggregation choices before anyone has read a pulse.

## D2 — Every figure derivable by hand

Each number must be reproducible from the run list a Member can already see, and each strip
figure links to the list it summarises. This is the acceptance bar the tests encode: given known
runs, the expected values are computed in the test by the same arithmetic a human would do. Cost
follows BR-011 exactly as project cost does today — sum the known, state how many were excluded;
unknown is never zero and never failure.

## D3 — The strip stays in the kit until the page migrates

The regrilled issue said "strip on shadcn", but adopt-foundations locked the stronger rule: a
screen is styled by exactly one system. The project page is still a kit screen, so the strip
reuses the kit vocabulary that page already renders (cards, stat strip, pills) and adds no new
kit classes. `dashboard-tabs` (#109) migrates the whole page — strip included — to the Platform
theme. The spec outranks the issue's styling note; restyling one strip twice is cheaper than
carving a two-system exception into the design contract the day after it merged.
