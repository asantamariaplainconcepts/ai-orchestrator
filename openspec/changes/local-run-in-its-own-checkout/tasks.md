## 1. The seam and the product rule

- [ ] 1.1 Amend BR-016 in `docs/product/v1/05-business-rules.md`: remove the clean-tree requirement
      and both refusal sites, state that a Local Run works in its own checkout, and preserve the
      branch-is-the-output sentence verbatim (spec criterion 9, design Migration Plan).
- [ ] 1.2 Remove `PreviousRef` from `LocalWorkspace` in
      `src/shared/AiOrchestrator.BuildingBlocks/Agents/ILocalCodeWorkspace.cs`, and delete
      `LocalWorkspaceErrors.DirtyTree`. Update the XML docs on `Prepare`/`Conclude` so they describe
      the checkout, not the folder (design D4).
- [ ] 1.3 Add the checkout-creation refusal to `LocalWorkspaceErrors` — a stage-named error carrying
      the folder and git's own reason, in the shape `WorkspaceErrors` already uses.

## 2. The checkout

- [ ] 2.1 Rewrite `LocalFolderWorkspace.Prepare` to create `git worktree add <checkout> -b
      ai/{vendorStoryId}-{slug}` in the product-owned checkout root, returning that path as
      `LocalWorkspace.Path`. It must not run `git checkout` in the configured folder (design D1, D3).
- [ ] 2.2 Rewrite `Conclude` to commit in the checkout and then `git worktree remove` it, leaving the
      branch. On failure it removes the checkout too, and restores nothing — there is nothing to
      restore.
- [ ] 2.3 Decide and document the checkout root (design Open Question 1) — outside the configured
      folder, namespaced so the reaper recognises the product's own work.
- [ ] 2.4 Remove the clean-tree pre-write check from `RunCreator` and the execution-time re-check
      from `RunExecutor`; leave every other Local-locus guard intact.
- [ ] 2.5 Confirm `ValidateLocalPath` and `Inspect` are untouched and `IsClean` still reports (design
      D4).

## 3. The reaper

- [ ] 3.1 Add a startup sweep that prunes the repository's worktree record and removes checkouts in
      the product's namespace that no live Run owns, skipping any checkout this process is using.
- [ ] 3.2 Assert in the sweep's own tests that no branch is ever removed (spec scenario "reaping never
      destroys a Run's output").
- [ ] 3.3 Register the sweep where the sbx reaper is registered, so both run on the same startup path.

## 4. Feature management plumbing

- [ ] 4.1 Add `Microsoft.FeatureManagement` to `src/Directory.Packages.props`, pinning the current
      version. Do **not** add any Azure App Configuration package (design D6).
- [ ] 4.2 Compose `AddFeatureManagement()` against the host's `IConfiguration` in the Server's
      composition, consumed by nothing.
- [ ] 4.3 Assert startup is unchanged with no `FeatureManagement` section present, in every habitat
      the test tiers cover.

## 5. Tests

- [ ] 5.1 Replace the functional tests asserting the clean-tree refusal and the checkout-restore path
      with tests for the new behaviour (spec scenarios 1 and 2).
- [ ] 5.2 Add a test that two Local Runs for different Stories on one folder execute concurrently,
      each in its own checkout.
- [ ] 5.3 Add a test that a Local Run leaves its branch in the configured folder's repository and its
      checkout gone.
- [ ] 5.4 Add a test that an unusable folder is refused before any write, naming the folder and the
      reason.
- [ ] 5.5 Add the habitat scenario: where `Habitat:LocalFolderUnavailableReason` is declared, no
      worktree is created and no git command runs against a configured path.
- [ ] 5.6 Follow the repository's naming convention `Subject_Should_Constraint` — the ArchTests
      enforce it.

## 6. Verification

- [ ] 6.1 `dotnet build` clean, no new warnings.
- [ ] 6.2 `dotnet test` green across the affected module and shared test projects.
- [ ] 6.3 CSharpier passes (the pre-commit hook and CI both run it; `--no-verify` is still caught).
- [ ] 6.4 Exercise a real Local Run end to end in the `aspire run` dev loop against a **dirty** folder
      and confirm by hand: the Run succeeds, the folder is untouched, the branch is present, the
      checkout is gone (ADR-0006 — exercised, never read).
- [ ] 6.5 Kill the Server mid-Run, restart, and confirm the sweep removes the orphaned checkout and
      leaves its branch.
