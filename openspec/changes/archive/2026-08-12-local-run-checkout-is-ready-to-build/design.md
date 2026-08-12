## Context

`RunExecutor.Invoke` (`src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Execution/RunExecutor.cs`)
takes the Local branch when `run.Locus == RunLocus.Local`: it writes the host-credentials line through
`onOutput`, calls `ILocalCodeWorkspace.Prepare`, records the local execution on the Run, invokes
`selection.Runtime.Execute` with `automation.Timeout`, and calls `Conclude`. After #331
(`local-run-in-its-own-checkout`) `Prepare` returns a **worktree** rather than the owner's folder, so
the path handed to the runtime is a checkout created for this Run and removed with it.

That checkout is empty of everything a build needs. `LocalAgentProcessHost`
(`src/shared/AiOrchestrator.Infrastructure/Agents/HeadlessProcess.cs`) runs the Agent CLI as a child
of the Server process, inheriting the Server's environment and adding the credentials it is given —
so the Agent arrives in a tree with no `node_modules` and no restored packages, and any Story of the
form "make the tests pass" fails on setup rather than on the work.

`HeadlessProcess.Run` is already exactly the process discipline this needs, and this design reuses it
rather than writing a second one: it streams both stdout and stderr line-by-line through `onOutput`
as they arrive (#96), enforces a timeout with `Kill(entireProcessTree: true)`, and reports
`TimedOut` as an outcome distinct from any exit code (BR-005).

The Connector already carries the code source and its absolute `LocalPath`
(`src/modules/Backlog/AiOrchestrator.Modules.Backlog/Domain/Connector.cs`), configured through
`ConfigureConnector` behind `[Requires(BacklogPermissions.Configure)]` — Admin standing, already
enforced by the pipeline. `IConnectorReader` is how the Runs module reads any of it.

**Measured before it entered this design** (probe run 2026-08-12 on macOS 25.6, `/bin/sh`, against
the real shell — ADR-0006 discipline):

| Claim | Result |
| --- | --- |
| `sh -c 'echo one && echo two && exit 3'` | both lines printed, exit **3** — a chained command line and its status survive the shell |
| `sh -c 'false && echo unreachable; echo after'` | `unreachable` absent, exit **0** — a `;` chain reports only the *last* command's status |
| a command writing to stderr | captured, and reaches the same stream as stdout |
| `sh -lc 'true'` | sourced `~/.profile` and printed an unrelated error from it **before running anything** |

The last two rows decide D2. The second row is a shell semantic the product must state rather than
fix.

## Goals / Non-Goals

**Goals:**

- An Admin names, once per project, the command that makes a fresh checkout buildable.
- The command runs to completion in the Run's own checkout before the Agent starts, and its output
  is in the Run's log ahead of the Agent's (UC-027, BR-014).
- A failing setup ends the Run before any Agent spend, saying it was the setup and naming the command
  (BR-004).
- Setup and the Agent share one phase budget (BR-005); neither gets a second clock.
- Nothing configured means nothing runs — absence is not an error.
- The command can never come from a file the Agent itself can write.

**Non-Goals:**

- Setup for sandboxed (sbx/ACA) Runs. That lane has UC-031 ahead of it, with its own trust ceremony.
- UC-031 itself — a repository-declared setup file, trusted per version. This is not a smaller
  version of it (D1).
- Caching or reusing anything between Runs. Every checkout is prepared from scratch, and making that
  cheap is separate work.
- Widening where a Local Run is available. The habitat gates from #331 and #247 are untouched.
- Changing what the Agent does once it starts, or what it is told.
- Validating the command at configuration time. Whether `pnpm` resolves is knowable only on the
  machine, at the moment it runs — the same reason `ValidateLocalPath` answers about a path and not
  about a build.

## Decisions

### D1 — the command is a field on the Connector, never a file in the repository

The setup command is stored beside the code-source folder and configured through the same
Admin-gated endpoint. It is deliberately **not** read from the checkout.

The argument is specific to this lane. On a Local Run the repository *is* the thing the Agent is
editing, and the Agent runs as the machine owner with the machine owner's environment, keychain and
push credentials. A file in the repository that names commands to execute would let the Agent write
that file in Run N and have Run N+1 execute it on its owner's account. That is precisely why UC-031
requires an Admin to trust the file **per version**, with re-trust on change — a real ceremony, with
real surface, which this slice would otherwise have to build in miniature and get right.

A product-side field has no such problem: nothing the Agent writes can become a command, so no trust
ceremony is needed and none is invented.

*Rejected — reading `.aio/setup.sh` (or similar) from the checkout.* It is UC-031 with the trust
removed, which is the one part of UC-031 that cannot be removed.

*Rejected — a per-Automation command.* Setup describes **the repository**, not the action being
taken: every Automation on the same folder needs the same tree. Per-Automation would multiply one
fact across every Automation an Admin creates and let two of them disagree about how the same
checkout is built.

### D2 — one command line, through a non-login shell, in the Server's own environment

The field holds a command **line**, not an argv vector, because what an Admin needs to write is
`pnpm install --frozen-lockfile && pnpm build`. Storing argv would make the common case
inexpressible, so the line is handed to a shell: `/bin/sh -c <line>` on Unix, `cmd.exe /c <line>` on
Windows.

**Not a login shell.** Measured above: `sh -lc` sourced `~/.profile` and wrote an error from it into
the output before the command ran — a Run's log would carry the operator's profile noise, and a
broken profile line would look like a setup failure. The stronger reason is agreement: the child
inherits the Server process's environment, which is *exactly* the environment
`LocalAgentProcessHost` already gives the Agent. Setup and the Agent therefore resolve the same
`PATH` and the same toolchain — a login shell would give setup a different one, and `pnpm` resolving
for the install but not for the Agent's own test run is the confusing failure this avoids.

The consequence is stated rather than hidden: where the Server runs as a service with a minimal
`PATH`, `pnpm` may not resolve — and the refusal then carries the shell's own `command not found`,
which names the problem. That is the same environment the Agent has always had.

**The exit status is the shell's, not the product's.** Measured above: `a; b` reports only `b`'s
status, so a failure of `a` does not fail the Run. This is the shell's rule and the product does not
rewrite it; the spec says so, so nobody reads it later as a bug.

### D3 — a seam of its own: `ILocalCheckoutSetup`

A new interface in `AiOrchestrator.BuildingBlocks/Agents`, implemented in Infrastructure over
`HeadlessProcess.Run`, returning a three-way outcome (completed with a status / timed out) plus the
captured output.

*Rejected — `IAgentProcessHost`.* That is one composed singleton **per habitat**: `sbx` or `aca`
where those launchers are configured (`AgentSandboxComposition`). Routing setup through it would
send an Admin's `pnpm install` into a sandbox that has no such checkout. Setup must run on the
machine that owns the folder, which is this process, always.

*Rejected — another method on `ILocalCodeWorkspace`.* That seam is git: inspect, branch, commit. Its
implementation shells out to `git` with a fixed argument list and no user input anywhere in it.
Executing an operator's arbitrary command line is a different responsibility with a different test
surface, and merging them would make the git seam the place a reviewer has to look for
command-execution behaviour.

`HeadlessProcess` is `internal` to the Infrastructure assembly and the implementation lives there,
so nothing about its visibility changes.

### D4 — the executor holds one phase deadline and hands the Agent the remainder

`automation.Timeout` becomes the budget for **the phase**, not for the runtime invocation. The Local
branch of `Invoke` starts the clock (via the injected `TimeProvider`, as everything else in the
executor already does), runs setup with the full remaining budget, and invokes the runtime with
`budget − elapsed`.

Three outcomes, three sentences:

- setup exits zero → the Agent runs with what is left;
- setup exits non-zero → the Run fails naming the setup, before the runtime is invoked (D5);
- setup exceeds the budget → the Run fails naming **the limit** (BR-005's own sentence), not the
  setup's — a Run that ran out of time did not fail its build.

A remainder that is zero or negative is treated as the overrun case rather than invoking the runtime
with a dead clock.

*Rejected — a separate timeout for setup.* A second limit an Admin has to reason about, and it lets
a Run exceed the sixty-minute ceiling DEC-054 puts on a phase — a Run could then spend an hour
installing and another hour working, which is the thing BR-005 exists to prevent.

**BR-005 needs no amendment.** Its text bounds the `Executing` phase, and `MarkExecuting` is already
called before any of this; the sentence in `agent-execution`'s spec that says *"the timeout clock is
the runtime invocation only"* is what stops being true, and that is a spec delta, not a product-rule
change.

### D5 — a failure that a reader can tell from an Agent failure

A new `LocalSetupErrors` in the `WorkspaceErrors` / `LocalWorkspaceErrors` pattern, with distinct
codes for the non-zero exit and for the overrun. The Run's reason names **the command as configured**
and the **tail** of its output — the tail because that is where a build error is, and because the
whole output is already in the log (BR-014), so the reason carries evidence rather than a transcript.
Truncation reuses the executor's existing `FailureLimit`.

The refusal states that it was the setup that failed. Criterion 2 is a legibility requirement: a
person reading `Failed` needs to know whether to fix their repository's build or their Story.

### D6 — the log says what is about to run, before it runs

One header line through `onOutput` naming the command, written **before** the process starts, then
the command's own output streamed line by line by `HeadlessProcess`. Written first so that a setup
which hangs is legible *while* it hangs — UC-027 is about watching a Run execute, and output that
only appears at the end fails that for the exact phase where it matters most.

Ordering needs nothing new: `RunLogWriter` is already open around the whole of `Invoke`, and the
Local branch already writes its host-credentials line through the same `onOutput`. Setup lines land
ahead of the Agent's because they are written earlier, in one stream.

### D7 — order, and cleanup on the setup path

`Prepare` → setup → runtime → `Conclude`. Setup runs after the checkout exists because it must run
*in* it, and after `RecordLocalExecution` so a Run that dies during a long install still records
which checkout it was working in (BR-014).

A setup failure takes the same exit as a failed Run: `Conclude(succeeded: false)`, so #331's checkout
removal happens on this path too and a failed setup leaks no checkout.

### D8 — the Runs module reads it through Contracts

`IConnectorReader` (`AiOrchestrator.Modules.Backlog.Contracts`) gains the field alongside `LocalPath`,
which is how `RunExecutor` already reads the folder. No new cross-module reference; MOD001–005 are
unaffected.

### D9 — the surface: one input, beside the folder, local-folder only

Governed by `DESIGN.md` (generated from the canonical `docs/design-system/`) and by
`connector-configuration`'s existing form requirements: the input sits inside the **Advanced**
disclosure beside the folder path, its explanation beside it rather than pooled at the end of the
form, and it is composed from the kit's `Input`/`Label` (`src/frontend/shared/ui/`) with token-only
styling. All copy resolves through the typed i18n catalog (`src/frontend/shared/i18n/en.ts`) —
hardcoded JSX text fails lint (DEC-009, DEC-021).

It follows the discipline `connector-configuration` already states for the code-repository field:
**hiding and clearing are the same act**. Where the code source is `Repository` the input is not
rendered and the request sends the field as null, so no stale command survives a switch and then
runs on a folder nobody configured.

The input is optional and stays optional — unlike the folder path, an empty value is a valid
configuration, so nothing about the disclosure's cannot-collapse rule changes.

## Risks / Trade-offs

- **An Admin's typed string is executed on the operator's machine.** → Accepted by design and
  bounded: Admin-only through the existing permission declaration, product-side so the Agent cannot
  author it (D1), and run as the same user with the same environment the Agent process already gets.
  No boundary moves; what is new is the string's source, and D1 is entirely about that source.
- **A cold install can eat most of the phase budget**, leaving the Agent little of a thirty-minute
  default to work in. → The Run fails naming the limit, which is diagnosable, and the remedy is the
  caching work this change explicitly defers. Stated here rather than discovered: on a repository
  this size a cold `pnpm install` is minutes, not seconds.
- **The Server's `PATH` may not carry the toolchain** where it runs as a service rather than from a
  terminal. → The shell's own `command not found` reaches the Run's reason, and the environment is
  identical to the Agent's, so the two never disagree about what is installed (D2).
- **`cmd.exe /c` is not `sh -c`.** A command line written for one shell may not work on the other,
  and self-host on Windows is real. → The field is per-project on the machine that runs it, so it is
  written for that machine; nothing here pretends the string is portable.
- **`a; b` hides a failure of `a`.** → The shell's rule, stated in the spec (D2) instead of
  papered over with argument parsing the product would then own.
- **Hard dependency on #331.** There is no checkout to prepare until it lands, and this change's
  implementation rebases onto it. → Sequenced immediately behind it and merged with it; if #331's
  checkout shape changes in review, this follows rather than duplicating it.

## Migration Plan

One additive EF Core migration on the Backlog schema: a nullable column on `Connectors`. No data
migration and no backfill — every existing row means "no setup command", which is the behaviour
those Connectors have today.

No message-contract, Aspire, host-csproj or CI change. `docs/product/v1/04-capabilities.md` gains one
sentence on UC-031 distinguishing it from this command, following the precedent #308 sets for
amending a product doc inside the change that makes it untrue.

Rollback is `git revert`: nothing outside the repository is mutated, and a Connector's stored command
becomes inert rather than dangerous — the column is simply no longer read.

## Open Questions

- **Whether the Run page should delimit setup output from the Agent's.** Today a Run's log is one
  stream and D6 keeps it one; if readers cannot tell the two apart in practice, that is a follow-up
  on the surface, not on this seam.
- **Whether a later sandboxed lane reuses this field or is superseded by UC-031.** Out of scope here,
  but the answer decides whether the field stays local-folder-only.
- **Whether a reused checkout could skip setup.** It is the shape the caching follow-on would take;
  nothing in this change forecloses it, because setup is a step in the ceremony rather than a
  property of the checkout.
