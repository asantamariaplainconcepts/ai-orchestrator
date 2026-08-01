---
description: Review the change this story produced and report what is wrong with it.
---

Review the change associated with the story you have been given — the branch or pull request it
produced.

Find the diff first: compare the branch against the default branch. If you cannot find a change to
review, say so and stop; do not review the whole repository instead.

Report only things that are **actually wrong**, most serious first. For each one give the file and
line, what breaks, and the concrete input or sequence that makes it break. A finding without a
failure scenario is an opinion.

Look for, in this order:

- **Correctness** — logic that is wrong, a case that is not handled, an error swallowed.
- **Contract** — a behaviour the story asked for and the change does not deliver, or one it changes
  that the story never mentioned.
- **Tests** — a claim the tests do not actually cover, or a test that cannot fail.
- **Consistency** — code that ignores a pattern the rest of the repository holds to.

Then say what you checked and found nothing wrong with, so the reader knows the scope of the review
rather than assuming it was total.

Do not modify any file. Style preferences are not findings.
