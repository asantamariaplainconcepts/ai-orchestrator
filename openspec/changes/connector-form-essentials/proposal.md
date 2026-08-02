## Why

Issue #220. Configuring a Connector (UC-004) asks eight questions at the moment somebody knows
least — vendor, owner, repository, a credential-*mode* select, the token, the prompts directory,
the code source, and on Azure DevOps the code repository — with the explanatory hints pooled at
the bottom of the form, a scroll away from the fields they describe. Four of those are required;
the rest carry defaults or serve a minority path, and they compete for attention on equal terms.

Two of them are worse than merely noisy. The credential *mode* select and the token input are two
controls for one decision, though pasting is the default path (#124). And with a Local folder code
source the Azure DevOps code repository names where to open a pull request — which a Local Run
never does; it leaves a branch (#210). The form asks anyway.

## What Changes

- The Connector form leads with **four inputs** — vendor, owner, repository, one credential — and
  folds the rest behind an explicit **Advanced** disclosure, each field carrying its own hint
  rather than a paragraph pooled at the end.
- The credential is **one input**, with a plain link to name an existing secret instead of pasting
  one. Choosing it swaps the input; the two never both carry a value, so the API's "not both"
  refusal becomes unreachable from the portal.
- **Advanced opens itself** when the Connector already stores any value it holds, and **cannot be
  collapsed** while the Local folder code source is chosen — its path is required and absolute
  server-side, and a required field behind a disclosure is a save that fails against something
  invisible.
- With the Local folder code source, the **code repository input is not rendered** and the request
  sends it as null, stating once why: a Local Run leaves a branch and opens no pull request.
- **No API change.** The request shape and every validation rule stay exactly as they are; this
  change is about which questions are asked, when, and next to what.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `connector-configuration`: the configuring *surface* gains requirements — an essentials-first
  form with a disclosure that may never hide a conditionally-required field, one credential input,
  and hiding-equals-clearing for the code repository under a local code source.

## Impact

- **Frontend only**: `src/frontend/features/backlog/ProjectScreen.tsx` (the Connector panel) and
  `CodeSourceSection.tsx`, plus the typed i18n catalogue. No new component beyond a disclosure.
- **Backend**: none. `ConfigureConnector`'s validator and handler are untouched — the form is
  being shaped *around* their conditional rules (`LocalPath` required-and-absolute under
  LocalFolder; the credential's exclusive-or), not changing them.
- **Unchanged**: BR-009's Admin gate, BR-010 (the credential is named or pasted, never displayed),
  #160's keep-the-stored-credential behaviour, #211's cloud-posture rule (no code-source UI where
  the surface answers 404).
- No integration contracts (Aspire, host csproj, queue message schema, CI) are affected.
