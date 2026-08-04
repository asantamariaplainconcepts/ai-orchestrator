## Context

Dispatch today has two substrates behind one one-method seam: a queue (ACA: KEDA scales the
worker job) and a Postgres outbox with an in-process consumer (#225: dev loop and compose). The
outbox's transport is in-memory, so its consumer must live in the publishing process — which is
why compose cannot simply run a worker container beside the Server. The pod-per-Run substrate
keeps the outbox as the durable half and replaces only the *execution locus*: the in-process
consumer, instead of calling the executor directly, starts a container that does.

## Goals / Non-Goals

**Goals:**
- The compose self-host executes Runs, isolated one per container, capped.
- The dev loop can opt into the same isolation without changing its default.
- Every refusal is named at the point a person can fix it: no socket, no image, cap reached.

**Non-Goals:**
- ACA changes — the queue + KEDA path is untouched.
- Local locus in pods (#247 declared it out; two pods over one working copy is the hazard).
- A queue in compose (rejected at #225).
- Registry/pull management beyond naming the image to run.

## Decisions

**D1 — the pod substrate composes ON TOP of the outbox, not beside it.** The dispatcher stays
`OutboxRunDispatcher`; what changes is the consumer: `AddRunDispatchConsumer` gains a mode where
the subscriber, instead of resolving `IRunExecutor`, hands the Run id to a `PodRunLauncher`. The
outbox remains the crash story (BR-004: accepted-then-lost is redelivered after restart), and the
substrate choice stays in composition. *Alternative rejected:* a third `IRunDispatcher` that
bypasses CAP — it would need its own durability story, and the outbox already has the right one.

**D2 — opt-in by configuration presence (ADR-0010, DEC-054).** `Dispatch:PodImage` names the
worker image; its presence selects the pod launcher, its absence keeps in-process execution.
The compose sets it; the dev loop may. Presence of configuration, never an environment name —
the same rule the queue/outbox split already follows.

**D3 — the socket is the operator's grant, and its absence refuses by name.** The launcher talks
to the docker CLI (`docker run`), which reaches whatever socket the operator mounted. No socket
or no image → the Run FAILS with the sentence naming what is missing — never a hang, never a
silent fallback to in-process, because a fallback would erase the isolation the operator asked
for without telling them.

**D4 — the per-Run entry mode exits 0 when execution completed.** The Run's state in the
database is the truth; a failed Run is a completed execution. Non-zero means the execution could
not happen (no database, unknown Run) — the same distinction the queue worker already draws, and
BR-004 forbids anything from retrying on it.

**D5 — sessions by default, observed before fixed.** The pod gets the host's CLI config mounted
read-only (`~/.config/opencode`, the Claude config dir) unless the operator disables it. The
first implementation task exercises a real CLI in a pod: if it needs to write (token refresh),
the mechanism switches to copy-in at start with no write-back, and the observation is recorded
in this design. Consequence stated plainly: pod Runs act and bill as the operator's sessions.

**D6 — the cap is a semaphore in the launcher, default 2.** Configurable
(`Dispatch:MaxConcurrentPods`). A dispatched Run past the cap WAITS (the outbox consumer holds
the message); delayed is not dropped. BR-001 already bounds per-Story; this bounds the host.

## Risks / Trade-offs

- [docker.sock in the Server container is root-equivalent] → granted explicitly in the operator's
  compose with the sentence saying so; never in the generated default? — no: the generated
  compose ships the mount commented out beside its warning, so `docker compose up` alone stays
  socket-less and Runs fail named until the operator uncomments it. Honest default over magic.
- [Session mount may leak more than intended] → read-only, named paths only, and off by one
  config key; the transcript names the credential source per Run (#244's criterion repeats it).
- [The image may not exist locally] → the refusal names the image and the pull command; no
  auto-pull, because a product that pulls images with root-equivalent access decides too much.
- [Two substrates in one consumer] → the mode lives in composition (D2); the subscriber itself
  stays one class with one seam (`IRunPodLauncher` faked in tests).

## Migration Plan

Additive. Without `Dispatch:PodImage` every habitat behaves exactly as today. The compose gains
the image name and the commented socket mount; ACA is untouched.

## Open Questions

(none — the grill on #246 closed them; observation tasks are tasks, not questions)
