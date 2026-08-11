## 1. The namespace predicate, shared before it is relied on

- [x] 1.1 Extract the claimed-prefix predicate (`aio-probe-`, `aio-run-`) out of
  `SbxSandboxLifecycle.ReapAbandoned` into one internal definition in
  `src/shared/AiOrchestrator.Infrastructure/Agents/Sbx`, and have the reaper call it — behaviour
  unchanged, one caller.
- [x] 1.2 Replace the reaper's whitespace-column parse with a typed read of `sbx ls --json`
  (`{sandboxes:[{name,id,agent,status,workspaces}]}`, verified against the real CLI 2026-08-11),
  returning name and status; keep the non-zero-exit early return.
- [x] 1.3 Unit-test the predicate against the real names it must judge: `aio-run-*` and `aio-probe-*` in,
  `opencode-ds-connect` and `aio-carry-*` out.
- [x] 1.4 Confirm `ReapAbandoned` still reaps as before after the parse swap — cover it with the existing
  sandbox tests rather than a new path.

## 2. The seam: enumeration and name-keyed entry

- [x] 2.1 Add enumeration and a name-keyed `Open(string sandbox, int columns, int rows)` to
  `IRunTerminalHost` in `src/shared/AiOrchestrator.BuildingBlocks/Agents/IRunTerminalHost.cs`, leaving
  `Open(Guid, int, int)` untouched. Model an entry as name, status and optional Run id.
- [x] 2.2 Answer both new members with "nothing" in `UnhostedRunTerminalHost`, so the deployed habitat
  resolves without hosting anything.
- [x] 2.3 Add enumeration to `RunSandboxHost` so a sandbox name can be attributed back to the Run holding
  it; keep it in memory and unpersisted.
- [x] 2.4 Implement both members in `SbxRunTerminalHost`: list via `sbx ls --json` filtered by the shared
  predicate and annotated from `RunSandboxHost`; on entry, re-resolve the caller's name against a fresh
  listing and refuse when absent, so no caller-supplied string reaches `sbx exec`.
- [x] 2.5 Rewrite `SbxRunTerminalHost`'s class doc comment: the old invariant ("no path by which a
  caller-supplied name reaches `sbx exec`") no longer holds, and the namespace predicate is what replaces
  it. A comment asserting a dead invariant is worse than none.

## 3. The attach record, which must survive having no Run

- [x] 3.1 Add a durable attach record to the Runs schema — who, when, sandbox name, nullable Run id — with
  its EF Core configuration and migration.
- [x] 3.2 Extend `IRunAttachRecorder` with a sandbox-keyed `Attached`, writing the durable row always and
  the existing `RunLogChunk` line only when a Run id is known, so #304's criterion 6 does not regress.
- [x] 3.3 Unit-test both paths: an attach with a Run writes the row and the Run log line; an attach with no
  Run writes the row and no log line, and does not throw looking for one.

## 4. The read and the hub

- [x] 4.1 Add a `ListMachineSandboxes` query under
  `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Observation/UseCases`, dispatched through the CQS
  pipeline with `[Requires]` on `run.attach`, returning the annotated listing; answer the habitat's "none
  hosted" before any permission question.
- [x] 4.2 Add the habitat-scoped permission read — `run.attach` on at least one project — for sandboxes
  that resolve to no Run, and keep the Run's project role check where one does resolve.
- [x] 4.3 Expose it at `GET /api/runs/sandboxes` via `IUseCase.AddRoutes`, with `ApiResults.Problem` for
  refusals.
- [x] 4.4 Add `OpenSandbox(string sandbox, int columns, int rows)` to `RunTerminalHub`, reusing the byte
  pump, the one-terminal-per-connection rule and the disposal path; key the second-viewer guard by sandbox
  name as the existing one is keyed by Run id.
- [x] 4.5 Order the hub's refusals so the habitat answers first, then permission, then an unresolvable
  name — and make out-of-namespace and does-not-exist return one identical refusal, so it cannot enumerate
  the machine.
- [x] 4.6 Update `ProjectRoles_Should_Constraint`'s `EnforcedOutsideThePipeline` doc comment to name
  `RunTerminalHub.OpenSandbox` as the second enforcer of `run.attach`.

## 5. The surface

- [ ] 5.1 Read `docs/design-system/` and `DESIGN.md` before writing any UI, and compose from the existing
  kit rather than new primitives.
- [ ] 5.2 Add the sandboxes screen under `src/frontend/features/`: the listing with name, status and Run
  where present, its query hook and typed API call.
- [ ] 5.3 Reuse `features/runs/RunTerminal.tsx`'s xterm transport for the sandbox-keyed terminal instead of
  adding a second terminal component.
- [ ] 5.4 State in the surface that entering a stopped sandbox starts it — `sbx exec` on a stopped sandbox
  starts it, verified on the real CLI — and render the sandbox-is-gone ending rather than a dead terminal.
- [ ] 5.5 Render each refusal as its own sentence: no terminal hosted here, no permission, not this
  machine's to enter.
- [ ] 5.6 Add every string to the typed i18n catalogue; hardcoded JSX copy fails CI (DEC-009, DEC-021).
- [ ] 5.7 Add the route and its navigation entry, hidden where the habitat hosts no terminal.

## 6. Tests that exercise the claims

- [x] 6.1 Extend `RunTerminalRefusal_Should_Constraint` (Runs functional tests) with the sandboxes surface:
  refused without `run.attach`, permitted for a Member holding it, out-of-namespace and unknown names
  refused identically, and a deployed habitat answering "none hosted" without evaluating permissions.
- [x] 6.2 Add a functional test that the listing excludes a sandbox outside the claimed namespace.
- [x] 6.3 Extend `RealSbxTerminal_Should_Constraint` (DispatchTests, real `sbx`) to open a shell on a
  listed sandbox by name and prove `Ctrl-C` arrives as a signal — the claim in criterion 3 gets exercised
  rather than asserted.
- [x] 6.4 Add a real-`sbx` test that entering a stopped sandbox starts it and returns a working shell,
  pinning the behaviour D5 rests on, and leaving the machine as it found it.
- [x] 6.5 Add a test that a disposed sandbox ends its open terminal and reports the sandbox as gone.

## 7. Docs

- [ ] 7.1 Add **UC-029 — Member opens a terminal on this machine's sandboxes** to
  `docs/product/mvp/04-mvp-use-cases.md`, matching the existing entry format and leaving UC-024's
  duplication to #315 / PR #316 rather than colliding with it.

## 8. Verification — the CI-equivalent gates

- [ ] 8.1 `dotnet csharpier check .` and `dotnet build` clean.
- [ ] 8.2 `dotnet test` for the Runs module's unit and functional projects, plus `ArchTests`.
- [ ] 8.3 Frontend: `pnpm lint --max-warnings=0`, `pnpm prettier --check`, `pnpm tsc --noEmit`.
- [ ] 8.4 `pnpm build` — run it before any E2E, because the E2E suite serves the built bundle and a `.tsx`
  edit is invisible to it until the build runs. Verify the exit code directly rather than through a wrapper
  that can mask a failure.
- [ ] 8.5 Run the app and open the surface for real: list this machine's sandboxes, open a terminal on one,
  send `Ctrl-C`, and confirm the deployed-habitat and no-permission refusals read as their own sentences.
  A config existing is not evidence it works.
- [ ] 8.6 `openspec validate terminal-on-any-local-sandbox --strict`.
