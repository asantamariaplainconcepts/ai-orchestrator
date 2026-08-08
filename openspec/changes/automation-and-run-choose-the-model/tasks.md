## 0. The rehearsal's preconditions (ADR-0017)

- [ ] 0.1 This change's proof needs no real Run against a remote repository: every acceptance
      criterion is reachable through the running dev loop and the gated real-sbx exercise. **Named
      anyway, per ADR-0017:** the enumeration path needs a machine that runs agents (the dev loop
      with `Parameters:sandbox=true`, already the default) and, to see the seat-dependent half, the
      carried session from #288 — both exist and resolve on the authoring machine today. No
      human-only credential step is required. If that turns out to be wrong, it is recorded here
      before implementation rather than discovered at proof time.

## 1. Observe before claiming (ADR-0001)

- [x] 1.1 Re-run the two measurements the design rests on and record them verbatim: `opencode
      models` on the host and inside a sandbox created from the opencode template with the session
      carried. The design records 41 and 495; confirm the gap still exists and that the
      `github-copilot/*` entries track the carried seat. If the numbers have converged, D2's
      argument weakens and the design says so before any code depends on it. **Confirmed
      2026-08-08:** host 41 (24 `github-copilot/*`), sandbox with the carried session 495 (21
      `github-copilot/*`). The gap holds and D2 stands.
- [x] 1.2 Confirm how each CLI rejects an unknown model — exit code, stream, and whether the model
      name appears in the message — because D5's failure sentence is built from it. **Result, and
      it falsified the design:** `claude --model definitely-not-a-model` exits 1 with
      `404 ... "model: definitely-not-a-model"`, naming it; `opencode run -m definitely/not-a-model`
      exits 1 with `UnknownError`, *"Unexpected server error. Check server logs for details."* and
      an opaque `ref` — **the model is named nowhere.** D5 had generalised from Claude alone and is
      corrected: the product composes the reason itself and keeps the CLI's text as detail. Passing
      opencode's message through would report a typo'd model as a server fault.

## 2. The model travels (design D4, D5)

- [x] 2.1 `Automation` and `Run` each gain a nullable model; two additive migrations. A functional
      test pins that existing rows keep behaving exactly as before.
- [x] 2.2 `RunExecutor` resolves `run.Model ?? automation.Model ?? deployment`, at execution time,
      beside the runtime chain it already resolves. A test pins the order and pins that a change to
      the deployment default reaches a Run without any Automation being edited.
- [x] 2.3 `OpenCodeRuntime` takes the resolved model instead of the singleton option;
      `ClaudeCodeHeadlessRuntime` passes `--model`, which it never has. A runtime with no resolved
      model launches byte-identically to today — pinned, because that is the whole no-op guarantee.
- [x] 2.4 A rejected model fails the Run naming the model and the runtime, in
      `AgentRuntimeRemedies` where the sentences already live (#279 D3) — not a raw vendor error.
      Said on **every** failure that resolved a model, not only suspected ones: 1.2 showed the
      product cannot tell a rejection from an outage, so recognising one before speaking would
      leave opencode silent. Found while wiring it: the resolved model has to be written **after**
      `Entry(run).ReloadAsync`, which the terminal path runs to let a cancellation win the race —
      anything set in memory during `Invoke` is discarded there. It travels out on `Outcome`.

## 3. Where the choices come from (design D1, D2, D3, D6)

- [x] 3.1 A runtime declares whether it can enumerate its models. opencode can (`opencode models`);
      Claude Code cannot and reads `Agents:<Runtime>:Models` from configuration. Neither list lives
      in code, and a unit test pins that a runtime which can be asked is never handed a copied list.
- [x] 3.2 Enumeration goes through `IAgentProcessHost`, beside `CliAnswers`, so it is answered where
      agents run. In the local host that is this process; in the sbx host, inside a sandbox.
- [x] 3.3 The cache is keyed on everything the answer depends on — including the carried session,
      per D3. **The test was re-aimed while writing it:** "two habitats must not read each other's
      list" would have been vacuous, because each habitat builds its own host and therefore its own
      cache — it could never have failed. The real risk is within ONE host: a developer
      re-authenticates and keeps being served the seat they left. That is what is asserted, with a
      stand-in that counts how many times the CLI was asked. Verified able to fail: keying on the
      command alone turns it red (ADR-0013). "Could not ask" is deliberately not cached — caching
      silence would keep every chooser empty for a whole probe interval after the machine returned.
- [x] 3.4 "Could not ask" is a distinct result from "no models", all the way to the API.

## 4. The surfaces (design D6)

- [ ] 4.1 The Automation form gains the model beside the runtime, re-asking when the runtime
      changes; three chooser states rendered; i18n as contract; routed through `aio-design`.
- [ ] 4.2 Every human launch dialog that already offers the runtime offers the model, pre-selected
      on the resolution, recorded on the Run only.
- [ ] 4.3 The Run's usage shows the model beside tokens and cost.

## 5. Proof

- [ ] 5.1 Unit and functional coverage: the resolution order, the no-op guarantee, the two
      discovery mechanisms, the cache key, the three chooser states, the rejected-model reason.
      Fakes that can fail — and for the cache, a fake that would pass if the key were wrong.
- [ ] 5.2 Exercised end to end on the running dev loop in sandbox mode: an Automation given a
      `github-copilot/*` model that only the carried seat reaches, a Run launched with a different
      model overriding it, and the Run's usage naming what actually ran. Recorded verbatim,
      including anything that did not work.
- [ ] 5.3 Full gates — build, tests, CSharpier, ESLint, Prettier, tsc, `openspec validate --strict`,
      design-system validator — and a deployment that sets nothing behaves exactly as before.
