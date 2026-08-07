# ADR-0016: a fixture derives what the server derives

- **Status:** Accepted
- **Date:** 2026-08-07
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** frontend, testing, process

## Context

The mock in `src/frontend/shared/http/mock.ts` is how every UI state is reached by hand — the
repository's stated convention, and the reason `?podsDown`, `?cliMissing` and their siblings
exist. It is also, three times now, how the UI has been taught a shape the server does not send.

1. **`nextSequence`** (#144) was missing from the log fixture, and the note added when it was
   fixed says it plainly: *"the mock has to carry the field the contract carries, or it teaches
   the UI a shape the server does not send."*
2. **Run ids** were `crypto.randomUUID()` evaluated at module load, so a mock Run's URL changed
   on every reload. A mock Run could not be linked, bookmarked, or put in a bug report at all —
   the one convention the mock exists to serve, quietly broken.
3. **`complete`** was hardcoded `false` on the log fixture, for every Run including `Succeeded`
   ones. The server derives it from `RunStates.IsTerminal`; the fixture asserted the opposite of
   the contract for half the states.

The third one did real damage rather than being merely untidy. `run-previews` added a preview
that must disappear when a Run ends, and the browser exercise appeared to show it working. It
did not: the frame was rendering on a finished Run because `enabled` in react-query stops a
query from fetching without retracting what it already fetched. The bug was visible only once
the fixture stopped claiming every Run was live. **A lying fixture did not just fail to catch
the bug — it produced the evidence that the bug was absent.**

The common shape: a field the server *derives* was *hardcoded* in the fixture. Fields the server
merely holds (a title, a count) are fine as literals; fields it computes are a second
implementation of a rule, and second implementations drift.

## Decision

We will require that **a fixture derives any field the server derives**, from the same inputs,
in the mock.

Concretely:

- Where the server computes a field from other state — `complete` from the Run's state, a health
  chip from a timestamp, a derived count — the fixture computes it too, from the fixture's own
  data. `complete: false` is forbidden where the server would say `IsTerminal(state)`.
- Identifiers in fixtures are **stable across loads**, so every mock state is reachable by URL.
  A fixture id generated per module load is a defect.
- When a UI change is verified against the mock and the result is "it works", the fields the
  verification depended on are checked for this property first. A green mock proves nothing
  about a field the mock invented.

## Consequences

- **Positive:** the mock stops being able to manufacture false evidence, which is its worst
  failure mode — worse than being incomplete, because incompleteness is visible and a lie is
  not. Mock states become linkable, which is what the convention always promised.
- **Negative:** fixtures get slightly more logic, and logic in a fixture can itself be wrong. The
  mitigation is that derived fixture logic mirrors a named server rule, so the two can be
  compared by reading.
- **Neutral:** this does not require a sweep of existing fixtures. It governs what is added and
  what a verification is allowed to rest on. Where an old hardcoded derived field is found, it is
  fixed in whatever change trips over it — which is how all three of these surfaced.

## Alternatives considered

- **Drive the mock from the real server's contract types** — rejected as disproportionate: the
  mock's value is that it runs with no backend at all, and generating it would trade that for a
  build step.
- **Verify only against the real dev loop, never the mock** — rejected: the mock is what makes
  error and edge states reachable in seconds, and losing that would cost more than the lies did.
- **Leave it as advice in the retro log** — rejected for the reason ADR-0015 exists: the same
  advice was already written as a code comment beside the first occurrence, and two more
  occurrences followed it.

## References

- Related: ADR-0001 (verify claims by exercising them), ADR-0013 (an assertion must be able to
  fail), ADR-0015 (always-on output has one owner)
- `src/frontend/shared/http/mock.ts` — the `nextSequence` note is the first occurrence, recorded
  in place
- `openspec/changes/.../run-previews-over-published-ports/evidence.md` — the third occurrence and
  the bug it concealed
