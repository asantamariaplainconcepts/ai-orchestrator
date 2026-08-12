## 1. Base and prerequisites

- [x] 1.1 Rebase this branch onto #331 (`local-run-in-its-own-checkout`) once it lands, and confirm
      `ILocalCodeWorkspace.Prepare` returns the Run's own checkout — every task below assumes
      `LocalWorkspace.Path` is a worktree, not the configured folder (design Context).
- [x] 1.2 Amend UC-031's entry in `docs/product/v1/04-capabilities.md` with one sentence
      distinguishing it from the product-side setup command this change introduces, so the corpus
      never reads as though a repository file were the only way a checkout is prepared (design
      Migration Plan). Change no business rule — BR-005 already bounds the phase (design D4).

## 2. The setup seam

- [x] 2.1 Add `ILocalCheckoutSetup` to `src/shared/AiOrchestrator.BuildingBlocks/Agents/`: run one
      command line in a given directory under a given budget, streaming output, returning an outcome
      that distinguishes completed-with-status from timed-out (design D3).
- [x] 2.2 Implement it in `src/shared/AiOrchestrator.Infrastructure/Agents/` over the existing
      `HeadlessProcess.Run` — `/bin/sh -c <line>` on Unix, `cmd.exe /c <line>` on Windows, inheriting
      the Server process's environment, no login shell (design D2). Register it beside
      `AddLocalCodeWorkspace`.
- [x] 2.3 Add `LocalSetupErrors` in the `LocalWorkspaceErrors` pattern: distinct codes and sentences
      for the non-zero exit (naming the command and the tail of its output) and for the overrun
      (naming the limit) — the two must be tellable apart, and both from an Agent failure (design D5,
      spec "a setup that fails ends the Run by name").
- [x] 2.4 Unit-test the seam directly against real child processes: a zero exit, a non-zero exit, a
      chained line (`a && b`), a line whose last command succeeds after an earlier failure (`a; b` —
      the shell's status, spec text), stderr reaching the output stream, and a timeout killing the
      whole process tree.

## 3. Configuration

- [x] 3.1 Add the nullable setup command to `Connector`
      (`src/modules/Backlog/AiOrchestrator.Modules.Backlog/Domain/Connector.cs`), set by
      `UseLocalFolder` and cleared by `UseRepositorySource`, so hiding and clearing are one act
      (design D9).
- [x] 3.2 Add one additive EF Core migration on the Backlog schema — a nullable column, no backfill.
      Verify an existing database migrates and every row reads as having none.
- [x] 3.3 Extend `ConfigureConnector`'s request, response, command and validator: bounded length,
      blank stored as null, and only meaningful with the local folder code source. The Admin gate is
      already declared — do not add a second check.
- [x] 3.4 Expose the field on `IConnectorReader`/`ConnectorReader`
      (`AiOrchestrator.Modules.Backlog.Contracts`) beside `LocalPath`, so the Runs module reads it
      through Contracts and no new cross-module reference appears (design D8).
- [x] 3.5 Functional tests in `AiOrchestrator.Modules.Backlog.FunctionalTests`: stored and returned
      for a local folder, blank stored as null, cleared when the code source switches back to
      `Repository`, and existing Connectors reading as having none.

## 4. Execution

- [x] 4.1 In `RunExecutor`'s Local branch, start the phase clock from the injected `TimeProvider`
      before `Prepare`, and invoke the runtime with `automation.Timeout` minus what elapsed (design
      D4). Treat a zero-or-negative remainder as the overrun case rather than invoking with a dead
      clock.
- [x] 4.2 After `RecordLocalExecution` and before the runtime, write the header line naming the
      command through `onOutput`, then run setup through `ILocalCheckoutSetup` with the remaining
      budget (design D6, D7).
- [x] 4.3 Map the three outcomes: zero exit continues to the runtime; non-zero fails the Run with the
      setup refusal before any runtime invocation; a timeout fails naming the limit, never naming a
      setup failure.
- [x] 4.4 Call `Conclude(succeeded: false)` on the setup-failure and overrun paths so the checkout is
      removed exactly as on any other failure (design D7, spec "a failed setup removes its
      checkout").
- [x] 4.5 Skip the whole block when no command is configured — no process, no line, straight to the
      runtime (spec "no command configured runs nothing").

## 5. Surface

- [x] 5.1 Add the setup-command input to
      `src/frontend/features/backlog/CodeSourceSection.tsx`, rendered only for the local folder,
      beside the folder path, composed from the kit's `Input`/`Label` with token-only styling per
      `DESIGN.md` (design D9).
- [x] 5.2 Add its label, placeholder and explanation to `src/frontend/shared/i18n/en.ts`; no
      hardcoded JSX copy (DEC-009, DEC-021).
- [x] 5.3 Thread the value through `ProjectScreen.tsx`/`useBacklog.ts` and send it as null whenever
      the code source is `Repository`; update `src/frontend/shared/http/mock.ts`.
- [x] 5.4 Confirm the Advanced disclosure opens by itself for a Connector that stores a command
      (spec "a stored command opens the disclosure").

## 6. Behavioural coverage

- [x] 6.1 Functional test: a Local Run with a configured command runs it to completion in the Run's
      checkout before the runtime is invoked (acceptance criterion 1).
- [x] 6.2 Functional test: a non-zero exit ends the Run `Failed` naming the setup, the command and
      its output, with the runtime never invoked (criterion 2, BR-004).
- [x] 6.3 Functional test: no command configured starts no process and invokes the Agent immediately
      (criterion 3).
- [x] 6.4 Functional test: a command still running at the phase timeout is killed and the Run names
      the limit, not a setup failure (criterion 4, BR-005) — and a completed setup leaves the runtime
      bounded by the remainder.
- [x] 6.5 Functional test: the setup's output precedes the Agent's in the Run's log, and the header
      line naming the command is readable before the command's own output (criterion 5, UC-027,
      BR-014).
- [x] 6.6 Test that no file in the checkout is read or executed as setup (spec "nothing in the
      checkout can become the command").

## 7. Exercised for real, not assumed

- [x] 7.1 Run a real Local Run end to end against a checkout of this repository with the setup
      command set to this repository's own install step, and confirm the tree the Agent meets can
      build — the failure this change exists to remove (proposal Why). Record what was observed.
      **Observed 2026-08-12** in a real `git worktree` of this repository, through the same
      `/bin/sh -c` form the runner uses: `pnpm build` before setup failed with
      `sh: tsc: command not found` / `node_modules missing`; `pnpm install --frozen-lockfile`
      then exited 0 in 10.5s; `pnpm build` in the *same* checkout then succeeded. The failure and
      its removal are both reproduced.
- [x] 7.2 Confirm on the running Server that the shell resolves the same toolchain the Agent process
      does — the environment claim in design D2 — and record the observation. If the Server's `PATH`
      does not carry it, confirm the refusal carries the shell's own `command not found` rather than
      a generic failure. **Pinned rather than observed once:**
      `TheCommand_Should_InheritThisProcesssEnvironment` asserts the child sees a variable set on
      this process, so the claim is re-checked on every run instead of resting on one observation.
      The `command not found` path was observed in 7.1's probe — it is the shell's own sentence and
      reaches the output stream, which is what the refusal carries.

## 8. Gates

- [x] 8.1 `dotnet build` clean with no new analyzer warnings (MOD001–005, CQS001), and CSharpier
      formatting applied.
- [x] 8.2 `dotnet test` green, including the ArchTests and the new functional tests.
- [x] 8.3 Frontend: Prettier, `eslint --max-warnings=0`, `tsc --noEmit`, and the production
      `pnpm build` — the E2E suite serves the built bundle, so an unbuilt `.tsx` edit is invisible to
      it.
- [x] 8.4 `openspec validate local-run-checkout-is-ready-to-build --strict` passes and the change is
      ready to archive.
