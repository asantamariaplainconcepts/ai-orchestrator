## 1. Make the sandbox addressable

- [ ] 1.1 Add `IRunSandboxMonitor` to `BuildingBlocks/Agents`, mirroring `IRunPreviewMonitor` —
      `NameFor(runId)` and a `Hosted` answer so a habitat with no local sandbox says so rather than
      returning null for two different reasons
- [ ] 1.2 Add `RunSandboxHost` beside `RunPreviewHost`, copying its structure and its reasoning
      comment about why nothing is persisted
- [ ] 1.3 Record the name in `SbxAgentProcessHost.Run` beside `_lifecycle.Create`, and remove it in
      the same `finally` that calls `_previews.Gone` — no other write path may exist
- [ ] 1.4 Compose it: registered for the sbx launcher, and answering *not hosted* for the local and
      ACA hosts

## 2. Prove the pty before building on it

- [x] 2.1 **Gate for the whole change.** Verify an `openpty(3)` P/Invoke works from .NET 10:
      **done 2026-08-11, `poc/PtyCheck.cs`.** `openpty` works; the child gets a real controlling
      terminal, but only via `posix_spawn` + `dup2` file actions, because `Process.Start` cannot
      hand over a raw fd. **Live resize does not work this way at all** — `ioctl` is variadic and
      .NET answers `Vararg calling convention not supported`. Design D3 amended accordingly
- [ ] 2.2 **Settle the resize instrument** left open by 2.1, in D3's stated order: pin a pty package,
      or spawn `stty` against the slave device (`ptsname`), or a native shim as a last resort. Also
      measure the Linux arm, still untested — `openpty` lives in `libutil` there, not `libc`
- [ ] 2.3 Confirm the chosen instrument drives `sbx exec -it` specifically, not just `/bin/sh`:
      the spike proved sbx accepts a pty, `PtyCheck` proved .NET can make one, and nothing has yet
      proved the two together
- [ ] 2.4 Add an interactive-process seam beside `HeadlessProcess` — a long-lived child with
      writable stdin, no timeout kill, and `Kill(entireProcessTree: true)` on disposal, because
      killing the wrapper alone orphans the sbx CLI (measured by the spike)
- [ ] 2.5 A gated test that drives the seam against the real sbx CLI, per ADR-0020: a launcher is
      unverified until it has met its real CLI. Assert the pty facts, not a stand-in's idea of them

## 3. Permission

- [ ] 3.1 Add `RunPermissions.Attach = "run.attach"` with a summary saying why it is not `Read`
- [ ] 3.2 Grant it to the Admin and Member bundles, and record in the grant's comment that a
      Member's shell spends the machine owner's session (#288) — accepted on #304
- [ ] 3.3 A test that a caller holding only `run.read` is refused

## 4. The terminal surface

- [ ] 4.1 `RunTerminalHub` at `/hubs/run-terminal`, mapped by a `MapRunTerminalHub` use case beside
      `MapRunLogHub`
- [ ] 4.2 Authorize in the hub — the same two questions `RunLogHub.Watch` asks, in the same order,
      against the same table, plus `run.attach`. A hub dispatches nothing, so the decorator never
      sees it
- [ ] 4.3 `Open(runId, cols, rows)`: resolve the sandbox from `IRunSandboxMonitor`, refuse with the
      habitat's own answer when nothing is hosted, refuse when the Run is not executing, and start
      the pty
- [ ] 4.4 `Send` and `Resize`; push `output` frames back. Refuse a second concurrent attach on the
      same Run, naming the reason (design D7)
- [ ] 4.5 Kill the pty and its process tree in `OnDisconnectedAsync`, and again when the sandbox
      goes — a dropped browser must not leave a shell running
- [ ] 4.6 Record the attach against the Run (who, when). The terminal's bytes must NOT reach the Run
      log (design D6)

## 5. The terminal in the Run screen

- [ ] 5.1 Pin `@xterm/xterm` and `@xterm/addon-fit` in `src/frontend/package.json`
- [ ] 5.2 A read that answers whether this Run has a terminal right now — hosted, permitted,
      executing — modelled on the preview read's three-way shape
- [ ] 5.3 `RunTerminal.tsx` beside `RunPreviewFrame.tsx`: xterm bound to the hub, binary frames,
      `FitAddon` driving `Resize`, and the mock-mode guard every live surface needs
- [ ] 5.4 i18n keys in `shared/i18n/en.ts` for the heading and each of the three refusals — no
      hardcoded copy, per the design system's gate
- [ ] 5.5 Render it only while the Run executes, and say plainly when the sandbox has gone rather
      than leaving a dead terminal (agent-sandboxing scenario)

## 6. Close the loop

- [ ] 6.1 Functional tests for the three refusals — permission, habitat, not-executing — asserting
      they are distinguishable
- [ ] 6.2 Run the built frontend through the E2E tier if the terminal is asserted there; a `.tsx`
      edit is invisible to E2E until `pnpm build` runs
- [ ] 6.3 Resolve design D4's open question with the outcome of spec review, and amend #304's
      criteria 4 and 5 to match what this slice actually does
- [ ] 6.4 Update `ARCHITECTURE.md` if the runtime seam's shape changed, and note in #308 that the
      transport, grant and registry it depends on have landed
