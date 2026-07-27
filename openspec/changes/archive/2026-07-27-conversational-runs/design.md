# Design: conversational-runs

## D1 — Waiting is the approval gate's shape, not a live container

`AwaitInput` stores when and transitions; the container exits; `Resume` puts the Run back in
`Queued` exactly as `Approve` does, and the existing dispatch path re-runs it. Nothing idles, KEDA
still scales to zero, and BR-005's timeout never has to learn about waiting because a waiting Run
is not executing anything.

Rejected: keeping the container alive polling for answers. It burns container-hours against a
human's calendar, and BR-005 would need a carve-out that swallows the exact hangs it exists to
catch.

## D2 — The marker, not the author, distinguishes question from answer

DEC-030 gives the whole project one PAT: the agent's comments and the owner's can share a vendor
account. So the agent's questions carry `<!-- aio:run:<id> -->` (invisible in every vendor's
rendering), and the resume condition is "a comment newer than the questions, without the marker".
This also makes AC3 structural — the agent's own comment can never resume its Run, whoever posted
it.

## D3 — Resume is a poll over waiting Runs, not a webhook

Comments are not mirrored (BR-008), so nothing in the mirror signals an answer. The resume check
runs on the existing polling cadence: for each `AwaitingInput` Run, read comments since the
questions were posted and resume on the first unmarked one. Runs waiting on humans are few and
the read is one page of comments — the poll is cheap where it matters. A webhook path can arrive
later as pure latency; it would call the same resume.

## D4 — The conversation is rebuilt, never stored

The resumed pass re-fetches the Story body and reads the comments through the seam. Nothing about
the conversation is persisted in the Runs schema beyond `WaitingSince`: a stored transcript would
be a second copy of vendor truth (BR-008), stale the moment someone edits a comment.

## D5 — The state is active for BR-001, and that changes the index

`RunStates.Active` is the single source of the BR-001 partial unique index filter. Adding
`AwaitingInput` to it is the point — a Story mid-conversation must not start a second Run — and
it means the migration regenerates the index, which is a schema change, not just an enum value.
