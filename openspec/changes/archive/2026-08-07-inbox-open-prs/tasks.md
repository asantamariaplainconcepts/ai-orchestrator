## 1. The seam (Backlog module)

- [x] 1.1 Add `IChangeReader` to `Backlog.Contracts`: `Open(projectId, ct)` → open changes
      (number, title, url, head branch, created at) or a readable failure reason; empty for a
      project with no code repository.
- [x] 1.2 Add `IBacklogConnector.OpenChanges(coordinates, token, ct)`; implement for GitHub via
      Octokit `PullRequest.GetAllForRepository` (open state, newest first); Azure DevOps answers
      its existing unexercised reason.
- [x] 1.3 Add the `ChangeReader` adapter beside its siblings (`ConnectorAccess` resolve pattern)
      and register it in `BacklogModule` DI.

## 2. The endpoint (Runs module)

- [x] 2.1 Add `GetInboxChanges` use case: `GET /api/inbox/changes`, visible projects only, one seam
      read per project with a code repository, per-project degradation (a failing project reports
      its reason without blanking the rest).
- [x] 2.2 Mark product-created changes by joining URLs against `Run.OutputLink` scoped to visible
      projects, carrying the matched Run id.

## 3. The surface (frontend)

- [x] 3.1 Add `useInboxChanges` (its own query, `refetchInterval` slower than the waits, mounted by
      the Inbox screen only); shell badge query untouched.
- [x] 3.2 Render the group in `InboxScreen.tsx`: distinct treatment (no severity spine), newest
      first, vendor link as the action, product-created marker linking to the Run, per-project
      failure reasons, nothing rendered for projects with no code repository.
- [x] 3.3 i18n keys for the group heading, action, marker and failure line; mock handler for
      `/api/inbox/changes` covering all four states.

## 4. Tests

- [x] 4.1 Runs functional: fake connector gains the member; cover the marker join, per-project
      degradation, the no-repository state, and that nothing is persisted.
- [x] 4.2 GitHubStub gains a `/pulls` route (empty list), so E2E flows read "no changes" rather
      than 404.
- [x] 4.3 Backlog coverage for the vendor mapping, per the module's actual pattern — which turned
      out to be: no unit seam exists for either connector's HTTP mapping (the AzDO unit suite
      covers pure translation helpers; GitHub's Octokit calls are exercised at the E2E tier
      through the stub, which now answers `/pulls`). The seam contract itself is pinned in the
      Runs functional suite through the stub connector. A unit test invented against a seam that
      does not exist would be an assertion unable to fail (ADR-0013), so none was written.

## 5. Verification

- [x] 5.1 `dotnet csharpier check src`; `pnpm format:check`, `lint`, `typecheck` clean.
- [x] 5.2 `dotnet build src/AiOrchestrator.slnx` — 0 errors; affected module suites pass locally.
- [x] 5.3 Mock mode: the four states on screen (entries, product marker, failure reason, empty),
      both themes, and the badge unchanged while changes exist.
- [x] 5.4 CI green on the PR head (verified job-by-job), at sync. Run 31134369329 on 79b7c84:
      every job success, terraform correctly skipped.
