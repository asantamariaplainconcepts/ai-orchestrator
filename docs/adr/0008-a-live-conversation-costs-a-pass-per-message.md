# ADR-0008: A live conversation costs a pass per message, because an untimed wait cannot idle

- **Status:** Accepted
- **Date:** 2026-07-29
- **Deciders:** repository owner (DEC-003); analysis by the agent working #149
- **Tags:** architecture, dispatch, cost, conversation

## Context

The grill made the product conversational (UC-024, #78): the agent asks its questions on the Story,
a human answers there, and matching resumes the Run. It works, and it is slow in a way people feel —
every exchange costs a full agent pass and a trip to the vendor's own interface.

The obvious product wish is to discuss a Story with the agent live, from the portal. It could not be
sliced, because it rests on three constraints that were never reconciled:

- **DEC-013 — nothing idles.** Dispatch is an Azure Storage Queue with a KEDA scaler: a job starts,
  drains the queue, and exits. There is no long-lived process by design, and the cost model follows
  from that.
- **BR-006 — human waits are untimed.** `AwaitingApproval`, `AwaitingInput` and `Queued` count
  toward no timeout. A Run may wait on a person indefinitely, which is correct: a person is not a
  resource the product may hurry.
- **DEC-050 deferred the portal→agent direction.** Output flows out to watchers; nothing flows in.
  The stated reason was OPN-002 — the portal authenticates nobody, so an ingest surface is a surface
  anyone reachable can drive.

Those three are individually right and jointly decide the question, which is why the decision had to
be made before any capability could be sliced.

## Decision

We will implement live conversation as **a pass per message** over the existing resume loop, and we
will not keep a process alive for a conversation.

Concretely: the portal gains an answer box that writes a comment through the Connector, the existing
`AwaitingInput` resume path picks it up, and the agent's next questions arrive as they do today. No
session runtime, no new liveness concept, no change to DEC-013.

**BR-006 is what decides it.** A live session is a paid process waiting on a human, and a human wait
has no bound — so a session's cost has no bound either. The only way to bound it is a session
timeout, which either contradicts BR-006 or introduces a second, competing notion of how long a
person may take. Weakening "a person is not a resource we hurry" to buy latency is a worse trade than
paying for a pass, and it is the kind of trade that is hard to reverse once a timeout exists.

## Consequences

- **Positive:** nothing about dispatch changes, so the cost model stays the one already understood
  and measured. No idle spend is possible, because nothing idles. BR-005 and BR-006 keep their
  current meanings. The auth exposure added is the same class the board already has — writing to the
  vendor on the project's behalf — rather than a new inbound channel, which is what DEC-050 was
  worried about.
- **Negative:** each message pays a full pass, and each pass re-reads the thread, so token cost grows
  with the conversation's length rather than with the last message. A long exchange is expensive in a
  way a session would not be. Latency stays at the sum of the resume trigger, a cold start and a
  pass — better than today only by removing the trip to the vendor's UI, not by making the pass
  faster.
- **Neutral:** the first Connector **comment write** becomes a prerequisite. Today the seam reads
  comments and writes labels and state; posting a comment is new, licensed by UC-008's shape but not
  yet implemented. That is the follow-up named below.
- **Neutral:** if measurement later shows the per-pass cost dominating real use, this ADR is
  superseded rather than amended, and the analysis of (b) and (c) below is where that conversation
  restarts.

## Alternatives considered

- **(b) A live session — a container that stays alive for the conversation.** Rejected because it
  contradicts DEC-013 directly and, more importantly, because BR-006 leaves its cost unbounded: a
  replica waiting on a person who may take a week is a replica paid for a week. Bounding it needs a
  session timeout, which is a second timing rule beside BR-005/BR-006 and applies to the human rather
  than the work. It also needs a real portal→agent transport, whose ingest authentication is the
  thing DEC-050 deferred and OPN-002 still blocks — so it could not be built now even if the cost
  model were acceptable.
- **(c) A hybrid — a session only while a human is present, pass-per-message otherwise.** Rejected
  because "present" is a new liveness signal, and a liveness signal that can be wrong is exactly what
  #144's design D3 argued against when it chose a deadline over a heartbeat. A browser heartbeat
  would end sessions for a person who is reading rather than typing, and keep them for a tab left
  open. It inherits both cost models and both auth preconditions, and adds a third failure mode of
  its own. It is the option to revisit if (a) proves too slow *and* (b)'s auth precondition is
  removed by OPN-002 closing.
- **A timeboxed spike before deciding.** Not needed: the decisive constraint is BR-006 against a paid
  idle process, and that is an argument from rules already locked, not a measurement. A spike would
  tell us how fast a session feels, which is not the question that blocked the slice.

## Follow-up, deliberately not part of this decision

The capability — *a Member discusses a Story with the agent from the portal* — is to be grilled as
its own item, with the Connector comment write as its first task. This ADR delivers the decision and
nothing executable.

## References

- Related: [ADR-0001](0001-verify-claims-by-exercising-them.md) — the instinct that made the spike
  unnecessary here: the answer follows from rules already exercised, not from a new experiment.
- OPN-005 (closed by this decision) and DEC-055 in `docs/product/mvp/`.
- DEC-013 (dispatch), DEC-050 (live output, deferred ingest), BR-005, BR-006, BR-008, BR-010.
- #78 (the resume loop this extends), #149 (this decision's issue).
