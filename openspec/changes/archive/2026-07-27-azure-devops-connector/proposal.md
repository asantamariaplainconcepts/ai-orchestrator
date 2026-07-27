# Proposal: azure-devops-connector

## Why

Issue #29 (DEC-011's second vendor, RULE-004's sequencing). One implementation proves a seam
compiles; a second proves it is a seam. Azure DevOps is a genuinely different model — tags
rather than labels, a process-dependent state vocabulary, an estimate field that may not exist,
and code living in a repository *inside* the project rather than alongside the backlog — so it
exercises every assumption GitHub let us leave implicit.

**OPN-003 is closed by this change**, with the part that cannot be settled centrally labelled
rather than guessed.

## What Changes

- **`AzureDevOpsBacklogConnector`** implementing every seam method, with no Azure type escaping
  the file — the same containment Octokit has.
- **Tags are the trigger vocabulary** (`System.Tags`), so matching is unchanged: an event from
  either vendor reaches matching as the same `StoryChanged`.
- **Process-dependent fields fail honestly.** The state the Agent named is written and the
  vendor's refusal is surfaced; the estimate tries the known field names in order and states
  which it tried when a project has none. Neither is hardcoded, because neither is knowable
  without the project's process template.
- **An optional code repository name on the Connector** (owner decision): empty for GitHub,
  where backlog and code coincide; on Azure DevOps it names the repository the implement-to-PR
  action clones.
- **`BacklogVendor.AzureDevOps`** and the configuration surface to choose it.

## Impact

- Affected specs: `connector-seam` (a second implementation is a requirement about the seam,
  not about a vendor).
- Touched: Backlog module (the connector, vendor enum, an optional Connector field + migration,
  configure slice), frontend (vendor choice), unit tests over the translation, ARCHITECTURE.md.
- Out of scope: Azure DevOps service hooks (#31 did GitHub; the AzDO shape is its own issue),
  process-template-specific UI.

## What this change does NOT prove

No call in it has ever reached a real Azure DevOps organisation — none was available. The
translation is unit-tested and the seam contract is exercised by the same stub-driven tier
GitHub passes, but "it works against the vendor" is a **hypothesis** until someone runs it
(ADR-0005). The spec, the design and ARCHITECTURE.md all say so, and the design records the
first thing to try when an organisation exists.
