## Context

#304 built the whole mechanism: `InteractivePty` (a real pty, because `sbx exec -it` refuses a plain
pipe), `IRunTerminalHost` as the habitat seam, `SbxRunTerminalHost` as its self-host implementation,
`UnhostedRunTerminalHost` as the deployment's honest "nothing here", and `RunTerminalHub` as the
byte pump with `run.attach` checked in the hub because a SignalR hub dispatches nothing through the CQS
pipeline and so the authorization decorator never sees it.

Every one of those is keyed by `Guid runId`. The sandbox name is resolved from `RunSandboxHost`, an
in-memory `Guid → string` ledger written beside creation and removed beside disposal, deliberately never
persisted — "a stored row outlives the thing it describes and lies after a restart"
([RunSandboxHost.cs:10](../../../src/shared/AiOrchestrator.Infrastructure/Agents/RunSandboxHost.cs#L10)).

That ledger is also the reason a whole class of sandbox is unreachable. It knows only *this process's*
currently-executing Runs. An `aio-run-*` sandbox left by a killed process is invisible to it and lives
until the next startup sweep. This change reaches those.

### What was verified against the real `sbx`, not assumed

Run on this machine on 2026-08-11 (ADR-0001 discipline — the CLI's behaviour is the design's
foundation, so it was exercised rather than inferred):

- **`sbx ls --json` exists** and returns
  `{"sandboxes":[{"name","id","agent","status","workspaces":[…]}]}`. `--quiet` gives names only. The
  human table has a header row (`SANDBOX AGENT STATUS PORTS WORKSPACE`).
- **A sandbox has a `status`**, observed `stopped`. `ReapAbandoned` today ignores status entirely.
- **`sbx exec` on a *stopped* sandbox starts it.** Observed verbatim:
  `Sandbox opencode-ds-connect started successfully` followed by the command's output, exit 0. This is
  the single most consequential finding for this change and D5 exists because of it. (The probe left the
  machine as it found it — the sandbox was stopped again.)
- **The only sandbox on this machine is `opencode-ds-connect`** — an `sbx`-managed sandbox created
  outside this product, i.e. exactly the case acceptance criterion 2 requires to be absent.

## Goals / Non-Goals

**Goals:**

- List this machine's sandboxes within the namespace the host claims, with the Run each belongs to where
  one is known.
- Open a terminal on a listed sandbox by name, with `Ctrl-C` arriving as a signal.
- Refuse in causes a reader can tell apart: permission, habitat, not-in-namespace, gone.
- Record every attach durably, including attaches with no Run to record against.

**Non-Goals:**

- Creating a sandbox on demand (RULE-002 — a separate capability; it was this issue's original shape).
- Sandboxes outside the claimed namespace, in listing or in entry.
- Any terminal in a deployed habitat (ADR-0021 refuses it).
- Conversations: self-host conversations run in-process (`InProcessConversationRuntime`) with no sandbox.
- Holding a sandbox open for an attached human — the existing requirement *"a human attached to a sandbox
  does not extend its life"* stays intact, and D5 is careful not to breach it.

## Decisions

### D1 — The listed set is the reaper's two prefixes, resolved through `sbx ls --json`

**Decision.** A sandbox is listable iff its name starts with `aio-probe-` or `aio-run-`, read from
`sbx ls --json`.

The issue and the existing spec both say "`aio-*`". That is shorthand:
`SbxSandboxLifecycle.ReapAbandoned` claims exactly those two prefixes
([SbxSandboxLifecycle.cs:138](../../../src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxSandboxLifecycle.cs#L138)),
and the other `aio-` names on the machine (`aio-carry-*`, `aio-workspace-*`) are host temp paths, not
sandboxes. Binding the surface to the reaper's own predicate is the whole of the safety argument: the
surface reaches exactly what the lifecycle already manages, and never a sandbox this product did not
make. `opencode-ds-connect` fails the predicate, which is criterion 2 satisfied by construction.

The predicate is extracted to one place and used by both the reaper and the listing, so the two cannot
drift into disagreeing about what this host owns.

**`--json` over the column parse.** `ReapAbandoned` splits on whitespace and takes the first token. That
is enough for names and wrong for anything else: `status` and `workspaces` are needed here, `PORTS` is
empty in the observed output (so column positions shift), and the header row survives the split. The
reaper's parse is replaced by the JSON read rather than a second parse being added beside it.

*Rejected:* a broader `aio-` prefix — it would claim temp-path names that are not sandboxes and widen the
boundary past what the reaper manages. *Rejected:* an allowlist of names the product remembers creating —
that is `RunSandboxHost`, and it cannot see the abandoned sandboxes this change exists to reach.

### D2 — `run.attach` is read at habitat scope for this surface, and the widening is written down

**Decision.** The sandboxes surface and its terminal require the caller to hold `run.attach` **on at
least one project**. Where a sandbox resolves to a Run, the check additionally uses that Run's project,
exactly as `RunTerminalHub.Open` does today.

`IProjectPermissions.RoleOn(projectId)` is project-scoped, and an abandoned `aio-run-*` sandbox resolves
to no Run and therefore to no project. There is no project-scoped check that can authorize the sandboxes
this surface exists to reach — the alternatives are to widen the scope or to drop those sandboxes and
ship #304 again under a new name.

The widening is smaller than it reads, and only in this habitat. ADR-0021/DEC-065 confine the surface to
self-host, and DEC-016 fixes self-host as one owner, one machine — the same assumption that already lets
`ReapAbandoned` delete any `aio-*` sandbox it finds without asking whose Run it was. A caller who holds
`run.attach` somewhere on a one-owner machine is that owner. What this must not become is a habitat-scoped
reading that leaks into a deployment, so the check is refused before it is reached there: `Hosted` is
false, and the habitat refusal is answered first.

*Rejected:* Admin-only (ACT-001). It contradicts the issue's stated actor and #304's deliberate choice to
grant Members `run.attach`, which `RunTerminalRefusal_Should_Constraint:116` pins as a test. *Rejected:*
listing only Run-attributable sandboxes — it satisfies criterion 1's "where it has one" vacuously and
leaves the abandoned sandboxes unreachable, which is the change.

### D3 — A caller-supplied name reaches `sbx exec` only after being resolved against a fresh listing

**Decision.** The sandbox-keyed `Open` takes a name, re-reads the listing, and refuses any name not in
it. The name is never passed through from the request to the CLI.

#304 wrote the opposite invariant down —
"there is no path by which a caller-supplied name reaches `sbx exec`, which is what stops this becoming
a way to enter any sandbox on the machine"
([SbxRunTerminalHost.cs:11](../../../src/shared/AiOrchestrator.Infrastructure/Agents/Sbx/SbxRunTerminalHost.cs#L11)).
This change introduces that path deliberately, so the sentence that made it safe must be replaced by
another rather than deleted: the ledger stops being the bound and the namespace predicate becomes it.
The doc comment is updated in the same change, because a comment asserting an invariant the code no
longer holds is worse than none.

Re-reading rather than trusting a listing the client holds is the point: a client's list is a memory of
what was true when the page rendered, and TOCTOU here means a shell in a sandbox that has since been
reaped and recreated for a different Run.

### D4 — The attach record gets its own durable home, and still writes into the Run's log where there is one

**Decision.** `IRunAttachRecorder` gains a sandbox-keyed record written to a durable table
(who, when, sandbox name, and the Run id where known). Where the sandbox resolves to a Run, the existing
`RunLogChunk` line is *also* written, unchanged.

The existing recorder writes a `RunLogChunk` keyed by `runId`
([RunAttachRecorder.cs:29](../../../src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Observation/RunAttachRecorder.cs#L29)).
A Run-less sandbox has no log to append to, so criterion 7 cannot be met by the existing shape for the
sandboxes this change adds. Keeping the Run log line where a Run exists preserves #304's property that an
attach appears "beside everything else that Run did" and keeps the two entry points telling the same
story; the table is what makes the record hold for the whole surface rather than the attributable slice.

The terminal's bytes are still not recorded, for #304's reason: a Run's record stays the agent's record
rather than becoming a screen capture.

*Rejected:* structured logs only. OpenTelemetry export is configured but log retention is not a record
anyone can query per sandbox, and criterion 7 asks for a record. *Rejected:* dropping the `RunLogChunk`
line in favour of the table alone — it would silently regress #304's criterion 6.

### D5 — A stopped sandbox is listed, marked, and entering it is a stated act

**Decision.** The listing carries each sandbox's `status`. Opening a terminal on a **stopped** sandbox is
permitted, and the surface says plainly that doing so starts it. It is recorded as an attach like any
other.

This exists because `sbx exec` on a stopped sandbox **starts it** — observed, not assumed. Left
unexamined, clicking a greyed-looking row would silently boot a microVM, which is precisely the resource
leak `ReapAbandoned` was written to fight after 31 sandboxes and 125 GB.

It does not breach *"a human attached to a sandbox does not extend its life"*: that requirement governs a
Run's sandbox being held past its Run, and nothing here holds anything. A started sandbox remains subject
to the startup sweep, which is the backstop DEC-065 already names. BR-006 is upheld — what bounds the
sandbox is the machine's own reaper, never the person.

*Rejected:* hiding stopped sandboxes — they are among the ones worth entering (an abandoned Run's
workspace is a forensic artefact). *Rejected:* refusing to enter them — it would leave the surface unable
to reach the case that motivated it, and the start is a legitimate act as long as it is not a silent one.

### D6 — Vertical slice placement, and one deviation stated

The listing is an ordinary query in `Modules/Runs/Features/Observation/UseCases`, dispatched through the
CQS pipeline. The terminal is a second method on `RunTerminalHub`, which is **the deviation**: a hub
dispatches nothing, so it authorizes inline. That is not new — it is #304's accepted exception, and
`ProjectRoles_Should_Constraint` records `run.attach` in `EnforcedOutsideThePipeline` with its enforcer
named. This change adds a second enforcer, so that doc comment is updated; a permission landing in that
set without a named enforcer is the rot the constraint exists to catch.

**Corrected during implementation.** This section first said the query would carry `[Requires]` and, when
that proved impossible to pair with `IScopedToProject`, that it would carry no attribute at all. Both were
wrong. The decorator **default-denies an undeclared request** — that is design D1 of the authorization
work, and it turned the read into a 403 the moment it was exercised. The codebase already has the right
declaration: `[Requires(Access.FiltersToCaller)]`, whose own definition is "reaches across projects, and
narrows its own answer to the ones the caller may see". That is exactly this read — the machine is inside
no project, and the handler returns nothing to a caller who may not attach — and it is what `ListProjects`
and `GetInbox` already use. It also removes the need for the ArchTest waiver an earlier draft of this
change added, because `FiltersToCaller` *is* a declaration a reviewer can check.

The seam stays habitat-shaped: `IRunTerminalHost` grows enumeration and a name-keyed `Open`, and
`UnhostedRunTerminalHost` answers both with "nothing", so the Runs module still never learns what a
sandbox is.

### D7 — UI governed by the design system

`docs/design-system/` is canonical, with `DESIGN.md` derived and drift-gated in CI. The surface reuses
`features/runs/RunTerminal.tsx`'s xterm transport rather than a second terminal component. Tokens only,
no hardcoded colour; all copy through the typed i18n catalogue — hardcoded JSX copy fails CI
(DEC-009, DEC-021).

### Two #304 bugs this change had to fix to work at all

Neither was in scope and neither was optional: acceptance criterion 3 cannot pass while either stands.
Both were latent in the Run-keyed terminal too, and both were found by running the app rather than by
reading it — which is the whole argument for criterion 3 being "a shell is reachable" and not "a shell is
requested".

- **`InteractivePty` used `posix_spawn`, which does not search `PATH`.** The default `CommandPath` is the
  bare name `sbx`, and on this machine `sbx` lives in `~/.local/bin`. The listing worked and the terminal
  failed with `rc 2` (ENOENT), because `HeadlessProcess` starts its child through `ProcessStartInfo`,
  which resolves `PATH`, while the pty called the one spawn variant that does not. Two ways of starting
  the same binary that disagreed about how to find it. Fixed to `posix_spawnp`.
- **The byte pump ran its first blocking read on the hub's own thread.** `_ = Pump(...)` looks like
  fire-and-forget but an async method runs synchronously until its first *suspending* await, and this
  one's first act is a blocking `Read`. When the WebSocket send completes synchronously — as it usually
  does — the loop goes straight back into a read that blocks until the shell speaks again, and the hub
  method never returns. The symptom is precise and misleading: a working prompt on screen above a
  surface still saying "Opening a shell…", because output streams from the pump while the client waits
  forever for `Open` to resolve. Fixed with `Task.Run` at both call sites — a blocking syscall belongs on
  a thread-pool thread, which is what #304's own comment already said it was doing.

## Risks / Trade-offs

- **A machine-wide shell is a bigger blast radius than a Run-scoped one (#288).** → Bounded to self-host
  by `Hosted`, to the reaper's namespace by D1, and to `run.attach` holders by D2; every entry recorded
  by D4. The exposure is the one #304 already accepted, not a new class of it.
- **D2 widens `run.attach` from project scope to habitat scope.** → Confined to a habitat DEC-016 defines
  as one owner, one machine, and unreachable in a deployment because the habitat refusal answers first.
  Written down here rather than discovered, and pinned by a test that the deployed habitat refuses.
- **Entering a stopped sandbox starts a microVM.** → D5: stated in the surface, recorded, and left to the
  startup sweep that already claims the namespace.
- **Replacing the reaper's `ls` parse touches startup.** → `--json` was verified on the real CLI; the
  reaper's behaviour is covered by the existing sandbox tests, and the shared predicate keeps listing and
  reaping from drifting.
- **TOCTOU between listing and entering.** → D3 re-reads at open. The residual window is a sandbox reaped
  between resolve and `exec`, which surfaces as the sandbox-is-gone ending criterion 6 already requires.
- **Attribution is partial by design** — `RunSandboxHost` is in-memory, so a sandbox from a previous
  process shows no Run. → That is criterion 1's "where it has one", and persisting the ledger is refused
  for the reason it was refused in #304: a stored name lies after a restart.

## Open Questions

- Whether the durable attach table (D4) belongs in the Runs module's schema or is better placed once a
  second machine-scoped record exists. Proposed: Runs schema now, since it is the only module that owns
  attaches, and moving one table is cheaper than inventing a module for it.
- Whether `aio-probe-*` sandboxes are worth showing. They are in the claimed namespace so D1 lists them,
  but they live ~30 seconds and will mostly be gone by the time anyone clicks. Proposed: list them and let
  criterion 6 handle the disappearance, rather than adding a second, unstated boundary inside the first.
