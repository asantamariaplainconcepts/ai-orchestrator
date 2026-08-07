## 1. The seam, with today's behaviour behind it (design D1)

- [x] 1.1 Extract `IAgentProcessHost` from `HeadlessProcess`: the same signature (command,
      arguments, workspace, environment, timeout, cancellation, `OnOutput`) returning the same
      `Outcome`. `LocalAgentProcessHost` holds today's `Process.Start` body verbatim — BR-005's
      kill-on-timeout and the streaming callback move unchanged, not rewritten.
- [x] 1.2 `ClaudeCodeHeadlessRuntime` and `OpenCodeRuntime` take the host as a dependency and
      call it instead of the static. No flag, parser or usage-extraction edit — a diff touching
      those is a signal the extraction went wrong.
- [x] 1.3 Register the local host as the default in composition, and prove the seam is a no-op:
      the existing runtime and dispatch test suites pass untouched.

## 2. The credential contract (design D2, spec: injected or passed, never absent)

- [x] 2.1 The host declares whether it supplies credentials out of band. Where it does, the
      runtimes omit the credential environment variables entirely; where it does not, values
      travel exactly as today. Unit-test both shapes, including that no empty-string credential
      variable is exported (the #279 hazard — an empty `ANTHROPIC_API_KEY` shadows session auth).
- [x] 2.2 An injecting host verifies its credential is present before the agent starts and
      refuses in the #279 remedy voice (store + command), with a unit test that a
      claimed-injecting host with nothing stored refuses rather than starting an
      unauthenticated agent.
- [x] 2.3 The Run's transcript names the credential source either way — the honesty the pod
      substrate already owes about the host's sessions. (Implementation note: it is the
      **executor** that says it, not the runtime. Announcing from the runtime polluted the
      agent's own output stream and broke `EveryLine_Should_ReachTheWatcherAsItArrives` — the
      right signal. The fact travels on `AgentRuntimeSelection.CredentialSource`, which is the
      seam the Runs module can see, filled in composition from the chosen host.)

## 3. The sbx driver (design D3, D4, D7)

- [x] 3.1 `SbxAgentProcessHost`: create a per-Run sandbox, run the runtime's command inside it,
      stream stdout through `OnOutput`, return the same `Outcome`; dispose in a `finally` that
      survives cancellation, with `--force` (spike H4). Constructed with its own options only —
      no `IConfiguration` (D7), so no connection string can reach it by any path.
- [x] 3.2 Map the workspace and verify it: the driver reports the in-sandbox path and asserts
      the directory is visible there before running, refusing by name when it is not (D4).
- [x] 3.3 Pass an explicit memory limit rather than accepting the 50%-of-host default (H5), and
      pin the sbx version in configuration.
- [x] 3.4 Translate the driver's own failures into the remedy voice: daemon unreachable,
      identity absent, network policy uninitialized — each naming its fix, each distinguished
      (their remedies differ, exactly as missing-CLI and missing-secret are distinguished).

## 4. Composition and refusals (design D5)

- [x] 4.1 Presence-driven registration: a named sandbox launcher selects the sbx host, nothing
      named keeps the local host. Unit-test both branches and the untouched default.
- [x] 4.2 Refuse a habitat naming both a pod image and a sandbox launcher, naming both keys and
      what to remove — the `DispatchComposition` queue/consumer refusal is the model. Test the
      refusal message, not just the throw.

## 5. Readiness tells the truth (design D6, agent-execution delta)

- [x] 5.1 `AgentRuntimesProbe` asks the sandbox host its own preconditions when Runs execute in
      sandboxes, and reports each runtime's CLI readiness from where that CLI will run — never
      from this process's PATH. An unreachable sandbox host reads as unreachable, not as ready.
      **Design amendment (D6):** two cadences, not one. The host's preconditions are probed every
      30s because they move minute to minute; the CLI-in-the-template verdict is refreshed every
      15 minutes, because creating a sandbox costs ~4.5s (spike H5) and that answer belongs to
      the image, which changes on deploy. Probing it every cycle would spend a sixth of the
      machine's time re-learning something that cannot have changed. `GET /api/pods` now carries
      `runtimes.host` (where, ready, remedy).
- [x] 5.2 The environment panel renders the sandbox host's preconditions beside the existing
      runtime and pod states, with copyable remedies and i18n copy as contract; routed through
      the design system (aio-design). (The host reads **above** the runtime rows, not beside
      them: the rows describe that machine, so reading them without knowing which one would be
      reading them about the wrong place — and while the host cannot answer, its remedy is the
      one to apply first. The chip carries the same precedence. Mock gained `?sandboxed` and
      `?sandboxDown`, and was corrected to stop rendering an impossible machine: in sandbox mode
      the pod host answers "not hosted here", because D5 refuses a habitat naming both.
      Validator green; both themes seen.)

## 6. Proof (design: CI cannot exercise this)

- [x] 6.1 The unit and functional suites: selection branches, both credential shapes, the
      refusal sentences, the streaming contract. No stub that would pass whatever we wrote —
      the fake must be able to fail. (`AgentSandbox_Should_Constraint`, 14 facts; the
      can-it-fail property verified by mutation — see evidence.md.)
- [x] 6.2 The manual exercise on the dev machine — done for the boundary, **bounded honestly**
      for the pipeline, recorded as evidence the way the spike
      recorded its own (ADR-0001): a real Run of each runtime through a real sandbox, streaming
      output observed, the diff produced, the sandbox gone afterwards, and the credential
      absent from inside. Commands and observed output written down, including anything that
      did **not** work. (Done for the **process host**: `RealSbxSandbox_Should_Constraint`
      drives the shipped driver against the real sbx, 4/4 — workspace at the same path,
      `GITHUB_TOKEN` empty inside, exit 7 with its stderr, no sandbox left behind; it also found
      a real bug, an unawaited task that swallowed the absent-binary remedy. **Still to do:** a
      full Run through the orchestrator, which needs Postgres, a queue, a project and a
      credential. **Then done in the dev loop too:** `aspire run … --Parameters:sandbox=true`
      brought the real Server up in sandbox mode, and the D6 probe caught a bug nothing else
      could have — every sandbox was created from sbx's generic `shell` template, which carries
      no agent CLI, so every Run would have failed with a missing binary. Fixed by letting the
      runtime's command select the image that contains it. **Not verified, owner-accepted
      2026-08-07:** a Run dispatched end to end, because the only configured project targets the
      owner's real repository and DEC-062 has the agent publish its own work. Evidence names
      exactly what stays unproven.)
- [x] 6.3 Full gates — build, tests, CSharpier, ESLint, tsc, spec validation — plus a check that
      no habitat's configuration changed by default: every existing test passes without naming
      a launcher anywhere. (build 0 errors; CSharpier clean; DispatchTests 55/55, ArchTests
      32/32, Projects 40/40, Backlog 40/40 — and the pre-existing suites passed with no
      assertion edited, which is what proves the extraction was a no-op.)
