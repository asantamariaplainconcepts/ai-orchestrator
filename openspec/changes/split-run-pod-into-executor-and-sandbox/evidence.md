# split-run-pod-into-executor-and-sandbox — evidence

Machine: Apple Silicon, macOS 26.5.2. sbx v0.38.0 at `~/.local/bin/sbx`. Date: 2026-08-07.

## The shipped driver against the real sbx (task 6.2, partial)

`RealSbxSandbox_Should_Constraint` exercises the **shipped** `SbxAgentProcessHost` — not a
model of it — against the real CLI. Gated on an environment variable, because CI has no
KVM/VMM:

```
$ AIO_SBX_EXERCISE=1 SBX_PATH=~/.local/bin/sbx dotnet test tests/AiOrchestrator.DispatchTests \
    --filter RealSbxSandbox_Should_Constraint
Passed!  - Failed: 0, Passed: 4, Total: 4, Duration: 26 s
```

What those four assert, observed for real:

- **Readiness** answers `ready=true`, `where="a per-Run sandbox on this machine"`, no remedy.
- **The workspace crosses and the credential does not**: inside the sandbox, `cat marker.txt`
  printed the host's file, `pwd` matched the host's absolute path exactly (virtiofs, same path
  — design D4's assumption holds), and `echo token=[${GITHUB_TOKEN}]` printed `token=[]` while
  the host's keychain holds a github secret. The agent authenticates without holding anything.
- **An inner failure travels**: `exit 7` arrived as exit 7 with `inner-detail` on stderr.
- **The CLI check answers from inside the sandbox**, and a nonexistent command answers false.
- **Nothing is left behind**: `sbx ls` shows zero `aio-*` sandboxes after the run.

**A correction found by running it.** The first attempt asserted `sh --version`, which fails
inside the sandbox — dash answers `sh: 0: Illegal option --`. That was the exercise's mistake,
not the driver's: the probe's `--version` assumption holds for the CLIs it is actually pointed
at (the spike observed `claude --version` → 2.1.221 and `opencode --version` → 1.18.13). The
assertion now uses `git`, and the reasoning is recorded in the test.

## The dev loop, in sandbox mode — and the bug only it could find

`aspire run --project src/root/AiOrchestrator.AppHost -- --Parameters:sandbox=true` (the opt-in
this change adds to the dev loop, because a substrate nobody can start from the keyboard is the
failure ADR-0001 exists to prevent). The real Server, against the real sbx daemon:

```
GET /api/pods →
  "host": { "where": "a per-Run sandbox on this machine", "ready": true, "remedy": null }
  ClaudeCodeHeadless  cliReady: false        ← the finding
  OpenCode            cliReady: false
```

**Both CLIs are installed on this Mac, and both reported not ready — correctly.** The D6 probe
asks inside a sandbox, and the driver was creating every sandbox from sbx's generic `shell`
template, which carries no agent CLI at all. Confirmed by hand rather than inferred:

```
$ sbx exec <shell-template>  sh -c 'command -v claude || echo NO-claude'
NO-claude
NO-opencode
$ sbx exec <claude-template> claude --version
2.1.221 (Claude Code)
```

Every Run would have failed with a missing binary. The fix: the runtime's own command selects
the sandbox image, because sbx names its agent templates after the CLIs they carry and those
names are exactly this product's runtime commands — configurable via
`Agents:Sandbox:AgentTemplates` so a new runtime stays a configuration line (DEC-012's promise).
After the fix, the same endpoint on the same machine answers `cliReady: true` for both, verified
from inside sandboxes rather than from this process's PATH.

Worth stating plainly: **the readiness property designed to stop the panel lying is what caught
this.** Had the probe kept answering from the host's PATH, it would have reported both runtimes
ready while every Run failed.

## A production bug the unit tests caught

`SbxAgentProcessHost.Sbx` returned `HeadlessProcess.Run(...)` **unawaited** inside its
`try`/`catch`. `HeadlessProcess.Run` is async, so a `Win32Exception` from an absent binary lands
in the task, not the catch — the caller would have seen a raw ENOENT instead of the remedy that
names `Agents:Sandbox:CommandPath`. Caught by
`AnAbsentSbxBinary_Should_RefuseNamingTheConfigurationKey`, fixed by awaiting inside the try.

## The stand-ins can fail

Required by task 6.1 ("the fake must be able to fail"). Verified by mutation: disabling the
missing-credential guard (`if (false && missing.Length > 0)`) turned
`AnInjectingHostWithNoStoredSecret_Should_RefuseNamingTheRemedy` red; restoring it turned it
green again. The suite is not one that would pass whatever we wrote.

## Gates

```
dotnet build AiOrchestrator.slnx        0 Error(s)
dotnet csharpier check .                Checked 367 files, clean (after one format pass)
DispatchTests                           55/55
ArchTests                               32/32
Projects.UnitTests                      40/40
Backlog.UnitTests                       40/40
```

The extraction was proven a no-op before anything was added: the pre-existing suites passed with
no assertion edited — only the two runtime constructions gained their new dependency.

## The panel (task 5.2)

The host reads **above** the runtime rows rather than beside them, because the rows describe
that machine: reading them without knowing which one would be reading them about the wrong
place, and while the host cannot answer, its remedy is the one to apply first. The chip carries
the same precedence. Seen in both themes; `validate-design-system.sh` green; typecheck, ESLint
`--max-warnings=0` and Prettier all clean.

The mock gained `?sandboxed` and `?sandboxDown` (the repository's idiom: every state reachable
without uninstalling anything) — **and was corrected**: it had rendered pods and a sandbox at
once, which composition refuses (D5). In sandbox mode the pod host now answers "not hosted
here", so the fixture cannot show a machine that could not exist.

## Run previews over `sbx ports` — verified feasible (not implemented)

The spike recorded this as a possibility; it is now measured rather than assumed:

```
$ sbx run -d --name preview-probe -p 8000 shell /tmp     # HOST_PORT omitted
$ sbx ports preview-probe
HOST IP     HOST PORT   SANDBOX PORT   PROTOCOL
127.0.0.1   49152       8000           tcp
$ sbx exec -d preview-probe ... python3 -m http.server 8000 --bind 0.0.0.0
$ curl http://127.0.0.1:49152/index.html                  # from the HOST
<h1>preview from inside the sandbox</h1>
```

An ephemeral host port is allocated automatically and bound to loopback, and content served
inside the sandbox is reachable from the host. So per-Run preview environments need no new
infrastructure: the launcher publishes, the portal reverse-proxies, the preview dies with its
sandbox. Two constraints found while proving it: `-p 0:8000` is rejected (`port 0 out of
range`) — the ephemeral form is to **omit** the host port entirely, `-p 8000`; and the server
inside must bind `0.0.0.0`, not `127.0.0.1`.

Deliberately not built here. It is a product capability with its own questions — agent-authored
HTML served through the portal's origin needs an isolated origin or strict iframe sandboxing,
and the Run's lifecycle would have to keep a sandbox alive past the agent's exit, which
contradicts this change's D3 ("disposed when that Run's agent finishes"). That contradiction is
the reason it must be its own change rather than a flag added here.

## Not done, and why

- **The environment panel (task 5.2)** — the API now carries the agent host's state
  (`GET /api/pods` → `runtimes.host`: where, ready, remedy), but nothing renders it. UI work in
  this repository is routed through the design system (`aio-design`), which is its own pass;
  doing it as an afterthought is how a panel ends up ignoring the tokens and the i18n contract.
- **A Run driven end to end through the orchestrator — NOT VERIFIED, and accepted as such by the
  owner (2026-08-07).** The dev loop was brought up in sandbox mode and everything short of
  dispatching a Run was exercised: composition, the sbx host's readiness against the real
  daemon, and the runtimes' CLI readiness from inside real sandboxes. The Run itself was not
  launched because the only configured project points at the owner's real repository
  (`asantamariaplainconcepts/ai-orchestrator`, four live backlog stories) and DEC-062 has the
  agent publish its own work — so a Run would push a branch and open a pull request on a real
  repository. The owner was asked, could not supervise it, and accepted the change without it.

  **What therefore remains unproven**, stated so nobody later assumes otherwise: that a Run's
  streamed output reaches the portal from inside a sandbox, that `RunExecutor`'s prepared
  workspace is the one the sandbox mounts in the real pipeline, and that the credential-source
  sentence appears in a real transcript. Each is covered by a unit or a real-sbx test in
  isolation; none has been seen end to end. The read-only ad-hoc-prompt Run (#275) is the safe
  way to close this when someone can watch it.
- **Linux/KVM** — untouched, as the proposal scoped. Every observation here is macOS.
