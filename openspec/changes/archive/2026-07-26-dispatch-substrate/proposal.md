# Proposal: dispatch-substrate

## Why

Issue #16, split from #9. Everything the product does after a Story is matched depends on one
mechanical fact: a message on a queue reliably becomes a container that runs. Nothing proves that
today. Run semantics (#17) and the Agent runtime (#18) both sit on top of it, and building either
against an unproven substrate means debugging two unknowns at once.

DEC-013 locked Azure Storage Queue as the substrate, with Azurite standing in locally so the
queue contract is exercised rather than mocked. #8 built the environment this needs.

**The grill found a conflict worth stating first.** Storage Queue redelivery — a message becoming
visible again when its consumer dies — *is* an automatic retry, and [BR-004](../../../docs/product/mvp/05-business-rules.md)
forbids those outright: a `Failed` Run is terminal and only a human re-triggers it. The
substrate's default behaviour would have violated a locked rule silently. This change resolves it
deliberately rather than discovering it later.

## What changes

- **The queue, and its one rule.** A dispatch queue in the existing storage account, and a
  consumer contract that **deletes the message the moment it claims it** (design D1). A crashed
  job therefore never re-dispatches: the Run ends `Failed` and BR-004 holds literally.
- **The message is a Run id and nothing else** (design D2). The job reads Run, Story and
  Automation from Postgres — one source of truth, no staleness, no payload that grows with every
  new field. The job consequently needs database access, which this slice provides.
- **A KEDA-scaled Container Apps Job**, triggered by queue length, with **its own user-assigned
  identity** (design D3) — separate from the portal's, because Agent jobs will clone
  repositories with project PATs and that is a far wider blast radius than a web host's.
- **The local path is real, not simulated:** Azurite already runs in the AppHost; the same queue
  client code runs against it, so the contract is exercised on every developer machine.
- **A dispatcher seam** the Run-creation slice (#17) will call — `IRunDispatcher` with one method
  — plus the test harness that enqueues without it, so this change can be proven end to end
  before any Run semantics exist.

## Impact

- `infra/dev/`: queue, a second user-assigned identity with its own grants, the KEDA job.
- A new `AiOrchestrator.Modules.Runs`-adjacent seam is **not** created here — the dispatcher
  interface lives in BuildingBlocks with the Azure implementation in ServiceDefaults, matching
  where the secret seam landed and keeping modules free of cloud SDKs.
- Specs: a new `run-dispatch` capability.
- **Cost:** the job scales to zero and bills only while running; the queue is fractions of a cent.

## What this deliberately does not do

Matching a story event and creating a Run (#17). The runtime seam and the Claude Code image
(#18). Cancellation (#23). Concurrency beyond one job in dev — BR-002's project cap is enforced
where Runs are created, not by the scaler.
