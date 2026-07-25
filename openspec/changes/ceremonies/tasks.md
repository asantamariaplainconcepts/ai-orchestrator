# Tasks — ceremonies

The ADRs come first. They are overdue by the second-occurrence rule, and the failure mode this
whole ceremony exists to prevent is exactly the one where they keep sliding to the end of a list.

## 1. The two overdue ADRs

- [ ] 1.1 `docs/adr/` with the template (`docs/adr/template.md`) and a short README stating the
      immutability rule and how numbers are allocated.
- [ ] 1.2 **ADR-0001 — Verify claims by exercising them.** Cite all four instances (missing
      launch profile → no HTTP endpoint; nothing applied migrations; health meant liveness not
      usability; the E2E log watch returned silence because it keyed on the wrong identifier).
      Consequences must name the check that would have caught each class.
- [ ] 1.3 **ADR-0002 — A test tier that provisions its own preconditions hides their absence.**
      Cite the fixture's private migration and the all-sequential suite that could not observe a
      concurrency bug. Consequence: fixtures use the application's own startup path, and at least
      one test exercises concurrency.
- [ ] 1.4 Link both from their triggering retro entries (append a link line only — the entries
      themselves are append-only and must not be rewritten).

## 2. Lifecycle labels

- [ ] 2.1 Create the nine `status:*` labels plus `lane:spec-less` with `gh label create`, once,
      with confirmation. Record in `CONTRIBUTING.md` that this is a one-time manual bootstrap.
- [ ] 2.2 Verify: `gh label list` shows all ten; `/aio:status` on a labelled issue reports its
      position instead of failing.

## 3. Definition of Ready

- [ ] 3.1 `docs/process/definition-of-ready.md`, bound to `RULE-001..007` by citation, with the
      required fields, slicing, traceability, the open-decision gate, and the process gate.
- [ ] 3.2 Verify: `grill-to-ready` reads it without needing an edit; every RULE reference
      resolves to a real rule in `08-backlog-shaping-rules.md`.

## 4. Contributor docs

- [ ] 4.1 `CONTRIBUTING.md`: the loop, the nine states, two gates, one-issue-one-branch-one-PR,
      merge = archive = sync = retro, the solo path (DEC-016), the spec-less lane (DEC-025), and
      the one-time label bootstrap. Links to the `/aio:*` files for gate mechanics.
- [ ] 4.2 `ONBOARDING.md`, **≤ 40 lines**, linking to canonical homes; points agents at
      `AGENTS.md` and setup at `README.md`.
- [ ] 4.3 Verify: `wc -l ONBOARDING.md` ≤ 40; no setup commands duplicated outside `README.md`;
      every internal link resolves.

## 5. Close-out

- [ ] 5.1 `AGENTS.md`: drop the "*lands in bootstrap Phase 3*" markers now that the documents
      exist; confirm the lookup table points at real files.
- [ ] 5.2 Confirm the Phase 1 issue/PR templates still match this lifecycle (they name the
      statuses and keep "Time invested").
- [ ] 5.3 Verify sweep: all internal links resolve; `openspec validate` green; CI green on the PR.
