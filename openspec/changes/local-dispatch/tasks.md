## 1. The second substrate

- [ ] 1.1 A CAP-backed `IRunDispatcher` beside the queue one in ServiceDefaults — publishes the
      Run id to the existing `cap` schema, no new schema and no new configuration
- [ ] 1.2 A subscriber that hands the id to `IRunExecutor`, composed by the host only (design D2)
      — registering the dispatcher must never register the consumer

## 2. The choice, and its refusal

- [ ] 2.1 `AddRunDispatch` selects by queue-connection-string presence (design D1)
- [ ] 2.2 Both configured, or neither, throws at startup naming which contract is ambiguous
      (ADR-0010's shape, and the message says what to set)
- [ ] 2.3 The dispatch worker keeps its reader and composes no consumer; the Server composes the
      consumer only in the queueless habitat

## 3. Proving the crash story

- [ ] 3.1 Functional test: dispatched in-process, host disposed before execution, restart
      redelivers and the Run reaches terminal
- [ ] 3.2 Functional test: a redelivery for a Run the reaper already terminated executes nothing
- [ ] 3.3 Functional test: the same Run lifecycle assertions the queue path makes, on this path
- [ ] 3.4 Composition tests: both-configured and neither-configured each refuse, naming the
      ambiguity

## 4. The habitat

- [ ] 4.1 AppHost composes the queueless shape locally — one fewer container — without changing
      the deployed template
- [ ] 4.2 `SELF-HOSTING.md` and the compose file reflect two containers rather than five where
      that is what they now describe

## 5. Truth in the docs

- [ ] 5.1 A DEC recording the local habitat's loss of the worker/portal credential separation, and
      the Postgres coupling CAP brings
- [ ] 5.2 `01-actors-and-responsibilities.md`: ACT-003 is a KEDA-scaled ACA Job **where the habitat
      provides one**, not by definition
- [ ] 5.3 `09-foundation-vs-product-split.md`'s queue entry gains the alternative local path

## 6. Proof

- [ ] 6.1 Run the local habitat end to end with no Azurite: label a Story, watch the Run reach a
      terminal state
- [ ] 6.2 Full gates — build, tests, lint, spec validation
