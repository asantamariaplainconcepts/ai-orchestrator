## 1. The seam (Backlog module)

- [ ] 1.1 Add `IChangeReader` to `Backlog.Contracts`: `Open(projectId, ct)` → open changes
      (number, title, url, head branch, created at) or a readable failure reason; empty for a
      project with no code repository.
- [ ] 1.2 Add `IBacklogConnector.OpenChanges(coordinates, token, ct)`; implement for GitHub via
      Octokit `PullRequest.GetAllForRepository` (open state, newest first); Azure DevOps answers
      its existing unexercised reason.
- [ ] 1.3 Add the `ChangeReader` adapter beside its siblings (`ConnectorAccess` resolve pattern)
      and register it in `BacklogModule` DI.

## 2. The endpoint (Runs module)

- [ ] 2.1 Add `GetInboxChanges` use case: `GET /api/inbox/changes`, visible projects only, one seam
      read per project with a code repository, per-project degradation (a failing project reports
      its reason without blanking the rest).
- [ ] 2.2 Mark product-created changes by joining URLs against `Run.OutputLink` scoped to visible
      projects, carrying the matched Run id.

## 3. The surface (frontend)

- [ ] 3.1 Add `useInboxChanges` (its own query, `refetchInterval` slower than the waits, mounted by
      the Inbox screen only); shell badge query untouched.
- [ ] 3.2 Render the group in `InboxScreen.tsx`: distinct treatment (no severity spine), newest
      first, vendor link as the action, product-created marker linking to the Run, per-project
      failure reasons, nothing rendered for projects with no code repository.
- [ ] 3.3 i18n keys for the group heading, action, marker and failure line; mock handler for
      `/api/inbox/changes` covering all four states.

## 4. Tests

- [ ] 4.1 Runs functional: fake connector gains the member; cover the marker join, per-project
      degradation, the no-repository state, and that nothing is persisted.
- [ ] 4.2 GitHubStub gains a `/pulls` route (empty list), so E2E flows read "no changes" rather
      than 404.
- [ ] 4.3 Backlog unit/functional coverage for the GitHub mapping per the module's existing
      connector test pattern.

## 5. Verification

- [ ] 5.1 `dotnet csharpier check src`; `pnpm format:check`, `lint`, `typecheck` clean.
- [ ] 5.2 `dotnet build src/AiOrchestrator.slnx` — 0 errors; affected module suites pass locally.
- [ ] 5.3 Mock mode: the four states on screen (entries, product marker, failure reason, empty),
      both themes, and the badge unchanged while changes exist.
- [ ] 5.4 CI green on the PR head (verified job-by-job), at sync.
