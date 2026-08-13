## Why

In self-host the operator already has the repository on disk and a git that is already logged in.
Adding a Project still makes them tell the product what the folder already knows — vendor, owner,
repository — and then mint a PAT to hand it one more. That is the first wall a new local user hits
([#347](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/347), UC-003, ACT-001).

**The decision this waited on has closed.** OPN-006 closed with `DEC-069` /
[ADR-0028](../../../docs/adr/0028-a-self-host-connector-may-authenticate-as-its-host-a-deployment-may-not.md):
a self-host Connector MAY authenticate as its host — through the machine's **git credential
helper**, per read, for **both reads and writes** — and a governed deployment MAY NOT.
[#348](https://github.com/asantamariaplainconcepts/ai-orchestrator/pull/348) wrote that into
`connector-configuration` as requirements (*"a credential path is offered only where it can
succeed"*) and shipped **no implementation behind them**. This change is what makes those
requirements true, and adds the capability that motivated them.

**Reconciliation, recorded rather than assumed.** #347's body predates DEC-069 and still reads
*"BLOCKED on #223 … until its `DEC-xxx` lands"*, and its criterion *"Saving still proves the
Connector works"* asks for live verification of every capability. `connector-configuration`
already answers that: a capability the vendor cannot answer without acting is reported **not
verifiable**, carrying its reason, and saving is allowed. Where the issue and the spec disagree the
spec governs (RULE-006 — locked decisions bind), and this proposal follows the spec. Nothing here
is decided by preference.

## What Changes

- **A folder is named where the Project is added.** `POST /api/projects` gains an optional absolute
  folder path. Its handler inspects the folder through the existing `ILocalCodeWorkspace` seam and
  the Project that results already carries its Connector coordinates and its `LocalFolder` code
  source. **No new HTTP surface**: `validate-path` stays project-scoped and unchanged, so no
  filesystem read becomes reachable without an existing Project to authorize against (BR-009).
- **The folder names the vendor.** `origin` is parsed for both vendors and both remote forms —
  GitHub (`owner/repo`) and Azure DevOps (`dev.azure.com/{org}/{project}/_git/{repo}` and
  `{org}.visualstudio.com/…`), SSH and HTTPS alike — filling Vendor, Owner, Repository and, for
  Azure DevOps, Code repository. Every derived field stays editable. A folder that fails any of the
  four checks (not a directory, not a repository, no `origin`, neither vendor) leaves the fields
  empty and names which check failed.
- **A self-host Connector may be saved with no credential at all.** A host-path Connector stores no
  token and no secret name, and nothing is written to the habitat's secret store for it.
  **BREAKING (requirement, not contract):** `connector-configuration` currently requires one of a
  token or a secret name when a project has no Connector; the host path becomes a third way to
  satisfy that rule in self-host only. No API contract, outbox schema, Aspire wiring or CI
  integration changes.
- **A host credential resolver** implements the git credential-helper protocol behind the seam every
  other resolution already uses, per read, non-interactively — a helper that would prompt fails with
  a stated reason rather than waiting, so no polling cycle stalls (UC-009). It never falls back to an
  empty or default credential.
- **What touched the vendor is reported** — the named secret, or the host's credential helper and the
  host it was asked about — borrowing `IAgentProcessHost.CredentialSource`'s shape (BR-014).
- **A governed deployment is unchanged**: the folder step is absent, the host path is not offered,
  and a credential is named exactly as today.

## Capabilities

### New Capabilities
- `local-folder-project`: adding a Project by naming a folder on this machine — folder inspection at
  create time, vendor and coordinate derivation from `origin` for both vendors and both remote
  forms, the four named failures, and the self-host-only posture of the whole step.

### Modified Capabilities
- `connector-configuration`: a self-host Connector may be configured with **neither** a token nor a
  secret name, resolving its credential from the host instead — the existing "neither or both"
  refusal gains its third, posture-gated path, and the host-path requirements written by #348 gain
  the scenarios that make them executable.
- `local-code-source`: the `LocalFolder` code source may be set when the Project is created, from
  the folder that was named, rather than only on the Connector form afterwards.

## Impact

- **Backend**: `CreateProject` (Projects) gains the optional folder and calls a new
  `IConnectorWriter` seam on `Backlog.Contracts` — the `IStoryWriter` / `IPromptDirectoryWriter`
  shape, because Projects cannot reach Backlog's internals (MOD001-005). New remote-parsing and
  host-credential types in `Infrastructure`; `ConfigureConnector` and `VerifyAccess` learn the
  no-credential path. EF migration for the Connector's credential-source column.
- **Frontend**: the add-Project form gains the folder input in the self-host posture only, gated by
  the deployment capabilities read (never by a client re-deriving posture), with derived fields
  shown editable. All copy through the typed i18n catalogue; kit primitives and Platform tokens
  only (DEC-051, `DESIGN.md`).
- **Tests**: functional coverage for both vendors × both remote forms, the four named failures, the
  cloud-absent posture, the non-interactive refusal, and the credential-source record.
- **Unchanged**: the three execution modes, dispatch, the outbox, BR-016's checkout rules, and every
  cloud path.
