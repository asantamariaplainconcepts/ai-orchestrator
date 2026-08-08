## 1. Observed before claiming (design D4) — DONE, and it set the scope

- [x] 1.1 Find what each CLI actually needs inside a sandbox, by copying candidates in by hand
      until the agent answers. **Result:** opencode's whole session is
      `~/.local/share/opencode/auth.json` (950 bytes) — copied in alone, `opencode auth list`
      showed both configured providers and `opencode run -m github-copilot/claude-haiku-4.5`
      answered and then edited a file, on the developer's own Copilot seat with no API key.
      GitHub Copilot keeps files under `~/.config/github-copilot/`. **Claude Code on macOS keeps
      its credential in the system Keychain** — no `.credentials.json` exists, and copying
      `~/.claude` + `~/.claude.json` into a sandbox produced `Not logged in`. Copying
      `~/.config/opencode` wholesale would move 1.4 GB of caches for nothing.
- [x] 1.2 Decide what a non-carryable credential means. **Result:** not a blocker but a scope
      boundary — Claude Code on macOS is excluded and the readiness panel explains it (D6).
      Extracting the Keychain item was deliberately declined: it converts a protected token into
      a plain file inside a sandbox, which is worse than the API key it would replace.

## 2. The carriage (design D1, D2, D4)

- [x] 2.1 The sbx host copies the observed **credential files** into the sandbox at creation when
      its options say to — never the configuration tree. Copy, not bind: the copy dies with the
      sandbox and an agent cannot write into the machine's session state.
- [x] 2.2 One setting turns it off, declared by the habitat that wants it —
      `AppHostHabitats.DeclareDevLoop` sets it, `DeclareServerShape` never does. A unit test pins
      that a habitat which does not declare it gets nothing.
- [x] 2.3 The option's own configuration comment states the consequence where an operator reads
      it: sandboxed Runs act and bill as that seat, and a carried session is readable by whatever
      runs in the sandbox.

## 3. The runtime that cannot be carried (design D6)

- [ ] 3.1 Readiness reports a runtime whose credential cannot be copied as not ready in a
      session-carrying habitat, naming the reason (the Keychain) and the copyable remedy
      (`sbx secret set -g anthropic`). This is what turns the sbx spike's unverified box into an
      answer, and it is the half that survives even if carriage were dropped.
- [ ] 3.2 The panel renders it beside the existing runtime states, i18n as contract, routed
      through the design system (aio-design).

## 4. The transcript (design D3)

- [x] 4.1 `CredentialSource` gains its third value and the executor's one header reads correctly
      with it — a clause, not a second line (ADR-0015). A test pins all three sources.

## 5. Proof

- [ ] 5.1 Unit and functional coverage: the habitat rule, the off switch, the minimum-set copy,
      and the not-carryable remedy. Fakes that can fail.
- [ ] 5.2 **The definitive Run** (#288 AC7, design D5, ADR-0014). Point a project's Connector at
      `asantamariaplainconcepts/ai-orchestrator-rehearsal`, give it an opencode Automation, and
      dispatch a Run end to end through the running orchestrator with sandbox mode and carriage
      on. Record verbatim: the agent authenticated on the carried seat with no secret stored; its
      output streamed to the Run page while it executed (UC-027); it published its own branch and
      pull request (DEC-062); the transcript named the carried session; no sandbox survived.
      Anything that did not work is recorded too.
- [ ] 5.3 Full gates — build, tests, CSharpier, ESLint, tsc, spec validation, design-system
      validator — and confirm a habitat naming nothing behaves exactly as before this change.
