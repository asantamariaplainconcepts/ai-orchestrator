# Tasks — dispatch-substrate

The contract first, locally against Azurite, then the cloud scaler. Every claim about
infrastructure is checked by reading the artifact back, never by a command's exit code
(ADR-0004); anything not yet verified stays written as a hypothesis (ADR-0005).

## 1. The dispatch seam

- [ ] 1.1 `IRunDispatcher` in BuildingBlocks — one method, a Run id, product vocabulary only.
      No queue type in the signature.
- [ ] 1.2 The Storage Queue implementation in ServiceDefaults beside the Key Vault resolver, so
      modules stay free of cloud SDKs. Composed by the host from the existing `queues`
      connection.
- [ ] 1.3 Verify: no module references an Azure queue package, transitively included — the same
      `dotnet list package --include-transitive` check #8 used, not a reading of the csproj.

## 2. The consumer, and BR-004

- [ ] 2.1 A `AiOrchestrator.DispatchWorker` executable: claim one message, **delete it before any
      work**, resolve the Run id, exit. It does nothing with the Run yet — #17 gives it meaning.
- [ ] 2.2 Verify against Azurite that delete-on-receive holds: kill the worker mid-work and
      confirm the message does **not** reappear and no second execution occurs. This is the
      acceptance test for BR-004 in the substrate; a passing enqueue proves nothing about it.
- [ ] 2.3 A functional test over the real Azurite container (never a mock): enqueue → the worker
      claims → the message is gone → the id it read matches what was sent.

## 3. Infrastructure

- [ ] 3.1 `infra/dev/`: the dispatch queue in the existing storage account.
- [ ] 3.2 A second user-assigned identity for jobs, with its own grants — registry pull, Key
      Vault secrets read, queue data access. Created and granted **before** the job that uses it
      (the deadlock #8 hit with a system-assigned identity — design D9 there).
- [ ] 3.3 The KEDA-scaled Container Apps Job: `azure-queue` scaler on the dispatch queue,
      authenticating with that identity, scale-to-zero, a small max.
- [ ] 3.4 Database access for the job (the Run id is a key, not a payload — design D2).
- [ ] 3.5 Verify by artifact: `az containerapp job show` reports the scale rule; the job's env
      carries a vault URI and secret names only; the two identities are different principals with
      different role assignments.

## 4. End to end

- [ ] 4.1 A harness that enqueues a Run id against the deployed queue.
- [ ] 4.2 Verify: a job execution starts within a reasonable window, reaches `Succeeded`, and its
      logs show the id it received. Read the execution back — a green enqueue is not evidence a
      job ran.
- [ ] 4.3 Verify the empty case: with the queue drained, no replicas run.
- [ ] 4.4 **Record what the local path cannot prove.** Azurite exercises the queue contract;
      KEDA has no local equivalent, so the scaler is only ever verified in Azure. Say so in the
      infra README rather than letting a green functional suite imply otherwise.

## 5. Close-out

- [ ] 5.1 `ARCHITECTURE.md`: dispatch topology, and why redelivery is disabled by deletion —
      the next reader will otherwise "fix" it back to at-least-once.
- [ ] 5.2 Full verify sweep; CI green.
