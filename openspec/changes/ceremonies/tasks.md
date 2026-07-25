# Tasks — ceremonies

The ADRs come first. They are overdue by the second-occurrence rule, and the failure mode this
whole ceremony exists to prevent is exactly the one where they keep sliding to the end of a list.

## 1. The two overdue ADRs

- [x] 1.1 `docs/adr/` with `template.md` and a `README.md` stating immutability, numbering against
      `origin/main`, the second-occurrence rule, and the evidence-and-a-check bar.
- [x] 1.2 **[ADR-0001 — Verify claims by exercising them](../../../docs/adr/0001-verify-claims-by-exercising-them.md).**
      Cites all four instances (no launch profile → no HTTP endpoint; nothing applied migrations;
      health meant liveness not usability; the log watch keyed on the wrong identifier and
      returned silence). Consequences name where each class is now caught.
- [x] 1.3 **[ADR-0002 — A test tier must not hide a precondition the application lacks](../../../docs/adr/0002-test-tiers-must-not-provision-their-own-preconditions.md).**
      Cites the fixture's private migration and the all-sequential suite. Consequences: fixtures
      start the host instead of reproducing its effects; one test exercises concurrency; scope
      validation is unconditional.
- [x] 1.4 Both linked from their triggering retro entries. Only the `ADR:` field was filled — the
      field the format reserves for exactly this — and each edit says so inline. No reflection
      text was altered; the log stays append-only in substance.

## 2. Lifecycle labels

- [x] 2.1 Ten labels created once via `gh label create` (nine `status:*` + `lane:spec-less`), each
      with a description that states its role, colour-grouped by kind (gates green, review stages
      blue, blocked red). Recorded in `CONTRIBUTING.md` as a one-time manual bootstrap.
- [x] 2.2 Verify: `gh label list` reports all ten present.

## 3. Definition of Ready

- [x] 3.1 `docs/process/definition-of-ready.md` written as **citations, not copies** — every
      section binds to `RULE-001..007`, so a rule change propagates without editing this file.
      Includes required fields, slicing, traceability, sequencing, the open-decision gate (naming
      the three currently-open OPNs), the process gate, and the not-ready protocol.
- [x] 3.2 Verify: every `RULE-00N` reference resolves to a real rule in
      `08-backlog-shaping-rules.md`; `grill-to-ready` reads this file unmodified.

## 4. Contributor docs

- [x] 4.1 `CONTRIBUTING.md`: the loop diagram and step table, the nine states, the one-time label
      bootstrap, the solo path (DEC-016), the spec-less lane (DEC-025), the two load-bearing sync
      orderings, and **the no-branch-protection decision stated with its cost**. Gate mechanics
      are linked to the command files, never restated.
- [x] 4.2 `ONBOARDING.md` at **exactly 40 lines**. It arrived at 49 and was cut by moving facts to
      their canonical homes rather than raising the limit — which is the behaviour design D4
      predicted the limit would force, observed working.
- [x] 4.3 Verify: `wc -l ONBOARDING.md` = 40; zero setup commands duplicated outside `README.md`;
      every relative `.md` link in the new docs resolves to a real file (scripted sweep, clean).

## 5. Close-out

- [x] 5.1 `AGENTS.md`: the three "*lands in bootstrap Phase 3*" markers removed now that the
      documents exist; the Decisions row points at `docs/adr/`.
- [x] 5.2 Phase 1 issue/PR templates re-checked against this lifecycle: both issue forms still
      apply `status:backlog`, the PR template retains "Time invested" and references the
      spec-less lane. No edit needed.
- [x] 5.3 Verify sweep: link check clean, `openspec validate` green, CI green on the PR.

### Note on what this change did *not* automate

The labels are repository state, not repository content: nothing in git recreates them, and that
is deliberate (design D1). The trade-off is that a fresh clone of this repo into a new GitHub
repository has no lifecycle labels until someone runs the ten commands again — the commands will
say so loudly rather than inventing them. A provisioning script was rejected because the thing it
would automate happens once per repository, while the token it would need lives forever.
