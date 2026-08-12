## Context

`LocalFolderWorkspace` (`src/shared/AiOrchestrator.Infrastructure/Agents/LocalFolderWorkspace.cs`)
implements `ILocalCodeWorkspace` by operating **on the configured folder itself**: `Prepare` re-checks
the tree is clean, remembers the current ref, and `git checkout -b ai/{story}-{slug}`; `Conclude`
commits and, on failure, restores `PreviousRef`. `RunCreator` and `RunExecutor` both consult it, so
BR-016's clean-tree sentence is raised twice — once before dispatch and once at execution, because
the folder belongs to a person who may have typed in between.

That design is the source of all three complaints in the proposal, and none of them come from the
Agent running on the operator's own hardware. `LocalAgentProcessHost`
(`src/shared/AiOrchestrator.Infrastructure/Agents/HeadlessProcess.cs`) already runs the Agent CLI as
a child of the Server process with the machine's own environment, and takes a `workingDirectory`
argument. Point it at a different directory and nothing else about the execution changes.

**Habitat reach is unchanged and is not this design's to widen.** A Local Run requires
`Identity:Mode = LocalOwner` (`IdentityHabitat.IsSelfHost`) with no
`Habitat:LocalFolderUnavailableReason` declared. `selfhost/docker-compose.yaml` declares that reason
— *"the orchestrator runs in a container here, and a folder on this machine is not visible to it"* —
so the compose path declines Local folders today and continues to.

## Goals / Non-Goals

**Goals:**

- A Local Run never enters the configured folder, so a dirty tree stops being a refusal and the
  owner can keep working while a Run executes.
- Concurrent Local Runs, bounded only by the project cap (BR-002).
- The branch stays the Run's output, in the owner's own repository, exactly as BR-016 promises.
- Abandoned checkouts are reaped without ever destroying a branch.
- `Microsoft.FeatureManagement` composed from `IConfiguration`, consumed by nothing.

**Non-Goals:**

- Widening where a Local Run is available (mounting a host folder into the compose path).
- Choosing a substrate per Automation — the follow-on capability this change only composes plumbing
  for.
- A terminal on a Local Run's checkout.
- Preparing the checkout so a build can run in it (its own issue; a fresh worktree has no
  `node_modules`).
- Any change to sbx or ACA Runs.

## Decisions

### D1 — `git worktree`, not a local clone

**Measured, not assumed** (probe run 2026-08-12 against real `git`; nine checks, all passing):

| Claim | Result |
| --- | --- |
| `worktree add` from a repository with uncommitted changes | succeeds |
| the configured folder afterwards | same branch, same uncommitted changes |
| two worktrees of one repository, different branches | both live concurrently |
| `worktree add` for a branch already checked out elsewhere | `fatal: … already used by worktree`, exit 128 |
| `worktree remove` after a commit | branch survives, carrying the commit |
| `rm -rf` the directory, then `worktree prune` | record cleaned, branch survives |

The decisive property is the fifth: **a worktree shares the repository's refs**, so the branch it
produces is already in the owner's repository when the checkout goes away. BR-016 promises exactly
that and needs no push-back ceremony to keep it.

*Rejected — a local clone.* Stronger isolation (its own `.git`, so the Agent cannot reach the owner's
other refs) and still cheap, because `git clone` hardlinks objects on one filesystem. Rejected
because the run branch would live in a temporary clone and would have to be pushed back into the
owner's repository for BR-016 to hold — a ceremony with its own failure modes, bought to defend
against an Agent that already runs as the machine owner with the machine owner's credentials. The
boundary would be cosmetic; the ceremony would not.

*Rejected — a worktree only when the tree is dirty.* Keeps in-place as the common path and never
delivers concurrency, which is half the value.

### D2 — the branch name is unchanged, and that enforces BR-001 for free

The checkout is created with `git worktree add <path> -b ai/{vendorStoryId}-{slug}`, the name BR-016
already specifies. Because git refuses a branch already checked out in another worktree (measured
above, exit 128), a second concurrent Run for the *same* Story cannot get a checkout. BR-001 already
forbids that, so this is a second, mechanical guard rather than a new rule — worth writing down
because a future reader will otherwise remove the branch-name coupling without knowing what it
carries.

### D3 — the checkout lives outside the configured folder

Under the folder, a worktree would appear in the owner's own file watchers, editors, and
`git status` ignore-noise. The checkout root is a product-owned directory with a namespace this
product claims, so the reaper can recognise its own work — the same discipline `SbxSandboxRoster`
applies to the `aio-*` sandbox namespace, and for the same reason: a sweep that cannot tell its own
artifacts from someone else's is a sweep that eventually deletes someone else's.

### D4 — `Prepare`/`Conclude` keep their shape; `LocalWorkspace.PreviousRef` goes

`ILocalCodeWorkspace` is unchanged as a seam. `Prepare` returns a `LocalWorkspace` whose `Path` is
now the checkout rather than the configured folder — which is precisely why `RunExecutor` needs no
restructuring, since it already hands `LocalWorkspace.Path` to the runtime. `PreviousRef` is removed:
there is no previous checkout to restore, and a field retained "just in case" would be a lie about
what failure does.

`Inspect` keeps reporting `IsClean` — it is a fact about a path and the configure screen still shows
it. It simply stops gating anything. `ValidateLocalPath` is untouched.

### D5 — the reaper is a startup sweep, and it prunes before it removes

`git worktree prune` reconciles the repository's record with the disk; removal of a live checkout is
`git worktree remove`. The sweep runs at startup, over the product's own checkout namespace, skipping
any checkout a Run in this process currently owns. **It never touches a branch** — measured above:
both `remove` and `prune` leave `ai/{story}-{slug}` intact.

The sweep exists because the failure mode is already on record for the sibling substrate: 31
abandoned sandboxes and 125 GB, from a process that died before its `finally` ran
(`SbxSandboxLifecycle.ReapAbandoned`). A checkout leaks identically.

### D6 — `Microsoft.FeatureManagement`, composed and unused

Registered with `AddFeatureManagement()` reading the host's `IConfiguration`; no Azure App
Configuration package, endpoint or credential. The library's own documentation is explicit that it is
built on `IConfiguration` and that the Azure service is one possible source rather than a
requirement, which is what keeps DEC-049's "a stranger with Docker can still run it" true.

**This is a seam with zero consumers and RULE-007 names that an anti-pattern.** It is here because
the owner decided (#331) that the follow-on substrate-choice capability should find the plumbing
ready. Recorded rather than rationalised: the honest cost is a dependency and a registration that no
scenario in this change exercises beyond "it resolves and changes nothing".

*Rejected — deriving the substrate from configuration the way `Agents:Sandbox:Launcher` already is.*
That is ADR-0010's shape and would need no package, but the owner's decision is on record and this
design implements it rather than relitigating it. The disagreement is preserved here so proposal
review can overturn it cheaply if it wants to.

## Risks / Trade-offs

- **A fresh checkout cannot build.** No `node_modules`, no build outputs — an Agent told to make the
  tests pass meets an unprepared tree. → Its own issue, sequenced immediately behind this one; this
  change does not pretend to solve it. This repository has already hit the failure in its own
  worktrees.
- **The Agent can reach the owner's other branches** through the shared object store, and the parent
  checkout through the filesystem. → Accepted knowingly: it already can, because it runs as a child
  of this process with the machine owner's environment. This change moves no boundary. The clone
  alternative (D1) is the escape hatch if that ever stops being acceptable.
- **Disk grows with concurrency** — one checkout per live Run rather than zero. → Bounded by BR-002's
  project cap and released at Run end; the startup sweep covers the crash case.
- **A partially-removed checkout at crash time** could leave the repository's worktree record stale
  and the directory present. → `prune` then `remove` is idempotent in both orders (measured); the
  sweep runs unconditionally at startup rather than only after a detected crash.
- **`FeatureManagement` is dependency weight for no behaviour.** → Stated above rather than hidden;
  proposal review is the gate that can decline it without touching the rest of the change.

## Migration Plan

No data migration: no schema, no stored shape and no message contract changes. `LocalWorkspace` is an
in-memory record.

BR-016's text in `docs/product/v1/05-business-rules.md` is amended in this change, following the
precedent #308 sets for BR-005. The clean-tree sentence is removed; the branch-is-the-output sentence
is preserved verbatim.

Rollback is `git revert` of the change — nothing outside the repository is mutated, and any branch a
Local Run already produced is unaffected either way.

## Open Questions

- **Where the checkout root lives** — beside the configured folder, under the product's own state
  directory, or a configured path. D3 fixes the *properties* it must have (outside the folder,
  namespaced, recognisable to the reaper) and leaves the location to implementation.
- **Whether `Inspect` should stop reporting `IsClean` to the portal.** Kept for now (D4) because the
  configure screen shows it as information; if it reads as a requirement to a user, that is a
  follow-up on the surface, not on this seam.
