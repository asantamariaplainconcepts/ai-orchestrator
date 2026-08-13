## Why

[#222](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/222) established that the
Connector's token cannot be hidden in self-host, because the backlog is remote and the Connector
needs it. The owner asked whether that is *necessarily* so: a Local Run already skips vendor-credential
resolution and runs git with the host's own tooling
([`local-code-source`](../../specs/local-code-source/spec.md)), and
[`connector-configuration`](../../specs/connector-configuration/spec.md) already excuses a
local-folder project from the *code* capabilities on exactly that ground. If the host's own tooling
can be trusted for git transport, the same reasoning could extend to reading Stories — and a
self-host owner would configure a Connector without minting a PAT at all.

That is not a UI decision. It touches the Connector seam (all fourteen `IBacklogConnector` methods
take `string token`), `VerifyAccess` (what would it verify, and what would it tell the operator to
grant?), BR-010's *"resolved by name at the moment of use"*, and the second vendor, which has no
`gh` equivalent. RULE-002 says the decision goes first, so this change delivers a decision and
nothing executable, closing **OPN-006**
([#223](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/223)).

The precedent is already in this codebase, for the **Agent** rather than the Connector.
`AgentCredentialEnvironment.For` exports no vendor token when the process host does not supply
credentials, and states the reason as a rule: *"an exported empty variable SHADOWS whatever auth the
host's own tooling holds… #244 AC6 extended it to the vendor token, which a Local Run never
resolves"*. In local mode an agent already reaches the vendor as whatever the machine is logged in
as, by design. Whichever way this closes, the ADR must engage with that — *"we have never trusted the
host's identity"* is not an argument that survives
[`AgentCredentialEnvironment.cs`](../../../src/shared/AiOrchestrator.Infrastructure/Agents/AgentCredentialEnvironment.cs).

[#347](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/347) is blocked on this
decision and on nothing else, and needs vendor **writes** as well as reads.

## What Changes

- **Record OPN-006** in [`07-open-decisions.md`](../../../docs/product/mvp/07-open-decisions.md) —
  *whether a self-host backlog read can use the host's own credentials* — naming what it blocks
  (hiding the credential in self-host, and any host-derived vendor authentication) — and **close it
  in the same change**, the convention OPN-002, OPN-005 and OPN-007 all followed. The file's
  *"Still open: OPN-006"* paragraph goes with it.
- **Write an ADR** evaluating **four** options, each with its blast radius on the Connector seam, on
  `VerifyAccess`, and on BR-010's by-name resolution:
  - **(a) status quo** — a named credential in both postures;
  - **(b) delegate vendor reads to the host's `gh` CLI** in self-host, stating what happens on Azure
    DevOps, which has no equivalent;
  - **(c) a hybrid** where only reads delegate and writes still name a credential;
  - **(d) delegate to the machine's git credential helper** rather than to a vendor CLI — both
    vendors have one, so (b)'s asymmetry is claimed to disappear. The question this option must
    answer: whether a helper's output may authenticate vendor **API** calls (work-item reads, labels,
    comments, transitions) and not only git transport.
- **State plainly which options #347 eliminates.** #347 covers writes, so **(c) is eliminated by
  construction** — recorded as a finding, not left for the reader to infer.
- **State what a Run's log would say about which identity touched the vendor**, for whichever option
  wins — the same honesty the local-workspace rule already requires of git.
  `IAgentProcessHost.CredentialSource` already exists for this question on the agent's side (*"so the
  source is never left to inference"*) and is **named rather than reinvented**.
- **Land the outcome as `DEC-069`** in
  [`10-locked-mvp-decisions.md`](../../../docs/product/mvp/10-locked-mvp-decisions.md), with
  OPN-006 moved to that file's closed list — never edited in place.
- **Record the spec delta the decision produces.** Which text lands depends on the decision, and the
  decision is the work, so the delta is written when the ADR is — not guessed here.

Not a **BREAKING** change: no integration contract moves. The Aspire model, host csproj, outbox
message schema and CI are untouched. This change edits documents and spec requirement text; it ships
no code.

## Capabilities

### New Capabilities

None. This is a Foundation decision-closure item (RULE-006): its deliverable is a recorded decision,
not a user-visible capability. The capability it unblocks (#347) is a separate issue by RULE-002.

### Modified Capabilities

- `connector-configuration`: the requirements *"the credential is verified before the Connector is
  stored"* and *"the product states the permissions it needs"* gain the decided answer for a
  credential the operator did not mint through the product. Both requirements are written today
  around a credential supplied to a form — one says the product must state *"the names a person
  selects while minting a token"*, the other that a write is verified *"by asking the vendor what the
  credential may do, never by doing it"*. Neither can be satisfied unchanged by a host-derived
  credential, so this requirement text is where the decision has to land whichever way it goes.
- `connector-seam`: the seam's credential parameter gains an explicit statement of whether it is
  always a resolved secret value, or may name a resolution the host performs. Today every method
  takes `string token` and the absence of any other shape is implicit; this change makes it stated.

## Impact

- **Documents:** `docs/adr/0028-*.md` (number allocated against current `origin/main` per the
  [`decision-records`](../../specs/decision-records/spec.md) spec — 0027 is the highest, and none of
  the three open branches adds one), `docs/product/mvp/07-open-decisions.md`,
  `docs/product/mvp/10-locked-mvp-decisions.md`.
- **Specs:** delta files for `connector-configuration` and `connector-seam` as described above.
- **Code:** none in this change. A decision that permits delegation implies later work in
  `IBacklogConnector` and both its implementations, `ConfigureConnector`, `TestConnector`, and the
  polling loop — all out of scope here and carried by #347.
- **Business rules:** BR-010 is the rule under examination; BR-008 (the vendor is the source of
  truth) must keep its current meaning whichever way the decision goes.
- **Evidence carried in:** `evidence.md` records what was exercised for real rather than asserted —
  in particular the credential-helper protocol's actual output shape, which is what option (d)'s open
  question turns on.
