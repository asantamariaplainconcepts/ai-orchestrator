# Tasks — run-file-changes

## 1. The seam

- [x] 1.1 `ListChangeFiles` on `IBacklogConnector` returning path, status, counts, patch and an
      optional omission reason (design D1–D3); `ListChangeDocuments` becomes a projection of it.
- [x] 1.2 GitHub implementation from the existing `PullRequest.Files` result: patch, status,
      additions, deletions; binary (no patch) and over-bound patches carry their reason.
- [x] 1.3 `IChangeFileReader` in `Backlog.Contracts` so the Runs module reads without touching
      the Backlog implementation (design D4); owner registers it.

## 2. The read slice

- [x] 2.1 `GET /api/projects/{projectId}/runs/{runId}/changes`: resolve the Run's Story, find
      its linked change, return its files. No pull request → an explicit empty answer, not a
      404 pretending the Run is missing.
- [x] 2.2 Functional tests: files with patches; a binary/over-bound file carrying its reason; no
      linked change; a vendor failure surfacing as itself.

## 3. The portal

- [x] 3.1 Changes section on the Run detail page under the Plan: files with status and counts,
      unified patch rendered with kit tokens for added/removed/hunk lines (design D5); the three
      absences distinguished; catalog copy.
- [x] 3.2 Kit: `.diff` classes in the canonical CSS, tokens only — and every `var(--x)` checked
      to resolve (the story-detail retro's finding). **Outcome:** the check was run across the
      whole kit, not just the new classes; nothing unresolvable anywhere.
- [x] 3.3 Frontend lint + build.

## 4. Close-out

- [x] 4.1 UC-024 in docs/product/mvp/04; full suite; CI green.
