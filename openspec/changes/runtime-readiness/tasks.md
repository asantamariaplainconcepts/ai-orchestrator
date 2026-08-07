## 1. The truth-telling core (design D3, D4)

- [ ] 1.1 `AddAgentRuntime` normalizes BOTH credential configs whitespace→null (Claude's loses
      its hard default's grip: empty means off); with null, the executor resolves nothing and
      the child process env carries no credential variable at all — a test asserts no
      empty-string `ANTHROPIC_API_KEY` is exported (it would shadow session auth).
- [ ] 1.2 Remedy sentences, spelled once per cause: executable missing (binary + PATH failure +
      pinned install command), secret missing (secret + store + where a value goes). The
      executor's failure path and the probe's verdicts read the same sentences.

## 2. The probe and the read (design D1, D2)

- [ ] 2.1 `AgentRuntimesProbe` + snapshot holder, sibling of the pods pair: per registered
      runtime, `<cli> --version` with a bounded timeout (exit code only) and credential
      `Resolve` against the real store; transitions logged, states not.
- [ ] 2.2 The environment read exposes runtime readiness beside pods (extend `GET /api/pods` or
      a sibling — decided at the seam, filtered like it), with cadence and last-checked.

## 3. The surface (design D3, aio-design)

- [ ] 3.1 Panel + environment chip render runtime states with copyable remedies; i18n copy as
      contract with the guides; routed through the design system.

## 4. Proof (design D5)

- [ ] 4.1 The machine matrix, real: CLI absent → panel says so; installed → flips ready without
      restart; secret configured-but-absent → named; switched off → a real Run completes on the
      machine's session. Observations recorded.
- [ ] 4.2 Full gates — build, tests, lint, spec validation — plus a whole-repository grep for
      the raw failure sentences this change retires (the #260 lesson: `git grep` from the root,
      no directory list).
