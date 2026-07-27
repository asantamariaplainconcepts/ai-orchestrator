# Proposal: run-file-changes

## Why

Issue #54 (adds **UC-024**). #22 gave the reviewer the Plan; #19 gives them a PR link. What is
missing is the middle: *what the Agent actually changed*. Reading the plan, approving it and
then leaving for the vendor to see the result breaks the review loop the portal just built.

The grill found the data is already fetched and discarded: `ListChangeDocuments` (#38) calls
`PullRequest.Files`, whose result carries each file's unified patch, status and line counts,
and keeps only markdown filenames. This surfaces what an existing call already returns.

## What Changes

- **The seam returns file changes, not just document paths.** `ListChangeFiles` gives path,
  status, added/removed counts and the vendor's unified patch — vendor-neutral, so #29's AzDO
  connector implements the same shape. `ListChangeDocuments` becomes a filter over it rather
  than a second call.
- **A read slice on the Run**: the Run's linked change resolved from its Story, then its files.
- **The Run detail page** (built by #22) gains a Changes section under the Plan: each file with
  its status and counts, and its patch rendered as a coloured unified diff using kit tokens.
- **Stated behaviours, not surprises**: a binary file (no patch from the vendor) and a patch
  over the size bound each render a one-line notice naming the reason with a vendor link — a
  truncated diff presented as complete would be the dishonest option.

## Impact

- Affected specs: `connector-seam` (file changes), `run-orchestration` (the Changes section).
- Touched: Backlog module (seam + GitHub impl + a Contracts read for the Runs module), Runs
  module (one read slice), frontend (diff rendering + kit tokens for added/removed lines),
  docs/product/mvp/04 (UC-024), tests.
- Out of scope: commenting/approving/merging the PR from the portal, browsing untouched files,
  side-by-side view, syntax highlighting inside the diff.
