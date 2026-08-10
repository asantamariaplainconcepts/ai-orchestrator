# ADR-0021: A developer's own machine may hold an agent session; a deployment may not

- **Status:** Accepted *(supersedes [ADR-0008](0008-a-live-conversation-costs-a-pass-per-message.md) in self-host; ADR-0008's conclusion stands unchanged for a deployment)*
- **Date:** 2026-08-11
- **Deciders:** repository owner (DEC-003); analysis by the agent working #301
- **Tags:** architecture, dispatch, cost, conversation, sandboxing

## Context

[ADR-0008](0008-a-live-conversation-costs-a-pass-per-message.md) decided that a live conversation
costs a pass per message, and rejected **alternative (b), a live session**, on three premises. Two
have since moved, and neither by this change:

- **DEC-013 — "nothing idles" — is superseded** (#296, 2026-08-09). The Azure Storage Queue and KEDA
  scaler whose cost model produced that phrase no longer exist; dispatch is a Postgres outbox
  consumed by the Server's own long-lived subscriber, in every habitat.
- **"Nothing idles" was already revised for containers**, twice. DEC-061 gave a portal conversation a
  warm container reclaimed after ten minutes of inactivity, accepting "bounded idling, by the pool's
  own cooldown". DEC-063 went further because Azure refuses `readySessionInstances = 0`: the pool
  holds **one session at 1 vCPU and 2 GiB, continuously**, whether or not anyone is talking.
- **BR-006 — human waits are untimed — is intact**, and is the only premise still standing. ADR-0008
  said so itself: *"BR-006 is what decides it."*

Two facts existed by 2026-08-10 that ADR-0008 could not have weighed:

**A self-host habitat.** Runs execute in sbx sandboxes on the machine owner's own hardware (#296,
#298). Holding one costs no money — the honest cost is 4 GiB of the owner's RAM per sandbox
(`SbxSandboxOptions.DefaultMemory`) and disk, and the measured failure mode is 31 abandoned sandboxes
and 125 GB when a process died before its `finally` ran (`SbxSandboxLifecycle.ReapAbandoned`).

**A measured transport.** A spike attached xterm.js to a Run's sandbox over a WebSocket and a
host-allocated pty. `sbx exec -it` refuses a redirected pipe outright; given a real tty it yields a
pty inside the sandbox, correct geometry, `^C` delivered as SIGINT, and full-screen programs
rendering. The spike measured feasibility and nothing else — not authentication, not audit, not what
a second writer does to the agent's working tree. It is preserved in
`openspec/changes/close-opn-007-live-agent-session/poc/`.

Judging the alternatives surfaced a distinction ADR-0008 never had to draw, because no sandbox
existed to attach to. "A human attaches" is **two** capabilities:

- **(2a) attaching to the agent's own process** — the human types into the agent's CLI, which
  requires running it interactively rather than headless;
- **(2b) attaching beside the agent** — the agent stays headless and the human gets a second shell in
  the same sandbox, sharing the workspace but not the agent's stdin.

They differ on nearly every criterion. Both runtimes are invoked headless with structured output
(`claude -p --output-format stream-json`; `opencode run`), and `transcript.ts` renders a Run's Output
from exactly that shape — *"a JSON object if it parses, text if it doesn't"*. A terminal byte stream
parses as none of its entry kinds, so under **2a** every line degrades to `kind: "raw"` and cursor
addressing makes even that misleading: a screen recording, not the transcript #299/#300 shipped.
**2a** also overloads BR-005, whose kill-on-timeout exists to bound the agent's work — a duration
that becomes human-paced once a person is typing into it.

## Decision

**We will permit both forms of attachment in the self-host habitat, and permit neither in a
deployment.**

Concretely:

- **Self-host (sbx on the machine owner's hardware):** a human MAY attach to a Run's sandbox beside
  the headless agent (2b), and MAY attach to the agent's own process (2a). Both are bounded by
  **inactivity of the machine**, in the shape DEC-061 already established — never by a deadline on
  the person, which BR-006 forbids.
- **Deployed (ACA):** neither is permitted. ADR-0008's conclusion stands unchanged there: a
  conversation costs a pass per message, and the portal answer box remains the way an agent's
  questions are answered.

**The habitat is the whole of the difference, and it is deliberate.** A self-host Run executes on
hardware its operator already owns, where holding a sandbox trades their own RAM for their own
latency and no third party is billed. A deployed Run executes on metered infrastructure shared
across a tenant, where the same affordance converts an untimed human wait into unbounded spend that
someone else pays. ADR-0008's cost argument was always a *deployment* argument; this ADR says so, and
declines to let a constraint that belongs to one substrate govern the other.

This is recorded as a decision the owner made against the analysis's own recommendation, which was to
permit 2b in both habitats and 2a in neither. That recommendation and its reasoning are preserved in
the change's `evidence.md`, so a future reader can weigh the road not taken rather than infer it.

## Consequences

- **Positive:** a developer running the product on their own machine gets the interaction its agents
  were built for — a real terminal, with signals and full-screen programs — instead of a comment
  round-trip through the vendor's UI. The dev-loop grill stops being a ticket queue.
- **Positive:** no deployed cost changes. The metered habitat keeps the shape DEC-055 chose, so the
  spend model already measured stays the one in force.
- **Positive:** BR-006 survives intact in both habitats: the bound is the machine's inactivity, and a
  human who steps away returns to a Run that is still waiting.
- **Negative — the same Automation now produces different records in different habitats.** A Run
  attached to under 2a yields a terminal byte stream where a deployed Run yields a structured
  transcript. `transcript.ts` will render the former as `kind: "raw"` lines, which is honest but much
  poorer, and the Output surface #299/#300 built does not apply to it. **This is the largest accepted
  cost of this decision**, and it is accepted knowingly.
- **Negative — BR-005 needs a stated rule it does not have.** With 2a permitted locally, the agent's
  kill-on-timeout can no longer mean simply "the agent's work took too long", because the work is
  human-paced while someone is attached. The follow-on capability MUST state how BR-005 applies to an
  attached agent — the obvious candidate being that the timeout governs unattended work and is
  suspended while a human is attached — and until it does, 2a is specified but not implementable.
- **Negative — a held sandbox inherits a measured leak.** 31 sandboxes and 125 GB happened because a
  `finally` never ran. Anything that deliberately holds a sandbox open while a human is away must name
  its reaper; today that is the startup sweep claiming the `aio-*` namespace, and it must keep working
  when a sandbox is held on purpose rather than abandoned.
- **Negative — a second writer in the agent's workspace is now possible.** A human running
  `git checkout` in a sandbox whose agent is mid-Run is not a transport problem and no pty mechanic
  prevents it. The follow-on capability must say whether the Run's outcome remains attributable to the
  agent alone.
- **Neutral — authorization and audit are unbuilt and are the gating risk.** A shell in a Run's
  sandbox is arbitrary command execution against a machine carrying the owner's own session (#288). It
  needs a grant of its own rather than `RunPermissions.Read`, checked where the surface can see it —
  the lesson `RunLogHub` already learned when a hub that dispatched nothing had to authorize itself.
- **Neutral — the portal answer box (ADR-0008's own follow-up) is still worth building**, and is now
  the *only* answer path in a deployment rather than merely the preferred one.
- **Neutral — if the habitat split proves confusing in practice**, this ADR is superseded rather than
  amended, and the analysis in `evidence.md` is where that conversation restarts.

## Alternatives considered

- **Reaffirm ADR-0008 in both habitats** — rejected because its cost premise was a deployment premise,
  and two of the three pillars it rested on have since moved. It would also leave a stuck local Run
  with no escape hatch but a re-run.
- **Permit 2b in both habitats and 2a in neither** *(the analysis's recommendation)* — rejected by the
  owner in favour of the habitat split. It scored best on every criterion — transcript and BR-005
  intact everywhere, one rule to explain — but it withholds from a developer on their own hardware an
  interaction that costs that hardware's owner nothing but their own RAM.
- **Permit 2a everywhere** — rejected: it converts every Run's record into a screen recording and
  overloads BR-005 in the metered habitat too, where the spend it makes unbounded is not the operator's
  own.
- **A session bounded by a reply deadline** — rejected outright, as ADR-0008 rejected it: a clock on
  the person contradicts BR-006, and weakening "a person is not a resource we hurry" to buy latency is
  a trade that is hard to reverse once a timeout exists.

## References

- Supersedes in self-host: [ADR-0008](0008-a-live-conversation-costs-a-pass-per-message.md)
- Closes: OPN-007 ([07-open-decisions.md](../product/mvp/07-open-decisions.md))
- Issue: [#301](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/301)
- Change: `openspec/changes/close-opn-007-live-agent-session/` — analysis in `evidence.md`, spike in `poc/`
- Related decisions: DEC-013 (superseded, #296), DEC-055, DEC-061, DEC-063, DEC-030, DEC-049
- Related rules: BR-001, BR-005, BR-006
