## 1. Observe before claiming (design D4)

- [ ] 1.1 On this machine, find what a signed-in Claude Code actually needs inside a sandbox:
      copy candidate paths into a scratch sandbox by hand and run `claude -p` until it answers.
      Record the set that works and the ones that were not enough — #246's own note (opencode's
      credentials live in `~/.local/share/opencode/auth.json`, NOT `~/.config/opencode`) is the
      reminder that guessing this costs a day.
- [ ] 1.2 If no copyable set works — a session bound to the machine in a way a copy cannot carry
      — stop and say so. The change then becomes "the panel explains that sandboxed Claude Code
      needs an API key", which is worth shipping and is not this.

## 2. The carriage (design D1, D2)

- [ ] 2.1 The sbx host copies the observed set into the sandbox at creation, when its options say
      to. Copy, never bind: the sandbox's own copy dies with it, and an agent cannot write into
      the machine's session state.
- [ ] 2.2 One setting turns it off, declared by the habitat that wants it —
      `AppHostHabitats.DeclareDevLoop` sets it, `DeclareServerShape` never does. A unit test pins
      that a habitat which does not declare it gets nothing.
- [ ] 2.3 The option's own configuration comment states the consequence where an operator will
      read it: sandboxed Runs act and bill as that session, and a carried session is readable by
      whatever runs in the sandbox.

## 3. The transcript (design D3)

- [ ] 3.1 `CredentialSource` gains its third value and the executor's one header reads correctly
      with it — a clause, not a second line (ADR-0015). A test pins all three sources.

## 4. Proof

- [ ] 4.1 Unit and functional coverage: the habitat rule, the off switch, and that a carried
      session does not reach a non-declaring habitat. Fakes that can fail.
- [ ] 4.2 **The definitive Run** (#288 AC7, design D5, ADR-0014). Point a project's Connector at
      `asantamariaplainconcepts/ai-orchestrator-rehearsal`, give it an Automation, and dispatch a
      Run end to end through the running orchestrator with sandbox mode and carriage on. Observe,
      and record verbatim: the agent authenticated with no `anthropic` secret; its output streamed
      to the Run page while it executed (UC-027); it published its own branch and pull request
      (DEC-062); the transcript named the owner's session; no sandbox survived. Anything that did
      not work is recorded too.
- [ ] 4.3 Full gates — build, tests, CSharpier, ESLint, tsc, spec validation — and confirm that a
      habitat naming nothing behaves exactly as before this change.
