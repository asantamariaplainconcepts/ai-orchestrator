## Why

A Local Run ([BR-016](../../../docs/product/v1/05-business-rules.md), DEC-049, #210) executes **in
the Connector's configured folder** — the checkout its owner has open in their editor. It refuses a
dirty tree before it starts, switches that folder onto `ai/{story}-{slug}` while the Agent works, and
restores the previous branch afterwards. Three consequences follow, and all three are felt by the
same person: they must stash before triggering, they cannot type in their own repository while a Run
is going, and only one Local Run can exist at a time no matter what the project cap
([BR-002](../../../docs/product/v1/05-business-rules.md)) allows.

None of that is inherent to running an Agent on the operator's own hardware. It is inherent to
running it in the *one* checkout the folder has. A `git worktree` gives the Run a checkout of its
own from the same repository — so the branch it produces still lands where BR-016 promises, and the
owner's folder is never touched. Issue #331 (ACT-002 Member; UC-012, UC-021, UC-016).

## What Changes

- **A Local Run executes in its own checkout**, created as a `git worktree` of the configured folder
  on branch `ai/{vendorStoryId}-{slug}`, and removed when the Run ends. The branch remains in the
  owner's repository — worktrees share refs, which is why a worktree was chosen over a local clone.
- **BREAKING (product rule): BR-016's clean-tree requirement is removed.** A dirty folder no longer
  refuses a Local Run at dispatch or at execution, because the Run never enters that folder. The
  rule's text is amended in `docs/product/v1/05-business-rules.md` as part of this change, following
  the precedent #308 sets for BR-005. Everything else BR-016 says is preserved verbatim: the branch
  is the output, committed, never pushed, no pull request.
- **The "restore the previously checked-out branch" failure path is removed**, because nothing was
  ever checked out in the owner's folder to restore.
- **Several Local Runs execute concurrently**, bounded by the project cap (BR-002) exactly as
  sandboxed Runs are. The cap is now the only thing bounding them.
- **Abandoned checkouts are reaped at startup** — the analogue of the `aio-*` sandbox sweep, and for
  the same measured reason (31 sandboxes / 125 GB from a `finally` that never ran). Branches produced
  by reaped checkouts are never removed.
- **`Microsoft.FeatureManagement` is composed**, reading feature state from `IConfiguration` alone,
  with **no feature consuming it yet**. Recorded knowingly against
  [RULE-007](../../../docs/product/v1/08-backlog-shaping-rules.md)'s speculative-abstraction
  anti-pattern: the owner decided the plumbing lands here so the follow-on substrate-choice capability
  (sbx or worktree, per Automation) finds it ready. No Azure App Configuration dependency enters the
  product, so DEC-049's self-hostability is untouched.

**Not changed, deliberately:** where a Local Run is *available*. It still requires the Server to be a
process on the machine — `Identity:Mode = LocalOwner` with no `Habitat:LocalFolderUnavailableReason`
declared. The compose self-host path continues to decline Local folders with its own declared
sentence, and this change pins that refusal with a scenario rather than widening it.

**No new privilege.** `LocalAgentProcessHost` already runs the Agent CLI as a child of the Server
process with the machine's own environment. This change alters which directory it runs in and
nothing else — no credential, boundary or permission moves.

## Capabilities

### New Capabilities

None. This changes how an existing capability behaves; it introduces no capability that did not
exist.

### Modified Capabilities

- `local-code-source`: the requirement *"a Local Run works in the folder and leaves a branch, never a
  push"* is replaced — the Run works in its own checkout, the clean-tree verification and the
  restore-previous-branch path are removed, concurrent Local Runs are permitted, and abandoned
  checkouts are reaped. The habitat-refusal requirement gains a scenario asserting no checkout is
  attempted where the locus is declared unavailable.
- `backend-architecture`: a new requirement that the feature manager is composed from
  `IConfiguration` alone and that a habitat declaring no features starts unchanged.

## Impact

- **Product docs:** `docs/product/v1/05-business-rules.md` (BR-016 amended). No glossary change — a
  worktree is a working folder, which *Execution locus* already covers; it is **not** a Sandbox, and
  DEC-005's locked meaning of that word is not stretched to cover it.
- **Code:** `LocalFolderWorkspace` / `ILocalCodeWorkspace` (`Prepare`/`Conclude` semantics), the
  startup composition that registers it, `RunCreator` and `RunExecutor` (the BR-016 pre-write and
  execution-time checks), and a new startup reaper. `ValidateLocalPath` is untouched — it reports
  `isClean` as a fact and no longer gates anything.
- **Dependencies:** `Microsoft.FeatureManagement` added to `src/Directory.Packages.props`.
- **Tests:** the functional tests asserting the clean-tree refusal and the checkout-restore path
  assert the new behaviour instead; a concurrency test is added. Aspire, the outbox message schema,
  the host csproj graph and CI are unaffected.
