# Design — dispatch-substrate

## D1 — Delete on receive: BR-004 beats at-least-once

Storage Queues are at-least-once by construction. A consumer that dies before deleting its
message leaves it to reappear, and KEDA starts another job — an automatic retry, which BR-004
forbids. The two cannot both be honoured, so the rule wins: **the job deletes the message as soon
as it claims it**, before doing any work.

The cost is stated plainly rather than hidden. A job killed by infrastructure (node eviction, an
image that will not start) is now indistinguishable from an Agent that failed: both end the Run
`Failed`, and both need a human. That is the same trade BR-004 already makes everywhere else, and
the product already supplies the remedy — *Run now* (BR-013) re-dispatches deliberately.

**Rejected:** allowing one redelivery for "infrastructure faults". It sounds more faithful to
intent, but nothing at the queue can tell an infrastructure fault from an Agent crash — the
distinction would have to be inferred, and an inferred exception to a locked rule is how rules
stop meaning anything. **Also rejected:** switching to Service Bus for native dead-lettering;
that contradicts DEC-013 and would need the decision reopened, not worked around.

## D2 — The message carries a Run id and nothing else

The job reads Run, Story and Automation from Postgres. One source of truth, no staleness between
enqueue and execution, and a message that does not grow every time the Run model gains a field.

The cost is that the job needs database access — an extra grant, and a dependency on the schema
from a second process. Accepted: the alternative is a payload that can already be wrong by the
time it runs, in a system where "the Story changed while queued" is an ordinary event.

## D3a — The scaler needs a connection string; the workload does not

**Amended during implementation.** D3 assumed the scaler could authenticate with the job's
managed identity. It cannot: the `azure-queue` scaler on Container Apps *jobs* has no
identity-based auth, and KEDA reports `error parsing azure queue metadata: no connection setting
given`. Microsoft's own event-driven jobs tutorial uses
`--scale-rule-auth "connection=<secret>"` for this reason.

Two wrong shapes were deployed before the right one, and **both were accepted by ARM, rendered
correctly by `az containerapp job show`, and silently never fired.** A message sat on the queue
for the better part of an hour with zero executions. Nothing static could have caught it — only
putting a message in and watching nothing come out.

The key is confined as tightly as the platform allows: stored in Key Vault, referenced by the job
as a secret rather than a literal, used only by the scaler. The workload still reaches the queue
with its managed identity, so no credential appears in the container's configuration and nothing
in the application handles one.

## D3 — Agent jobs get their own identity

The portal's workload identity reads two secrets and pulls images. An Agent job will clone
customer repositories with project PATs. Sharing one identity would mean a compromise of either
reaches both, and it would grow the web host's entitlements every time the Agent needs something.

Separate user-assigned identity, separate vault grants, same registry pull. This is also what
makes #18's per-project PAT access expressible without touching the portal at all.

## D4 — The dispatcher is a seam now, with one consumer later

`IRunDispatcher` (one method: dispatch a Run id) lives in BuildingBlocks; the Storage Queue
implementation lives in ServiceDefaults beside the Key Vault resolver. Same reasoning as the
secret seam: modules reference BuildingBlocks, and no module may reach a cloud SDK.

This is a seam with **zero production consumers** in this change, which RULE-007 normally rejects
as speculative. It earns its place because the test harness *is* a consumer: the acceptance
criteria drive it end to end, so the interface is exercised rather than imagined. #17 becomes a
call site, not a rewrite.

## D5 — KEDA scales on queue length, the cap lives elsewhere

The scaler runs one job per message, up to a small ceiling. BR-002's per-project concurrency cap
is **not** enforced here: the queue has no notion of a project, and a scaler that tried would be
a second, hidden place where the cap lives. Runs beyond the cap are never enqueued in the first
place — that is #17's responsibility, where the Run is created and the cap is known.

## D6 — The operator needs data-plane access too

Owning a subscription grants nothing on the data plane. Terraform gives the human running it
`Key Vault Secrets Officer` so it can write secrets; the queue needed the same treatment, and did
not have it — the first attempt to enqueue a test message failed on permissions.

It is worth stating why that matters beyond convenience: a substrate nobody can put a message
into by hand is a substrate nobody can verify. The grant exists so the acceptance criteria are
executable, not so operators can poke at production.

## Local parity

Azurite already runs in the AppHost. The same `QueueClient` code runs against it, so the enqueue
→ dequeue → delete contract is exercised on every developer machine and in the functional tier.
KEDA itself has no local equivalent; the tasks say so explicitly rather than pretending the local
path proves the scaler.
