## 1. The truth-telling core (design D3, D4)

- [x] 1.1 `AddAgentRuntime` normalizes BOTH credential configs whitespace→null (Claude's loses
      its hard default's grip: empty means off); with null, the executor resolves nothing and
      the child process env carries no credential variable at all — a test asserts no
      empty-string `ANTHROPIC_API_KEY` is exported (it would shadow session auth).
- [x] 1.2 Remedy sentences, spelled once per cause: executable missing (binary + PATH failure +
      pinned install command), secret missing (secret + store + where a value goes). The
      executor's failure path and the probe's verdicts read the same sentences.

## 2. The probe and the read (design D1, D2)

- [x] 2.1 `AgentRuntimesProbe` + snapshot holder, sibling of the pods pair: per registered
      runtime, `<cli> --version` with a bounded timeout (exit code only) and credential
      `Resolve` against the real store; transitions logged, states not.
- [x] 2.2 The environment read exposes runtime readiness beside pods (extend `GET /api/pods` or
      a sibling — decided at the seam, filtered like it), with cadence and last-checked.

## 3. The surface (design D3, aio-design)

- [x] 3.1 Panel + environment chip render runtime states with copyable remedies; i18n copy as
      contract with the guides; routed through the design system.

## 4. Proof (design D5)

- [x] 4.1 The machine matrix, partially real, honestly bounded. Proven for real: every UI
      state in the running mock browser (CLI missing with copyable install, secret missing
      naming the store, ready with "this machine's session", both themes, the chip's dot); the
      E2E suite booted the real AppHost with the probe registered (44/44). NOT exercised live:
      the owner declined the real Run (it posts to their GitHub repository), and the dev-loop
      panel check hit a pre-existing machine fault — the aio-postgres-data volume holds a
      cluster initialised with another session's password (the exact hazard the E2E fixture
      documents), unrelated to this change. The unit tests carry the switch-off and env-guard
      contracts (RuntimeReadiness_Should_Constraint, 5 facts).
- [x] 4.2 Full gates — build, tests, lint, spec validation — plus a whole-repository grep for
      the raw failure sentences this change retires (the #260 lesson: `git grep` from the root,
      no directory list).
