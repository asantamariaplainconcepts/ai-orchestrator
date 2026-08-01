## Why

Issue #215. Since #162 the only action is running the repository's prompt, and since #150 the
Automation names that prompt as a bare string — a typo or a wrong directory surfaces only when a
Run fails (UC-005/UC-006 configure it; BR-008 reads it live). The form knows the project's prompts
directory and the Connector can read the repository; nothing offers the Admin the prompts that
actually exist. #150 left discovery explicitly out of scope; this change is that scope, now that
the prompt name is the form's most important field.

## What Changes

- The Automation form's prompt-name input becomes a **picker that also accepts free text**: it
  offers the `.md` files currently in the project's prompts directory, read **live** from the
  repository's default branch through the Connector — never cached, never mirrored (BR-008's
  spirit). Free text stays, because a prompt may be arriving in a pending PR.
- The Connector seam grows one read: **list the files of a repository directory at the default
  branch** — beside the existing single-document read. Implemented and exercised for GitHub;
  Azure DevOps beside it per ADR-0005 (stated hypothesis, unexercised).
- Discovery failure degrades honestly: a missing directory or a failed listing renders the field
  as today's textbox with the reason readable — configuration is never blocked by discovery, and
  the missing-at-run refusal from #150 is untouched.
- The suggestion is a convenience only (same rule the output-label picker follows): saving is not
  restricted to listed names, and no save-side validation changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `automation-configuration`: the prompt-name input's requirement changes from a bare text field
  to a picker fed by the live repository listing, with free text preserved and stated degradation.
- `connector-seam`: a new read — list a repository directory's files at the default branch — with
  the seam's vendor-neutral types and the GitHub/AzDO exercised/hypothesis split.

## Impact

- **Backend**: Backlog module — the Connector seam interface and both vendor implementations; one
  new query use case exposing the listing to the portal (`/api/...` under the module's routes).
- **Frontend**: the Automation form slice (`src/frontend/features/automations/`) — the prompt
  field component and its query hook.
- **Unchanged**: save-side validation, the Run path's live read and its honest refusal (#150),
  BR-010 (the PAT is resolved by name at read time, value never travels), dispatch, Run states.
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
