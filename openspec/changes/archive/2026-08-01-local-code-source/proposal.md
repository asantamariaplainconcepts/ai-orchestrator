# Local code source — proposal

## Why

Self-hostability is a product goal (DEC-049), but today every Run clones a remote repository and
opens a PR — a self-hoster whose code already sits on the machine running the orchestrator pays a
clone, a credential dance and a PR round-trip to see an Agent touch their own working copy. Issue
#210 (from the 2026-08 design review, mocks 3a–3c) makes the folder itself a code source and
records, per Run, where execution happened.

## What Changes

- A Connector separates **backlog vendor** from **code source**: `CodeSource = Repository`
  (default, today's behaviour, unchanged) `| LocalFolder` with an absolute `LocalPath` on the
  host. Stories still come from the vendor; only the Agent's working copy changes.
- A posture-gated validation endpoint (`POST /api/projects/{id}/connector/validate-path`) answers
  `{isDirectory, isGitRepository, branch, isClean}` — self-host posture and project Admins only; a
  cloud deployment 404s the whole code-source surface.
- A Run records its **execution locus** (`Pod | Local`), working folder and created branch
  (BR-014 extension), exposed on the runs read model. `Run now` accepts an optional locus;
  matching-created Runs take the project default (LocalFolder → Local, Repository → Pod).
- A `LocalFolderWorkspace` sibling behind the existing `ICodeWorkspace` seam: verify clean tree,
  create `ai/{storyId}-{slug}`, hand the folder to the runtime, commit — never push, never open a
  PR. Local runs use the host CLI's own credentials; the log says so in one line.
- **New business rule** (recorded in `docs/product/mvp/05-business-rules.md` as BR-016): a Local
  run requires a clean working tree; dispatch refuses a dirty tree before any write, naming the
  folder.
- Not breaking: the dispatch queue message schema, Aspire wiring, host csproj set and CI are all
  unchanged — locus is a workspace decision inside the worker, not a routing one.

## Capabilities

### New Capabilities

- `local-code-source`: a project's code may come from a folder on the orchestrator's host —
  configuration, posture gating, path validation, and the local workspace's branch-only output.

### Modified Capabilities

- `connector-configuration`: the Connector gains the code-source axis (kind + path) and its
  validation surface; reconfiguration and credential semantics unchanged.
- `run-orchestration`: a Run carries an execution locus chosen at creation (Run now parameter or
  project default) and refuses Local dispatch on a dirty tree; BR-001/BR-002/BR-013 unchanged.
- `agent-execution`: the executor selects the workspace per Run by locus behind `ICodeWorkspace`;
  Local output is a branch (no push, no PR), and audit fields extend accordingly (BR-014).

## Impact

- **Backend**: Backlog module (Connector domain + migration + configure/validate use cases),
  Runs module (Run domain + migration, RunNow, RunCreator, RunExecutor, read models),
  ServiceDefaults (`LocalFolderWorkspace`), Server composition (posture switch).
- **API**: `ConfigureConnector` request widened; new validate-path endpoint; runs read model and
  `RunNow` request gain locus fields. All additive.
- **Frontend**: none in this change — #211 consumes it (`features/inbox`-style read-model types
  updated there, not here).
- **Docs**: BR-016 recorded; UC-004/UC-011/UC-012/UC-016/UC-021 extended in place.
- **Traceability**: realises issue #210; upholds BR-001, BR-002, BR-005, BR-013, BR-014,
  DEC-049, DEC-052; actors ACT-001 (configures), ACT-003 (executes).
