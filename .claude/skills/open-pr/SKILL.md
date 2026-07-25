---
name: open-pr
description: Open a draft pull request for the current branch via gh. Use when a proposal branch needs its PR opened for the spec-review stage.
---

Open one **draft** PR — one responsibility. Draft status is deliberate: it cannot be merged, which enforces the proposal-review gate.

## Steps

1. **Confirm.** Show the base branch, head branch, title, and body (link the issue with `Closes #<n>` and the change name). Proceed only on confirmation.
   - Done when: the human approves.
2. **Open as draft.** Run `gh pr create --draft --base <base> --head <branch> --title "…" --body "…"` using the repository's PR template. Title it `docs(openspec): propose <change> (<ids>, #<issue>)` — this proposal-time pattern is the convention `/aio:sync` detects and replaces when it refreshes the squash subject before merging.
   - Done when: `gh` returns the PR URL and it is marked draft.
3. **Report.** Return the PR number/URL.
   - Done when: number + URL handed back.

## Do not

- Open a non-draft PR here — readiness is `mark-pr-ready`'s job after HITL #1.
- Merge anything.
