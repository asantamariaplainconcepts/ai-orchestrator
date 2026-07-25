---
name: mark-pr-ready
description: Mark the existing draft PR ready for review via gh. Use when implementation has landed on the proposal PR and it's ready for the code-review stage.
---

Flip one existing draft PR to ready-for-review — one responsibility. Never open a second PR; the proposal and the code share one PR.

## Steps

1. **Verify the PR exists and is a draft.** Find the PR for the current branch (`gh pr view --json number,isDraft,url`).
   - Done when: the draft PR for this branch is identified.
2. **Confirm.** State the PR and that implementation is complete and pushed. Proceed only on confirmation.
   - Done when: the human approves.
3. **Mark ready.** Run `gh pr ready <number>` and refresh the PR description if needed (checklist, DoD).
   - Done when: the PR is no longer a draft and is requestable for review.
4. **Report.** Return the PR URL.
   - Done when: URL handed back.

## Do not

- Create a new PR — reuse the proposal's PR.
- Merge — that's `/ds:sync`.
- Retitle for the squash subject — making the PR title describe the implementation before merge is `/ds:sync`'s job, not this skill's (improving the title here is welcome but optional).
