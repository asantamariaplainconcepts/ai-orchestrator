---
name: rebase-safely
description: Conduct a git rebase safely — preflight, a backup ref, and conflict-by-conflict resolution that challenges the user instead of guessing. Use when rebasing a branch onto an updated upstream (e.g. after a merge to main), replaying commits, or resolving rebase conflicts.
---

Drive a rebase to completion without silent conflict resolution — one responsibility: preflight, replay, challenge every conflict, verify. Not for `git merge`, and flag before rewriting a branch other people build on.

## Orientation — read before touching a conflict

During `git rebase <upstream>`, git replays **your** commits on top of the upstream, so the conflict labels **invert** relative to merge:

- **`ours` / `HEAD` = the upstream you are landing on** (the incoming work, e.g. main's changes).
- **`theirs` = the commit of yours** currently being replayed.

Getting this backwards silently discards exactly the change you meant to keep. Always name which **real** branch each side is before resolving.

## Steps

1. **Preflight.** Establish a safe starting point before rewriting any history.
   - Clean the tree: commit or stash all uncommitted work so `git status` is clean.
   - Name the three points — current branch, the upstream/target to rebase **onto**, and the merge-base — and confirm the target with the user if there's any ambiguity (e.g. `main` vs `feature/scaffold`).
   - `git fetch` the target so you rebase onto its true latest, not a stale local ref.
   - Create a backup: `git branch backup/<branch>-<yyyymmdd>` (dated so it's unique).
   - Enable resolution reuse once: `git config rerere.enabled true`.
   - Scope the work and preview collisions: `git log --oneline <upstream>..HEAD` for your commits, then intersect `git diff --name-only <merge-base> HEAD` with `git diff --name-only <merge-base> <upstream>` to list files touched on both sides.
   - Done when: tree clean, target confirmed + fetched, backup ref exists, and the user has seen the commit count and the overlapping-file list.

2. **Start the rebase.** Run `git rebase <upstream>` (use `--rebase-merges` only if the branch has merge commits worth preserving; otherwise plain). If it finishes with no conflicts, go to Finish.
   - Done when: the rebase either completes cleanly or stops at the first conflict.

3. **Resolve each conflict — challenge, don't guess.** A loop, repeated at every commit the rebase stops on. For each conflicted file:
   - State orientation for *this* stop: which real branch is `ours`, which is `theirs`, and the subject of the commit being replayed (`git rebase --show-current-patch HEAD` shows it).
   - For each hunk, explain what each side changed and **why they collide** — textual overlap vs a genuine semantic conflict.
   - Propose a resolution **with reasoning**, but present it as a recommendation, not a settled fact. Ask the user to confirm or redirect.
   - Challenge lazy answers: if the user says "just take mine/theirs," name exactly what that discards (e.g. "that drops main's new null-check on `X`") and require acknowledgement before applying it.
   - After resolving a file, confirm no `<<<<<<<` / `=======` / `>>>>>>>` markers remain and the result reads coherently, then `git add` it. When the stop is fully resolved, `git rebase --continue`.
   - Escape hatch: if resolution turns unsafe or the user loses confidence, offer `git rebase --abort` — it restores the exact pre-rebase state — rather than forcing through.
   - Done when: every conflict of every replayed commit is resolved with user-confirmed intent, no conflict markers remain anywhere, and the rebase reports it is finished.

4. **Finish & verify.** A clean-applying rebase can still be semantically broken.
   - Run the build/tests (or the project's verify) — replaying commits can compile individually yet break in combination.
   - Sanity-check intent was preserved: `git range-diff <upstream> backup/<branch>-<yyyymmdd> HEAD` compares the old and new commit series so you can see each commit survived as intended.
   - Push only with `--force-with-lease` (never `--force`), and confirm first — this rewrites shared remote history.
   - Keep the backup ref until the user confirms the rebased branch is good; delete it only then.
   - Done when: verification passed, the range-diff was reviewed, and — on confirmation — the branch was pushed with `--force-with-lease`.

## Do not

- Resolve any conflict without first stating the inverted `ours`/`theirs` orientation for the rebase.
- Apply `-X ours` / `-X theirs`, `git rebase --skip`, or a bulk `git checkout --ours/--theirs` without explicit, per-conflict consent — these discard changes wholesale.
- `git push --force` — always `--force-with-lease`, and confirm before rewriting remote history.
- Rebase a branch others are actively building on without flagging that it rewrites shared history.
- Delete the backup ref before the user confirms the result.
