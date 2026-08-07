# run-previews-over-published-ports — evidence

Machine: Apple Silicon, macOS 26.5.2. sbx v0.38.0 at `~/.local/bin/sbx`. Date: 2026-08-07.

## The whole path, against the real sbx (task 5.2)

`RealSbxSandbox_Should_Constraint.ARealPreviewPort_Should_BePublishedAndReachableThenGone`
drives the **shipped** code against the real CLI, gated on `AIO_SBX_EXERCISE=1` because CI has
no KVM:

```
$ AIO_SBX_EXERCISE=1 SBX_PATH=~/.local/bin/sbx dotnet test tests/AiOrchestrator.DispatchTests
Passed!  - Failed: 0, Passed: 59, Total: 59, Duration: 45 s
```

What that one test asserts, observed end to end:

- the sandbox published an **ephemeral loopback host port** while the agent ran,
- a page written on this machine was **served from inside the sandbox and fetched from outside
  it**, through that port,
- and the ledger entry was **gone** once the agent finished — the sandbox with it.

`sbx ls` afterwards: zero `aio-*` sandboxes.

**The first version of this test failed, and the failure was the design working.** It read the
port *after* awaiting the run, by which time the `finally` had already removed the record. The
test now reads it in flight, which is the only honest way to observe something that exists only
while its sandbox does.

## The surface, in the browser (tasks 4.1–4.3)

| State | Verified |
|---|---|
| Executing, preview available | frame renders above the output, `live` badge, copy naming whose application it is |
| Frame confinement | `sandbox="allow-scripts allow-forms allow-popups"` — **no `allow-same-origin`** — plus `referrerPolicy="no-referrer"` |
| Succeeded, **with** the preview flag on | **zero iframes, no heading, nothing** |
| Run ends while watching (`?previewEnds`) | frame replaced by one sentence: the preview closed with its sandbox, output and changes below |

**A real bug the browser exercise caught.** react-query's `enabled` stops a query from
*fetching*; it does not retract what it already fetched. On a finished Run the log has not
arrived on the first render, so the preview query fired and its answer stuck — framing a Run that
had ended. The guard now lives at render as well as at fetch. The backend would have refused
anyway (`available` is false for a terminal Run), but a surface that depends only on the server
being right is one deploy away from being wrong.

**Two mock defects fixed on the way**, both of the kind the mock's own notes warn about — a
fixture teaching the UI a shape the server does not send:

- run ids were `crypto.randomUUID()` per module load, so a mock Run **could not be reached by
  URL at all**; they are deterministic now, and a mock Run is linkable.
- the log reported `complete: false` for **every** Run including `Succeeded` ones, which is what
  let the stale-preview bug hide. It now derives from the Run's state, as the server derives it
  from `RunStates.IsTerminal`.

## Gates

```
dotnet build                     0 errors
dotnet csharpier check           379 files, clean
DispatchTests                    59/59  (incl. 5 against the real sbx)
Runs.FunctionalTests            152/152 (incl. 4 new preview tests)
Projects.FunctionalTests        110/110
Projects.UnitTests               40/40
Backlog.FunctionalTests          87/87
Backlog.UnitTests                40/40
frontend: tsc, eslint --max-warnings=0, prettier, design-system validator — all clean
```

## Not done, and why

- **A Run dispatched end to end through the orchestrator with a preview** — the same wall as the
  sandboxing change: the only configured project targets the owner's real repository, and
  DEC-062 has the agent publish its own work. **ADR-0014** is the standing answer, and this
  change is its second customer: with a rehearsal target, this would be one Run.
- **The relay against a real agent-authored page.** The relay's refusals are covered by
  functional tests and its happy path by the real-sbx exercise one layer down (the port serves,
  and the fetch succeeds) — but the two have not been joined through a running Server.
