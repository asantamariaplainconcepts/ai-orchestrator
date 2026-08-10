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

## 2 — The three shapes, judged against the same criteria

### The distinction that emerged while judging (task 2.4)

Shape 2 is not one shape. "A human attaches" splits into two capabilities with almost nothing in
common but the transport:

- **2a — attach to the agent's own process.** The human types into the agent's CLI. Requires the
  agent to run interactively rather than headless.
- **2b — attach beside the agent, in its sandbox.** The agent stays headless; the human gets a second
  shell in the same microVM, sharing the workspace but not the agent's stdin.

Every criterion below separates them, so they are judged separately. The spike built **2b** — `top`
showed the agent's process and the human's `bash` side by side in one sandbox.

### The table

| Criterion | 1 — reaffirm (pass per message) | 2a — into the agent | 2b — beside the agent | 3 — split by habitat |
|---|---|---|---|---|
| **BR-006** untimed human wait | Honoured; nothing is held | Honoured only with an inactivity bound | Honoured with the same bound | Same as whichever shape it permits |
| **BR-005** agent kill-on-timeout | Unchanged | **Breaks its meaning** — the agent's work is now human-paced, so one timeout governs two different things | Unchanged: the agent's own exec still times out; the human's shell is not the agent's work | — |
| **BR-001** waiting blocks the Story | Unchanged | Unchanged | Unchanged | — |
| **Transcript integrity** | Intact | **Destroyed** — every line falls to `kind: "raw"` (§1.4) | Intact; the human's shell is a separate stream | — |
| **Cost, deployed** | One pass per message, growing with thread length | Increment on a pool already paid for (DEC-063) | Same increment | The point of the split |
| **Cost, self-host** | Nothing held | 4g RAM per held sandbox | Same | The point of the split |
| **DEC-030** credential boundary | Untouched | Preserved by the DEC-061 shape (one session, one container, one PAT) | Preserved | — |
| **Durability** | Survives restarts, days, a closed laptop; the answer is a Story comment, so it is auditable by construction | Dies with the socket; nothing recorded unless the bytes are | Dies with the socket | — |
| **Latency for a 12-round grill** | 12 × (resume trigger + cold start + pass) | Conversational | Not applicable — it answers no questions | — |

### 2.1 — Shape 1, reaffirm

Costs the Member, concretely: a dozen-round grill is a dozen full passes, each re-reading the whole
thread, so token cost grows with the conversation's length rather than with the last message —
ADR-0008 already stated this as its accepted negative. Latency is the sum of the resume trigger, a
cold start and a pass. What it buys is the thing no session offers: the exchange survives everything,
and the answer lands in the vendor's own record where it is auditable without new machinery.

### 2.2 — Shape 2, attached session

**2a fails on two criteria, and one of them is fatal.** It destroys the transcript (§1.4), and it
overloads BR-005: kill-on-timeout exists to bound *the agent's work*, and an interactive session makes
that duration human-paced, so either the timeout starts hurrying a person (violating BR-006) or it
stops bounding the agent. There is no version of 2a where both rules keep their current meanings.

**2b passes every criterion.** The agent stays headless, so BR-005 and the transcript are untouched;
the human's shell is a separate process with its own stream. Its bound is DEC-061's, already accepted
and already specified for conversations: reclaim on inactivity, which times the container and not the
person.

**What 2b does not do:** it does not answer the agent's questions. A human in a shell beside a waiting
Run can fix a dependency, resolve a conflict, read a log or re-run a failing test — but the agent is
still parked in `AwaitingInput`, and resuming it still costs a pass. **2b is not a substitute for
shape 1; it is orthogonal to it.**

One consequence worth stating rather than discovering: **2a degrades into shape 1 at the cooldown
boundary anyway.** A reclaimed container loses the agent's in-memory context, so a human who returns
after the cooldown resumes with a fresh pass — which is shape 1 with extra machinery.

### 2.3 — Shape 3, split by habitat

Coherent, and weaker than it looks. The cost asymmetry that would motivate it is smaller than
ADR-0008's framing implies: a deployment already pays for a continuously idle session at 1 vCPU and
2 GiB (DEC-063), so a held session is an increment on a paid pool rather than a new class of spend. A
split would also mean one Automation behaves differently on two substrates — a grill that is
interactive on a laptop and comment-driven in the cloud is two products to explain. Acceptable **only**
if the deployed answer is a hard refusal on a ground other than cost; on cost alone it is not
justified.

### 2.5 — What the analysis recommends

**Reaffirm ADR-0008 for the conversation, and narrow it explicitly.** ADR-0008 answered "how does a
human converse with an agent" and its answer holds: a pass per message, because 2a cannot keep BR-005
and the transcript. What ADR-0008 never had to draw — because no sandbox existed to attach to — is the
boundary between *holding the agent's process* and *a human working beside a headless agent*. 2b sits
on the far side of that line, breaks no rule, and is what the spike measured.

So the recommended outcome is: **no** to a live agent session, **yes** to a human attaching to a Run's
sandbox beside the agent, one rule for both habitats, bounded by inactivity as in DEC-061 — and
shape 1's portal answer box remains the way an agent's questions get answered.
