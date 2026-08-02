## Why

Issue #225. Self-hosting needs Docker and five containers of it — Postgres, Azurite, the portal,
the dispatch worker and the migration job. The queue buys the least locally and costs a whole
container: on a machine one person owns there is no KEDA, nothing to scale to zero, and no second
consumer. DEC-049 made self-hostability a product goal and said future infrastructure choices are
judged against "can a stranger with Docker still run it?" — five containers is a weak yes.

CAP is already in this repository doing this exact job for integration events: a Postgres outbox in
the `cap` schema, in-memory transport, retries chosen deliberately, transactional publish. Pointing
dispatch at the same substrate takes the local habitat from five containers to two, and it does so
without inventing a mechanism — the durability is the outbox, so BR-004's at-least-once survives a
crash exactly as it does for events.

## What Changes

- A **second `IRunDispatcher`** publishing through CAP, plus a subscriber that hands the Run id to
  the executor already composed in the Server.
- **The two are mutually exclusive by configuration** (ADR-0010's rule: a habitat contract is
  asked, never inferred). A queue connection string composes the Storage Queue dispatcher and **no
  CAP consumer**; its absence composes the CAP pair. Both configured, or neither, refuses at
  startup naming the ambiguity.
- **BREAKING for nothing deployed:** the deployed habitat is bit-for-bit unchanged — it has a queue
  connection string, so it takes the same path it takes today.
- `ACT-003`'s description stops saying the Agent *is* a KEDA-scaled ACA Job; it is that where the
  habitat provides one.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `run-dispatch`: the substrate becomes habitat-dependent — one contract, two compositions, chosen
  by configuration presence and refusing ambiguity — with the local one's credential consequence
  stated rather than discovered.

## Impact

- **Backend**: a CAP-backed `IRunDispatcher` and its subscriber in ServiceDefaults beside the queue
  pair; `DispatchComposition` gains the choice and its refusal. The Server composes the consumer
  only in the local habitat. No module changes: `IRunDispatcher` is unchanged, and the executor is
  already composed.
- **Docs**: a DEC recording the local habitat's loss of the worker/portal credential separation,
  and `09-foundation-vs-product-split.md`'s queue entry gaining the alternative local path.
- **Unchanged**: BR-001, BR-002 (database-enforced, habitat-independent), BR-004, BR-014, the
  Storage Queue path, the KEDA job, and CAP's transport for integration events.
- **Stated consequence**: choosing CAP couples the local habitat to Postgres — CAP has no SQLite
  storage provider — so a future embedded database is constrained by this choice. Recorded, not
  solved here.
