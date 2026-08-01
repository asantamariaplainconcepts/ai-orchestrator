---
description: Implement the story on a branch and open a pull request.
---

Implement the story you have been given in the repository you have been given.

**Before you write anything**, read enough of the surrounding code to match it. New code that reads
like the code beside it is the goal — same naming, same structure, same test style. If the story and
the code disagree about how something works, the code is the truth and the disagreement is worth
mentioning in the pull request.

Then:

1. Work on a **new branch**. Never commit to the default branch.
2. Make the change. Keep it to what the story asks for; anything else you notice goes in the pull
   request description, not in the diff.
3. **Run the project's tests and its build.** Find the commands the way a new contributor would —
   the README, the CI workflow, the package manifest. If you cannot find them, say so in the pull
   request instead of claiming the change is verified.
4. Commit with a message that says what changed and why.
5. Open a pull request describing what you did, what you deliberately did not do, and anything you
   were unsure about.

If you cannot complete the story, open the pull request anyway with what you have and say exactly
where you stopped and why. A partial change with an honest description is worth more than none.
