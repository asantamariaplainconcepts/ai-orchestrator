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

- [x] 3.1 Readiness reports a runtime whose credential cannot be copied as not ready in a
      session-carrying habitat, naming the reason (the Keychain) and the copyable remedy
      (`sbx secret set -g anthropic`). This is what turns the sbx spike's unverified box into an
      answer, and it is the half that survives even if carriage were dropped.
      **Observed on the running server** (`aspire run`, sandbox mode, carriage on):
      `ClaudeCodeHeadless` reports `sessionUnavailableReason` naming the keychain and
      `sessionUnavailableRemedy` = `sbx secret set -g anthropic-api-key` — the name the runtime
      already expects, not an invented one; `OpenCode` reports neither; the host reads
      "a per-Run sandbox on this machine".
- [x] 3.2 The panel renders it beside the existing runtime states, i18n as contract, routed
      through the design system (aio-design).

## 4. The transcript (design D3)

- [x] 4.1 `CredentialSource` gains its third value and the executor's one header reads correctly
      with it — a clause, not a second line (ADR-0015). A test pins all three sources.

## 5. Proof

- [x] 5.1 Unit and functional coverage: the habitat rule, the off switch, the minimum-set copy,
      and the not-carryable remedy. Fakes that can fail.
- [ ] 5.2 **The definitive Run** (#288 AC7, design D5, ADR-0014) — **NOT VERIFIED, and blocked on
      one human step.** The rehearsal target exists
      (`asantamariaplainconcepts/ai-orchestrator-rehearsal`), the dev loop runs in sandbox mode
      with carriage on, and the sbx host holds the `github` credential the agent publishes with.
      What is missing is the **Connector's own token**: the server clones and reads Stories with a
      GitHub PAT stored under a name derived from the project and the vendor, so a fresh project
      has no secret to name, and the only way to create one is to paste a token into the Connector
      form. An agent may not do that. **To finish this: configure the Demo project's Connector
      against `asantamariaplainconcepts/ai-orchestrator-rehearsal` with a PAT, give it an opencode
      Automation, and dispatch one Run.** ADR-0014 asked for a rehearsal target and got one; this
      is the second half it did not anticipate — a rehearsal *credential*.

      **What was exercised instead**, against the real sbx CLI through the shipped host
      (`RealSbxSandbox_Should_Constraint`, `AIO_SBX_EXERCISE=1`, 7/7 green):
      - a carried session authenticates the agent as the machine owner — `opencode auth list`
        inside the sandbox lists **GitHub Copilot (oauth)** and **plainconcepts (oauth)**, with no
        API key stored and nothing passed in the environment;
      - the same command in a habitat that declared nothing finds no session, so the assertion
        above can fail;
      - `sbx ls` is identical before and after, so nothing outlived the Run;
      - the workspace crosses the boundary at the same absolute path and `GITHUB_TOKEN` does not.

      **What that leaves unproven:** the orchestrator's own path around the agent — a Run reaching
      a terminal state, output streaming to the Run page (UC-027), the agent publishing its branch
      and pull request (DEC-062), and the transcript's third credential source read in situ. Each
      is covered by a test; none has been seen end to end on a real repository.

      **Found while doing this, and fixed:** `sbx cp` preserves the host's uid and mode, so the
      0600 credential owned by uid 501 landed inside the sandbox still 0600 and still owned by
      501 — unreadable to the sandbox user, which cannot chown it either. The CLI then reported
      "0 credentials" from a file demonstrably present. Carriage is now staged through a 0644 copy
      in a 0700 directory and re-created by the sandbox user. **The by-hand observation in 1.1
      would not have caught this**: copying by hand as the machine owner is a different act from
      the server copying on the owner's behalf.
- [x] 5.3 Full gates — build, tests, CSharpier, ESLint, tsc, spec validation, design-system
      validator — and confirm a habitat naming nothing behaves exactly as before this change.
      **Green:** 553 tests across 8 assemblies (incl. 45 E2E and 7 real-sbx), CSharpier 379 files,
      ESLint, Prettier, tsc, `openspec validate --strict`, design-system validator all three
      stages. The unchanged-habitat claim rests on `NoLauncherNamed_Should_KeepTheAgentAChildOf‑
      ThisProcess`, `CarriageOff_Should_BeTheDefaultForEveryHabitat`, and the E2E fixture, which
      declines every dev-loop convenience and still passes.
