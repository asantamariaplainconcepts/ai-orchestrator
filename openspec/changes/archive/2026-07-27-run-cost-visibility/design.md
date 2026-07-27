# Design — run-cost-visibility

## D1 — Null is unknown; zero is zero; they must never look alike

BR-011's "unknown" was a database nullability decision in #18 and becomes a *display* decision
here. With free models (DEC-044) `0.00` is a legitimate reported cost, so rendering unknown as
zero would assert that a Run cost nothing when in truth nobody said. Unknown renders as the
design system's empty value plus the word; zero renders as `$0.00`. The distinction is asserted
in tests, because it is exactly the kind that erodes into "just show 0" later.

## D2 — The project total counts only what is known, and says what it skipped

Summing nulls as zero would understate the number the experiment is judged by, quietly and
forever. The total is over reported Runs, accompanied by the count of unknown ones. A reader
can then tell "cheap" from "unmeasured", which a bare total cannot express.

## D3 — Computed in SQL, not in the browser

The project total is an aggregate over Runs, which the client would otherwise assemble by
pulling every Run. One small read, one `SUM`, one `COUNT` — and it stays correct when a project
has a thousand Runs and the table is paged (which it will be, eventually).

## D4 — Money formatting lives with the other formatting

`Intl.NumberFormat` with an explicit currency and fixed fraction digits, beside the existing
relative-time helper. Tokens use the kit's tabular-figure treatment so a column aligns.
