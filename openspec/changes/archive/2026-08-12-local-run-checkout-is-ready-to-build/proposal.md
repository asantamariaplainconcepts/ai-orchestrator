## Why

A Local Run gets a checkout of its own ([#331](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/331),
`local-run-in-its-own-checkout`), and a fresh checkout cannot build: no `node_modules`, no build
outputs, no restored packages. An Agent told to implement a Story and make the tests pass
([UC-016](../../../docs/product/v1/04-capabilities.md)) meets a tree where the tests cannot run, so
the first honest *implement* Run on a Local source fails on setup instead of on the work. **This
repository has already hit exactly that**: a fresh worktree's first commit fails the pre-commit hook
because `src/frontend/node_modules` is absent.

[#332](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/332) closes that gap for
the Local lane: [ACT-001](../../../docs/product/v1/01-actors-and-responsibilities.md) Admin configures the setup command,
[ACT-002](../../../docs/product/v1/01-actors-and-responsibilities.md) Member is who it unblocks
([UC-012](../../../docs/product/v1/04-capabilities.md) *Run now* on a Local source,
[UC-027](../../../docs/product/v1/04-capabilities.md) watching the output while it executes). It is
sequenced immediately behind #331 and lands with it; #331's own design names this as the follow-on it
deliberately does not solve.

## What Changes

- **A Connector carries an optional setup command**, configured by an Admin **beside the code-source
  folder** and applicable to the local folder source only. Absent means absent: nothing runs.
- **The command runs to completion in the Run's own checkout, before the Agent starts** — after the
  checkout exists, before the runtime is invoked. Its output goes through the same log the Agent's
  does, ahead of it ([UC-027](../../../docs/product/v1/04-capabilities.md),
  [BR-014](../../../docs/product/v1/05-business-rules.md)).
- **A non-zero exit ends the Run `Failed` before the Agent runs**, naming the setup command and the
  tail of its output, and saying it was the setup — a reader must be able to tell it from an Agent
  failure. Nothing retries ([BR-004](../../../docs/product/v1/05-business-rules.md)).
- **Setup spends the phase's budget, not one of its own**
  ([BR-005](../../../docs/product/v1/05-business-rules.md)). The Automation's timeout starts when the
  phase's work starts; the Agent is invoked with what remains. Overrunning ends the Run naming the
  limit that fired, exactly as an overrunning Agent does.
- **The command is never read from a file in the repository.** A repository file that executes
  commands is [UC-031](../../../docs/product/v1/04-capabilities.md), and it needs per-version trust
  precisely because of what this lane is: on a Local Run the repository is the thing the Agent is
  editing, so an Agent could write the file in Run N and have Run N+1 execute it on the machine
  owner's own account, with their keychain and their push credentials. Keeping the command
  product-side means nothing the Agent writes can become a command, and no trust ceremony is needed.
  UC-031 remains a distinct capability; this slice is not a smaller version of it, and the corpus is
  amended to say so.

**Not changed, deliberately:** setup for sandboxed (sbx/ACA) Runs; caching or reusing build artifacts
between Runs — the checkout is prepared from scratch every time, and making that cheap is its own
work; anything the Agent itself does once it starts.

**No new privilege.** The command runs as the Server process's own user, on the machine that owns the
folder — the same user, environment and credentials the Agent process already runs with on this lane
(#331). No credential, boundary or permission moves. What *is* new is that an Admin's typed string is
executed, and the whole of D1 below is about why that string can only come from an Admin.

**BREAKING:** none. The Connector's new field is nullable and every existing Connector reads as "no
setup command" with no behaviour change. The outbox message schema, Aspire, the host csproj graph and
CI are untouched. One additive EF Core migration on the Backlog schema.

## Capabilities

### New Capabilities

None. This adds behaviour to existing capabilities; it introduces no capability that did not exist.

### Modified Capabilities

- `connector-configuration`: a Connector carries a setup command for the local folder code source —
  Admin-configured, collected beside the folder path, and cleared rather than merely hidden when the
  code source returns to `Repository` (the discipline the code-repository field already follows).
- `local-code-source`: a new requirement that a configured setup command runs to completion in the
  Run's checkout before the Agent starts — its output first in the log, a non-zero exit ending the
  Run by name before any Agent spend, absence running nothing, and the phase budget shared rather
  than doubled.
- `agent-execution`: the requirement *"the phase timeout ends the Run"* is amended. Its present
  sentence — *"the timeout clock is the runtime invocation only"* — stops being true: the clock
  starts with the phase's work, and where a Run prepares its checkout first, that preparation is
  inside the budget. Nothing about [BR-006](../../../docs/product/v1/05-business-rules.md)'s untimed
  human waits changes.

## Impact

- **Product docs:** [UC-031](../../../docs/product/v1/04-capabilities.md)'s entry gains one sentence
  distinguishing it from the product-side setup command this change introduces, so the corpus never
  reads as though a repository file were the only way a checkout gets prepared. **No business rule is
  amended:** BR-005 already bounds the `Executing` phase rather than the agent process, so making the
  implementation honest about that needs no change to the rule's text.
- **Code:** `Connector` (a nullable setup command, set and cleared with the code source),
  `ConfigureConnector` (request, response, validator), `IConnectorReader`/`ConnectorReader` so the
  Runs module can read it through Contracts, a new `ILocalCheckoutSetup` seam with a host
  implementation over the existing `HeadlessProcess`, and `RunExecutor`'s Local branch (run setup,
  hold the phase deadline, hand the Agent the remainder).
- **Frontend:** a setup-command input in `CodeSourceSection`, rendered only for the local folder,
  with its explanation beside it and new keys in the typed i18n catalog (DEC-009, DEC-021).
- **Persistence:** one additive migration on the Backlog schema — a nullable column. No data
  migration; every existing row means "no setup command".
- **Dependencies:** none added.
- **Tests:** functional coverage for the four acceptance paths (runs before the Agent, fails by name
  before any Agent spend, absence is not an error, overrun names the limit) and for the log ordering;
  the connector-configuration tests gain the clear-on-switch case.
- **Sequencing:** hard dependency on #331. There is no checkout to prepare until it lands, and this
  change's implementation rebases onto it.
