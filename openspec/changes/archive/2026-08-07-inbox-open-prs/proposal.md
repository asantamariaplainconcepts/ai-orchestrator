## Why

The product opens pull requests all day — an implement Run's change, a setup install, a starter
install — and then loses sight of them: a PR is visible only from the Run that created it, the
setup report that linked it, or a Story's documents. There is no answer to "what is waiting for my
review?", and the Inbox exists for exactly that question (UC-026). Issue
[#274](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/274).

## What Changes

- The Inbox gains a second, **visually distinct** group: the open changes (pull requests) of each
  visible project's connected code repository, read live from the vendor, newest first, each
  linking to the vendor's page. Decided at grill: same surface as the Run waits, different
  presentation — a PR is answered on the vendor, a Run wait inside the product.
- A change whose URL matches a Run's recorded output link is marked as the product's own and links
  to that Run.
- A new vendor read joins the Connector seam: the open changes of a repository, named "changes"
  per the seam's vocabulary rule. GitHub implements it; Azure DevOps answers with its existing
  unexercised-path reason.
- The read is served by its own endpoint and polled only while the Inbox page is open, on a slower
  cadence than the Run waits — the shell's ambient badge keeps counting Runs only and triggers no
  vendor call. (A deliberate narrowing of the issue's phrasing, recorded in the design: the badge
  polls every 30 s from every page, and N vendor reads per project on that cadence is a rate-limit
  incident, not a feature.)
- Failure degrades to a readable reason; projects without a code repository simply contribute no
  group. Nothing about a change is stored locally (BR-008).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `connector-seam`: one added requirement — a repository's open changes can be listed through the
  seam, vendor-neutrally.
- `run-orchestration`: one added requirement beside the inbox one — open changes await review in
  the Inbox as their own group, with the product-created marker, the degradation rule, and the
  ambient count's unchanged meaning.

## Impact

- `AiOrchestrator.Modules.Backlog.Contracts` — new `IChangeReader` (+ records).
- `AiOrchestrator.Modules.Backlog` — new `IBacklogConnector` member `OpenChanges`, GitHub
  implementation (Octokit `PullRequest.GetAllForRepository`, open state), Azure DevOps stub per its
  existing pattern, `ChangeReader` adapter, DI registration.
- `AiOrchestrator.Modules.Runs` — new use case `GetInboxChanges` (`GET /api/inbox/changes`):
  visible projects → per-project seam read → join against `Run.OutputLink` for the marker.
- Frontend — `useInboxChanges` hook (slow poll, page-scoped), the distinct group in
  `InboxScreen.tsx`, i18n keys, mock handler.
- Tests — Runs functional (fake connector member: marker join, degradation, no-repo project),
  GitHubStub gains a `/pulls` route, connector unit coverage per existing patterns.

Module boundaries preserved: Runs consumes the read through `Backlog.Contracts` only. No schema
change. No `OPN-*` open.
