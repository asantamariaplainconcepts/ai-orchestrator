# Evidence

Verified 2026-08-10 against the repository at `origin/main` (55cbb72, after #299/#300) and against
real hardware where a claim is empirical. Every quotation below was read from the cited file, not
recalled.

## 1.1 — Which of ADR-0008's three pillars moved

ADR-0008 rejected alternative **(b) a live session** and rested its decision on three premises. Their
current state, quoting the superseding text rather than paraphrasing it:

**Pillar 1 — DEC-013, "nothing idles": superseded.**
`10-locked-mvp-decisions.md` now reads DEC-013 struck through, with:

> *(superseded 2026-08-09 by `runs-execute-in-azure-sandboxes`, #296)*: dispatch goes through the
> Postgres outbox in every habitat, consumed by the Server's own subscriber. The queue's three
> reasons retired with the pod substrate — execution stopped being heavy […] the worker's
> scale-to-zero stopped saving anything

The premise is not weakened but **gone**: the substrate whose cost model produced "nothing idles" no
longer exists, and the Server already runs a long-lived subscriber.

**Pillar 2 — "nothing idles" as applied to the container: already revised, twice.**
DEC-061:

> a portal conversation (#166) runs in an on-demand Container Apps **session**, one per conversation
> […] It starts on the first message, keeps its cloned workspace, and the platform reclaims it after
> ten minutes of inactivity. […] What is accepted is bounded idling, by the pool's own cooldown

And DEC-063, which revised even DEC-061's "nobody talking costs nothing":

> the conversation session pool holds `readySessionInstances = 1` at 1 vCPU and 2 GiB, continuously
> […] **What is accepted:** a standing cost in dev for a container nobody is talking to, at the full
> size

So the product already accepts bounded idling *and* an unconditional standing idle. ADR-0008's own
status line concedes the first half: *"the 'nothing idles' consequence revised by DEC-061, #166"*.

**Pillar 3 — BR-006, human waits are untimed: intact.**
`05-business-rules.md` still reads that `AwaitingApproval`, `AwaitingInput` and `Queued` count toward
no timeout. Nothing has revised it, and ADR-0008 says plainly: *"BR-006 is what decides it."* This is
the pillar the decision now turns on, and the only one.

## 1.2 — The two cost facts, as numbers

**Deployed (ACA).** DEC-063 fixes a standing cost that exists whether or not anyone is talking:
`readySessionInstances = 1` at **1 vCPU and 2 GiB, continuously**. Azure refuses zero
(`SessionPoolInvalidReadySessionInstances`), so this is a constraint discovered at apply, not a
preference. The marginal cost of one *more* held session is therefore an increment on a pool that is
already paid for — which is a materially different question from ADR-0008's "a replica paid for a
week".

**Self-host (sbx).** No per-hour money cost: the sandbox runs on the machine owner's own hardware. It
is **not** free of resources, and the honest figure is memory, not money — `SbxSandboxOptions`
declares `DefaultMemory = "4g"` per sandbox. The measured failure mode is disk, recorded in
`SbxSandboxLifecycle.ReapAbandoned`: **31 running sandboxes and 125 GB**, 25 of them probe sandboxes
created every thirty seconds, because a process died before its `finally` ran.

The distinction that matters for the decision: in self-host the cost of holding a sandbox is a
resource the owner already owns and can reclaim; in a deployment it is billed. That is an argument for
examining the habitat split (shape 3), not for assuming it.

## 1.3 — What the spike measured, and only that

`poc/TerminalSpike.cs` and `poc/findings.md`, probed 2026-08-10 against sandbox `spike-term`:

| Probe | Result |
|---|---|
| `sbx exec -i` with a piped stdin | works; `tty` answers **`not a tty`** — a line pipe, no signals |
| `sbx exec -it` with a piped stdin | **fails**: `ERROR: inspect exec: context deadline exceeded` |
| `sbx exec -it` under a host-allocated pty (`script`) | **`/dev/pts/1`** — a real pty |
| geometry | `stty size` → `58 128`, matching the browser's own measurement |
| signals | `^C` interrupted a running `sleep 300` and returned the prompt |
| full-screen rendering | `top` drew its reverse-video header and refreshed live |

**Not addressed by the spike, and therefore not evidence for anything:** authentication, audit, what a
second writer does to the agent's working tree, resize after connect (the `script` trick cannot
propagate `TIOCSWINSZ`; the product would need a real openpty), and cost in a deployment.

One finding was corrected mid-spike and is recorded because the false version was nearly designed
around: an apparent "the Enter key does nothing, so the pty lacks `icrnl`" was the **test harness** —
the browser tool's synthetic `Return` never reached xterm's textarea. A CR pushed down the socket
executes the line normally.

## 1.4 — The transcript premise, verified in code after #299/#300

Both runtimes are invoked headless with structured output:

- `ClaudeCodeHeadlessRuntime.cs:70-73` — `-p`, `--output-format`, `stream-json`
- `OpenCodeRuntime.cs:49` — `run`

And `src/frontend/features/runs/transcript.ts` renders from exactly that shape. Its own header states
the contract:

> Every line is "a JSON object if it parses, text if it doesn't". Well-known fields are lifted when
> they happen to be present; whatever is left is kept for a collapsible block.

It lifts those lines into `text`, `tool`, `event`, `boundary` entries — the steps #300 built.

**The consequence, which is the strongest technical input to this decision:** a terminal byte stream
parses as none of those. Every line would fall to `kind: "raw"`, and cursor-addressing escapes would
make even the raw text misleading — a screen recording, not a transcript. So *attaching a human to the
agent's own process* costs the product the record it finished building last week, while *attaching a
human beside the agent in the same sandbox* costs it nothing: the agent's structured stream is
untouched and the human's shell is a separate stream.

ADR-0008 could not have weighed this — #130's transcript, and #299/#300's steps, did not exist on
2026-07-29.
