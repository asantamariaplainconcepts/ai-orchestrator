## 1. Layout (RunScreen)

- [x] 1.1 Move the changes block into the main column; the rail's CHANGES card becomes the
      summary (PR, files, ±) anchoring to `#run-changes` (design D1).
- [x] 1.2 The failure banner above the content: full reason, Run again (story Runs) and Dismiss
      inside it, remedy link per the explicit map (design D2); remove the header's failure
      actions and the rail's red row.
- [x] 1.3 Empty Plan/Output cards collapse to one line each.

## 2. Diff rendering (RunChanges)

- [x] 2.1 Line numbers from hunk headers; sticky per-file header; per-file collapse.
- [x] 2.2 Mobile: wrap with the ± marker in a fixed gutter, left-truncated paths, files beyond
      the first collapsed by default below `md`.
- [x] 2.3 "Show N more lines" pagination for long patches (design D4).

## 3. Copy + mock

- [x] 3.1 i18n keys (banner heading/remedies, summary card, collapse/pagination controls, empty
      lines); no literal JSX text.
- [x] 3.2 Mock: a failed Run with a mapped cause and one with an unmapped cause; a multi-file,
      long-hunk change so pagination and collapse are reachable by hand.

## 4. Tests

- [x] 4.1 Survey and update any E2E/functional test pinned to the old placement (header actions,
      rail diff), updated not weakened.

## 5. Verification

- [x] 5.1 Frontend gates + design validator + production build.
- [x] 5.2 Mock mode at wide and narrow: diff at body width with line numbers and sticky headers;
      banner with decisions and correct link per cause; no sideways scroll at 375; empty cards
      one line; both themes; accessibility-tree check for the banner (role, single heading) —
      the turn-6 lesson.
- [x] 5.3 CI green on the PR head (verified job-by-job), at sync — twice: run 31163381458 on
      355340b, then run 31163920096 on the rebased cdd40f5, because main gained an AppHost
      change mid-flight and the e2e lane boots that AppHost. Every job success both times.
