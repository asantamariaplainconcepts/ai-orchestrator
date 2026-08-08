## 1. The rehearsal target (ADR-0014, design D3)

- [x] 1.1 Create a throwaway repository under the owner's account (suggested
      `ai-orchestrator-rehearsal`) with one committed file, and record its name in
      `findings.md`. Left in place afterwards: two changes owe an end-to-end verification
      blocked on exactly this, and paying for it once is the point of ADR-0014.
      (Created **private** at owner's choice: `asantamariaplainconcepts/ai-orchestrator-rehearsal`,
      default branch `master`, one README stating it is disposable plus a `greet.js` for an agent
      to edit.)

## 2. H1–H3 — the mechanics

- [x] 2.1 **H1, the clone happens.** Create a sandbox with `--clone` over a local checkout of the
      rehearsal repository on a prepared branch. From inside: is there a working copy, is it on
      that branch, and is its `origin` the git-daemon rather than GitHub? Record `git remote -v`
      and `git status` verbatim. (Clone present, on the prepared branch, origin = GitHub.)
- [x] 2.2 **H2, the work comes back.** Commit inside the sandbox, then look at the host checkout:
      does the commit appear, and does it need a pull, a push, or nothing? Whichever it is,
      record the exact mechanism — the documentation is loose about the direction of travel and
      this is the half the whole idea rests on. (It comes back only when the HOST fetches from
      the `sandbox-clone-h1` git:// remote; nothing arrives on its own.)
- [x] 2.3 **H3, credentials still never enter.** Inside the cloned sandbox: `GITHUB_TOKEN` is
      still empty, and an authenticated operation against the rehearsal repository still works
      through the host-side proxy. If the clone arrangement needs a token inside, that is a
      finding that outweighs the convenience.

## 3. H4 — the decoupling test (design D1)

- [x] 3.1 Run the same shape with a workspace path the sandbox is NOT given — no mount, nothing
      at the host's absolute path. A Run that still works is the only evidence that co-location
      is removable; a Run that fails names precisely what the mount was still doing. (Answered
      NO by direct observation: the host repo is bind-mounted read-only at /run/sandbox/source,
      same inode, and the clone is seeded from it.)
- [x] 3.2 If 3.1 cannot be arranged with the sbx CLI as it stands, say so and record H4 as **not
      verified**. Do not infer it from H1–H3 succeeding — that is the trap this task exists to
      avoid. (Not needed as an escape: H4 was answered by observation rather than left
      unarrangeable — `--clone` with no path still resolves a host workspace, and the CLI offers
      no clone-from-URL.)

## 4. H5 — DEC-062

- [x] 4.1 From inside a cloned sandbox, have the agent push a branch and open a pull request on
      the rehearsal repository — the promise DEC-062 makes. Record whether it works, and if it
      does not, what would have to change for the promise to survive a decoupled workspace.
      (Push AND pull request, from inside, with no credential in the sandbox — PR #1 on the
      rehearsal repository.)

## 5. H6 — the remote shape (desk-check)

- [x] 5.1 Only if H4 held: name what would actually carry a Run to a sandbox on another machine —
      what the executor would send, what the sandbox would need, and what is still missing.
      Questions sharpened, nothing built. If H4 did not hold, record instead which habitats the
      co-location constraint rules out, since that is the answer the Azure question wanted.
      (H4 did not hold, so the negative list is recorded — including that an Azure VM works only
      by moving the worker there, which is the shape that already exists.)

## 6. Verdict

- [x] 6.1 `findings.md` closes with a verdict per hypothesis and its exercised evidence, and
      either "co-location is kept, deliberately" or a named follow-up change that removes it.
      Run `openspec validate spike-sandbox-clones-its-own-workspace` and note that the spec
      addition may itself need modifying by that follow-up (design D2).
