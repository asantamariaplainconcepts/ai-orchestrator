# spike-sandbox-clones-its-own-workspace — findings

Machine: Apple Silicon, macOS 26.5.2. sbx v0.38.0. Date: 2026-08-08.
Rehearsal target: **`asantamariaplainconcepts/ai-orchestrator-rehearsal`** (private, default
branch `master`) — created for ADR-0014 and deliberately left in place.

## Verdict, up front: **co-location holds. `--clone` does not decouple anything.**

It is a *writability* arrangement, not a transport. The host's repository is still mounted into
the sandbox, and the clone is seeded from that mount. A sandbox on another machine remains
impossible without building something that does not exist today.

## H1 — the clone happens: **CONFIRMED**

```
$ sbx run -d --clone --name clone-h1 shell .        # host checkout on branch ai/spike-clone
$ sbx exec clone-h1 -- <in the workspace path>
git branch --show-current  → ai/spike-clone         # the prepared branch, carried in
git remote -v              → origin  https://github.com/…/ai-orchestrator-rehearsal.git
```

The agent gets a working copy on the branch the executor prepared. Its `origin` is GitHub, not
the daemon.

## H2 — the work comes back, but only if pulled: **CONFIRMED, with a caveat that matters**

```
inside:  git commit -am "feat: excitement"   → 825b62f
host:    git log --oneline -1                → ff712e0        ← unchanged
host:    git fetch sandbox-clone-h1          → ok (2 new refs)
host:    git log sandbox-clone-h1/ai/spike-clone -1 → 825b62f ← there it is
```

The wiring is a remote **on the host** — `sandbox-clone-h1 → git://127.0.0.1:49158/rehearsal` —
so the host *pulls from* the sandbox. Nothing arrives on its own: the host's working tree stays
exactly as it was until somebody fetches and merges. For an orchestrator that reads a Run's diff
from the vendor rather than from disk, that is survivable; for anything expecting the workspace
to contain the result, it is not.

## H3 — credentials still never enter: **CONFIRMED**

```
inside:  echo GITHUB_TOKEN=[${GITHUB_TOKEN}]  → GITHUB_TOKEN=[]
inside:  git ls-remote --heads origin         → ff712e0  refs/heads/master   (private repo)
```

Unchanged from the sandboxing change: the value is not there, and the egress proxy authenticates
anyway. `--clone` costs nothing here.

## H4 — the host path stops mattering: **NO — and this is the finding**

The decisive observation, from inside the sandbox:

```
$ mount | grep sandbox/source
host on /run/sandbox/source type virtiofs (ro,nosuid,nodev,relatime)

$ stat -c %i /run/sandbox/source/greet.js   → 136354115
$ stat -f %i <host>/greet.js                → 136354115      ← the same file
$ touch /run/sandbox/source/.probe          → Read-only file system
```

The host's repository is **bind-mounted read-only** inside the sandbox, and the agent's clone is
seeded from it. The workspace path the agent works in is a genuine copy (different inode), but
the source it was copied from is the host's filesystem.

And there is no way to ask for the clone without it: `sbx run --clone shell` with no path still
resolves a host workspace, and the CLI offers no "clone from this URL instead". So the escape
hatch the sbx spike noticed is not one. **Task 3.2's honest record applies: H4 is answered NO by
observation, not left unverified.**

## H5 — DEC-062 survives: **CONFIRMED, and more completely than expected**

From inside the cloned sandbox, against the private rehearsal repository:

```
git push -u origin ai/spike-clone   → * [new branch]  ai/spike-clone → ai/spike-clone
gh pr create …                      → https://github.com/…/ai-orchestrator-rehearsal/pull/1
```

The agent pushed a branch and opened a pull request on a private repository **while holding no
credential**. DEC-062's promise — the agent publishes its own work — is intact under a cloned
workspace, and the `gh` CLI turned out to be present in the template.

## An unlooked-for property worth keeping

The host's working tree was **untouched** after the agent edited and committed:

```
host:  cat greet.js      → the original, no "!"
host:  git status --short → (empty)
```

`--clone` means an agent cannot dirty the developer's checkout. That is a real benefit for the
**local** lane (#210, where a Run works in the owner's own folder) and has nothing to do with
decoupling. It is the strongest reason to reach for `--clone` that this spike found.

## H6 — the remote shape: what the answer rules out

H4 failed, so the desk-check becomes the negative one task 5.1 asks for. Co-location stands, so:

- **Azure VM with sbx** — viable, and the only shape is the one already available: move the
  **worker** there as a queue consumer. Not "connect the orchestrator to a sandbox host". The
  nested-virtualization caveats from the exploration stand (Dv4/Dv5, Ev4/Ev5, Fv2; Standard
  security type only; Microsoft frames nested virt as non-production).
- **A sandbox pool shared by several orchestrators** — ruled out. Nothing can hand a workspace
  across a machine boundary.
- **ACA Jobs / Container Apps** — ruled out for sbx (no KVM), unchanged.
- **AKS Pod Sandboxing (Kata)** — still the Azure-native candidate, and it sidesteps this
  entirely because it isolates the *pod* rather than asking a sandbox to reach a workspace.
- **CubeSandbox** — its REST API is the only surveyed thing that could break co-location, and
  the price is that the workspace must travel to it. Not free; just possible.

## What this means for the seam

The convergence question keeps resolving the same way: not one technology everywhere, but one
`IAgentProcessHost` with a driver per habitat. This spike removes an option rather than adding
one, which is worth as much — a habitat designed on the assumption that `--clone` decouples would
have been designed on a hope.

## Recommendation

**No follow-up change.** Keep co-location, and keep the spec addition this change makes, which
now reads as a deliberate constraint rather than an unexamined one.

One optional, unrelated idea surfaced and is worth its own proposal if anyone wants it: use
`--clone` for the **local locus** so a Run cannot dirty the owner's working folder. That is a
product improvement with a measured basis, and nothing to do with deployment.
