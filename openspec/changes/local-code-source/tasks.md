## 1. Connector: the code-source axis

- [ ] 1.1 Add `CodeSource` enum (`Repository = 1`, `LocalFolder = 2`) and `LocalPath` to the
      Connector aggregate with `UseLocalFolder`/`UseRepositorySource`; additive Backlog migration
      defaulting every existing row to `Repository`.
- [ ] 1.2 Widen `ConfigureConnector` to accept `codeSource` + `localPath`; refuse `localFolder`
      outside the self-host posture (the LocalOwner composition switch), 404 the surface entirely
      where the posture is absent.
- [ ] 1.3 Add the validate-path use case (`POST /api/projects/{id}/connector/validate-path` →
      `{isDirectory, isGitRepository, branch, isClean}`), Admin-gated and posture-gated; answers
      about exactly one path, never lists contents.
- [ ] 1.4 Expose the code-source kind on the connector read model (for #211's badge and recents).

## 2. Run: locus recorded and refused

- [ ] 2.1 Add `Locus` (`Pod = 1`, `Local = 2`), `WorkingFolder`, `BranchName` to the Run
      aggregate; additive Runs migration defaulting existing rows to `Pod`.
- [ ] 2.2 Derive the default locus in `RunCreator` from the project's code source; accept an
      explicit locus from `RunNow` and refuse impossible pairings (Local without a folder, Pod
      with one), naming the constraint.
- [ ] 2.3 Add the clean-tree pre-write refusal at dispatch for Local runs (BR-016 pattern:
      refuse before any write, name the folder).
- [ ] 2.4 Expose locus, working folder and branch on the runs read model.

## 3. Workspace: the Local sibling behind the seam

- [ ] 3.1 Implement `LocalFolderWorkspace : ICodeWorkspace` in ServiceDefaults: re-verify clean
      tree, `git switch -c ai/{vendorStoryId}-{slug}`, hand the folder to the runtime, commit
      what changed; never push, never open a PR; restore the prior checkout on failure paths.
- [ ] 3.2 Select the workspace per Run by locus in `RunExecutor`; skip vendor-credential
      resolution for Local and write the one host-credentials log line; record folder + branch
      on the Run at execution.

## 4. Rules and docs

- [ ] 4.1 Record BR-016 (a Local run requires a clean working tree) in
      `docs/product/mvp/05-business-rules.md`; extend UC-004/UC-011/UC-012/UC-016/UC-021 in
      place where their wording assumes clone-and-PR.

## 5. Tests

- [ ] 5.1 Functional: configure LocalFolder in the self-host fixture → RunNow defaults Local →
      Run records locus/folder/branch; dirty tree refuses with the sentence; impossible locus
      pairings refuse; cloud posture 404s the surface.
- [ ] 5.2 Workspace: branch created and committed, no push, prior checkout restored on failure,
      execution-time dirty re-check fails the Run with the same sentence.
- [ ] 5.3 Regression: full existing suites pass unchanged — a Repository project's behaviour is
      byte-for-byte today's.
