# Tasks: product-corpus-v1

## 1. The corpus and its study land

- [ ] 1.1 Add `docs/product/v1/` (README, 00-brief, 01-actors, 02-glossary, 03-contexts,
      04-capabilities, 05-business-rules, 06-journeys, 07-roadmap, 08-shaping-rules) — the
      drafts already in this branch, re-read once against the acceptance criteria of #318
- [ ] 1.2 Add `docs/product/studies/2026-08-11-orca.md` (sources UC-030..032)
- [ ] 1.3 Verify stable-ID continuity: every ACT/BC/UC/BR/RULE id in `mvp/` resolves to the
      same concept in `v1/`, with only UC-028 naming its correction (spec: product-corpus)

## 2. The decision is recorded

- [ ] 2.1 Allocate the next DEC number against `origin/main` (expected DEC-066) and append the
      entry to `docs/product/mvp/10-locked-mvp-decisions.md`: revises DEC-001 (open-source,
      dual-habitat identity), adopts `docs/product/v1/` as living corpus, names the
      UC-024→UC-028 correction and BR-014's habitat sentence as a DEC-065-sourced wording
      clarification; DEC-001's own text untouched
- [ ] 2.2 Write the ADR in `docs/adr/` (next free number) carrying the rationale and the
      rejected alternatives from `design.md`, naming its evidence (the grill audit: the
      DEC-001/DEC-049 contradiction, the UC-024 collision, the missing glossary terms) and its
      check (task 4.1's sweep)

## 3. Live documents point at v1

- [ ] 3.1 Repoint `README.md` ("Where things live" table), `AGENTS.md`, `ARCHITECTURE.md`,
      `ONBOARDING.md`, `CONTRIBUTING.md` product-corpus links to `docs/product/v1/`
- [ ] 3.2 Repoint `docs/process/definition-of-ready.md` rule citations to
      `docs/product/v1/08-backlog-shaping-rules.md` (+ actors/UC/BR links), per the
      definition-of-ready delta
- [ ] 3.3 Sweep the rest of `docs/process/` for product-corpus links and repoint them
- [ ] 3.4 Rewrite the project-context and rules text in `openspec/config.yaml` to the truth:
      open-source dual-habitat identity, per-Run sandboxes with Postgres-outbox dispatch (not
      queue/KEDA), corpus paths at `docs/product/v1/`, UC/BR/DEC ranges current
- [ ] 3.5 Add the one supersession note atop `docs/product/mvp/00-product-brief.md`; no other
      `mvp/` file changes

## 4. Verification

- [ ] 4.1 Run the cutover sweep: `grep -rn "product/mvp"` excluding `docs/product/mvp/`,
      `docs/adr/`, `BOOTSTRAP*`, `docs/process/retro-log.md`, `openspec/changes/archive/` —
      every remaining match is one the design names (decision-log links from v1, the
      run-orchestration historical note); fix or justify each survivor
- [ ] 4.2 Confirm history is untouched: `git diff --stat origin/main` shows no `docs/adr/`,
      `BOOTSTRAP*`, retro-log or archive files
- [ ] 4.3 `openspec validate --change product-corpus-v1` passes; Prettier over the touched
      markdown/yaml (`pnpm` lint-staged equivalent) is clean; no code gates apply (docs-only
      diff — assert it stays that way)
