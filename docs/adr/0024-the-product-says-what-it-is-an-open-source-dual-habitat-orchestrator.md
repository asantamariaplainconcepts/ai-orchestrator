# ADR-0024: the product says what it is — an open-source, dual-habitat orchestrator

- **Status:** Accepted
- **Date:** 2026-08-11
- **Deciders:** repo owner (DEC-003)
- **Tags:** product, docs, governance

## Context

The product corpus at `docs/product/mvp/` was written in the bootstrap charter for a product
described as an "internal web app" (DEC-001) whose Agents ran as queue-scaled container jobs.
Both halves of that description have since been revised by decisions that never reached the
identity documents:

- **DEC-049** (2026-07-28) licensed the repository MIT and made self-hostability a product
  goal — "anyone can run this", with infrastructure choices evaluated against "can a stranger
  with Docker still run it?". DEC-001's "internal" was never revised.
- **DEC-013's supersession** (#296, 2026-08-09) replaced the queue/KEDA substrate with per-Run
  microVM sandboxes dispatched through the Postgres outbox.
- **DEC-052, DEC-055, DEC-061, DEC-065** built a habitat distinction (deployment vs self-host)
  with different lawful affordances per habitat — attach sessions, local code sources
  (BR-016), a machine sandboxes surface (UC-029) — serving a persona (the self-hosting
  developer) the brief's "Plain Concepts delivery teams (internal tool)" never mentions.

An audit during the grill of #318 also found mechanical rot: UC-024 assigned twice (resolved
mid-grill by #316: the file-changes review became UC-028), stale cross-references (Member
"cancel" citing UC-019), load-bearing terms with no glossary entry (Habitat, Sandbox,
Conversation, Pass, Inbox, Transcript), a "post-MVP" section listing shipped capabilities, and
an `openspec/config.yaml` project context still describing the retired queue/KEDA substrate —
the text every future proposal is grounded in.

Patching the corpus in place had been the practice, and it produced this drift: each decision
amended its own corner while the identity kept asserting a product that no longer exists.
Evidence for the specific claims above: the DEC-001/DEC-049 texts in
`docs/product/mvp/10-locked-mvp-decisions.md`, the grep audits recorded in
`openspec/changes/product-corpus-v1/` (58 files referencing `product/mvp`; three specs, two at
requirement level), and #316/#318.

## Decision

We will say what the product is, once, in a corpus rewritten from zero — and record the
identity change as a formal decision rather than a documentation edit.

- **DEC-066** (appended to the decision log) revises DEC-001: the product is an **open-source
  web application** connecting backlogs to AI agents in per-Run microVM sandboxes, **one
  product in two habitats** — a governed deployment on metered infrastructure, and self-host
  on a machine its operator owns — with Plain Concepts delivery teams as the first governed
  team, not the definition.
- **`docs/product/v1/` is the living corpus**: identity, glossary (including the habitat
  vocabulary), capabilities (UC-001..029 carried; UC-030..032 recorded as intended and
  ungrilled), business rules, journeys, roadmap and shaping rules. Live documents point here.
- **`docs/product/mvp/` is legible history**: byte-identical except one supersession note atop
  its brief. The decision log stays there, append-only — one log, one numbering.
- **Stable IDs survive the version**: every ACT/BC/UC/BR/RULE id resolves to the same concept
  in both corpora; the one correction (UC-024 → UC-028 for the file-changes review) is #316's,
  carried and named, not remade.
- **Authority split, stated in the v1 README**: `openspec/specs/` is the authority on current
  behaviour; the corpus on what the product is and must become; where they disagree, the spec
  wins and the corpus has a bug.

## Consequences

- **Positive:** grills trace against a corpus whose identity matches its decisions; the
  self-host persona and habitat vocabulary exist where work is shaped; future proposals read a
  truthful project context; the anti-references section (not a cockpit, not a PR dashboard,
  not CI) fences scope cheaply.
- **Negative:** two corpora coexist and readers can land in the old one — mitigated by the
  supersession note and the live-doc cutover; external links into `mvp/` paths keep working
  but show history, which is the intended trade.
- **Neutral:** the decision log living under `mvp/` while the corpus lives under `v1/` is
  mildly surprising; acceptable until the log outgrows its home, and moving it would break
  ~40 historical references today. UC-030..032 still owe their own grills (#318 out-of-scope).

## Alternatives considered

- **Keep patching `mvp/` in place** — rejected: three patches deep it already contradicted
  itself; identity drift is exactly what in-place patching produced.
- **Move the decision log into v1** — rejected: breaks historical links for zero product
  value; append-only logs earn their trust by not moving.
- **A second decision log in v1** — rejected: two logs, one numbering — a collision factory.
- **Two DECs (identity, then corpus adoption)** — rejected: the corpus was rewritten *because*
  the identity changed; splitting them makes the second meaningless alone.
- **Renumbering the grill to UC-028 (this change's first draft)** — rejected: #316 landed
  main's resolution first (file-changes → UC-028); a second renumber of the same id pair
  within a day maximises confusion.

## References

- Issue [#318](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/318) · change `product-corpus-v1` (`openspec/changes/product-corpus-v1/`)
- DEC-066, revising DEC-001 — `docs/product/mvp/10-locked-mvp-decisions.md`
- DEC-049 (open source, self-hostability), DEC-013's supersession (#296), DEC-052/055/061/065 (habitats)
- #316 (UC-024 → UC-028), ADR-0021 (self-host sessions), ADR-0012 (the seed never carries this corpus)
- Studies: `docs/product/studies/2026-08-11-orca.md`, `docs/product/studies/2026-08-03-pr-dashboard.md`
- Check: the cutover sweep in `openspec/changes/product-corpus-v1/tasks.md` §4
