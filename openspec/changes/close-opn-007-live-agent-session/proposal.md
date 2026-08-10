## Why

A Member answering an agent mid-Run pays a full agent pass per exchange and answers through the
vendor's own interface ([DEC-055](../../../docs/product/mvp/10-locked-mvp-decisions.md) /
[ADR-0008](../../../docs/adr/0008-a-live-conversation-costs-a-pass-per-message.md)). A grill is a
dozen such exchanges, so UC-024 works in principle and is painful in practice — and no faster shape
can be proposed, because ADR-0008 explicitly rejected the alternative it would need. This change
makes that decision again, on evidence that did not exist when it was made, and records the outcome
as an ADR ([#301](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/301)).

ADR-0008 rejected **alternative (b), a live session**, on three pillars. Two have since moved:

- **DEC-013 — "nothing idles" — is superseded** (2026-08-09, #296). The Azure Storage Queue and KEDA
  scaler the argument rested on are gone; dispatch is a Postgres outbox consumed by the Server's own
  long-lived subscriber, in every habitat.
- **"Nothing idles" was already revised for conversations** by DEC-061: a portal conversation runs in
  a warm ACA session, reclaimed after ten minutes of inactivity — and DEC-060 (#198) went further,
  holding `readySessionInstances = 1` continuously because Azure refuses zero. Bounded idling is
  therefore already accepted, already specified, and already paid for.
- **BR-006 — human waits are untimed — still stands**, and is the pillar the decision now turns on.
  DEC-061 demonstrates a shape that satisfies it: an *inactivity* cooldown bounds the container
  without putting a clock on the person.

Two considerations ADR-0008 could not weigh:

- **A self-host habitat.** Runs execute in sbx sandboxes on the machine owner's own hardware
  (#296, #298), where holding one costs no per-hour money. ADR-0008 reasoned only about a paid
  replica, so its cost argument may simply not apply to one of two habitats.
- **Measured feasibility.** A spike on 2026-08-10 attached xterm.js to a Run's sandbox over a
  WebSocket and a host-allocated pty (`sbx exec -it` needs a tty on the host side; a redirected pipe
  is refused): a live shell, geometry matching the browser, `^C` delivered as SIGINT, and
  full-screen programs rendering correctly. The transport is not the open question — the cost and
  timing rules are.

ADR-0008 states it should be **superseded rather than amended** if the analysis of (b) is revisited.

## What Changes

- **Record OPN-007** in [`07-open-decisions.md`](../../../docs/product/mvp/07-open-decisions.md) —
  *whether a human may take the keyboard in a Run's own agent session* — naming what it blocks, and
  **close it in the same change**, which is the convention every prior OPN entry followed.
- **Write an ADR** that either supersedes ADR-0008 on alternative (b) or reaffirms it, states which
  of the three pillars its conclusion rests on, and names the bound on an attached session that is
  **not** a human-facing timeout — or records that no such bound exists and refuses on that ground.
- **Answer the habitat question explicitly**: one rule for both habitats, or two, and why. A decision
  that permits an attached session locally and refuses it in a deployment is a legitimate outcome and
  must be stated rather than left to inference.
- **Correct the stale index**: `07-open-decisions.md` currently asserts "None remain open" while #223
  is open as *Close OPN-006*. The file is edited by this change, so it is fixed here.
- **Record the spec delta the decision produces** — the requirement text that states how a human
  supplies input to a waiting Run, and whether a sandbox may outlive the agent's own process.
- **Open the follow-on capability issue**, sequenced behind #245 (which also touches the `Automation`
  aggregate), citing the ADR.

Not a **BREAKING** change: no integration contract moves. The Aspire model, host csproj, outbox
message schema and CI are untouched — this change edits documents and, at most, one spec's
requirement text.

## Capabilities

### New Capabilities

None. This is a Foundation decision-closure item (RULE-005): its deliverable is a recorded decision,
not a user-visible capability. The capability it unblocks is a separate issue by RULE-002.

### Modified Capabilities

Both deltas are the *statement of the decided rule*. Which text lands depends on the decision this
change makes, and the decision is the work — so the delta is written when the ADR is, not guessed
in this proposal.

- `run-orchestration`: the requirement *"a Run can wait for a human's answer on its Story and
  resume"* and *"a message costs exactly one agent pass, and the spend is visible"* gain an explicit
  statement of whether a human may instead attach to the Run's own agent process. Today the
  pass-per-message shape is specified; the absence of an attached session is implied by ADR-0008
  rather than stated in the spec, which is the gap this closes either way.
- `agent-sandboxing`: the requirement *"a sandbox is per Run and does not outlive it"* gains its
  edge — whether a sandbox may be held open while a human is attached to it, and what reclaims it if
  so. Reaffirming ADR-0008 leaves this requirement unchanged and says so; permitting a session
  requires it to name the cooldown.

## Impact

- **Documents:** `docs/adr/NNNN-*.md` (number allocated against current `origin/main` per the
  `decision-records` spec), `docs/product/mvp/07-open-decisions.md`,
  `docs/product/mvp/10-locked-mvp-decisions.md` (a `DEC-*` entry if the decision changes a locked
  one), and `ARCHITECTURE.md` if the runtime seam's stated shape changes.
- **Specs:** delta files for `run-orchestration` and `agent-sandboxing` as described above.
- **Code:** none in this change. A decision that permits an attached session implies later work in
  `IAgentProcessHost` (the seam that would hold an interactive process), the Runs module's
  observation surfaces, and the frontend — all out of scope here.
- **Business rules:** BR-006 is the pillar under examination; BR-005 (kill-on-timeout for the
  agent's own work) and BR-001 (a waiting Run still blocks its Story) must keep their current
  meanings whichever way the decision goes.
- **Prior art carried in:** the spike's harness and findings, to be copied into this change's `poc/`
  the way `2026-08-07-spike-sbx-sandbox` did, so the feasibility claim above is inspectable rather
  than asserted.
