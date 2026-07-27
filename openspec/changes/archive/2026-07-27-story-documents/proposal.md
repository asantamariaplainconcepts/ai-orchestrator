# Proposal: story-documents

## Why

Issue #38 (adds **UC-023**). #37 made the portal show what a Story _asks for_; this makes it
show what was _written for it_ — the proposal, the design, the tasks — so reviewing a change
does not mean opening the vendor. It is also the last piece before the approval gate (#22) has
an honest place to show a Plan: next to the documents it was written against.

## What Changes

- **The Connector seam gains two reads**, in product vocabulary: find the change (pull request)
  linked to a Story, and read a document's content at a ref. GitHub implements both; the AzDO
  connector (#29) will have to as well — which is the point of putting them on the seam.
- **Generic by decision, not OpenSpec-shaped** (taken at grill): the app lists the **markdown
  files the linked PR adds or changes**. No path convention, no branch-name parsing, no
  assumption a team uses OpenSpec. For this repository that surfaces the whole change bundle;
  for another team, whatever documents their work adds.
- **Story detail page**: lists the attached documents by path and renders the selected one
  through the same sanitising pipeline as the description (#37) — repository content is
  untrusted wherever it comes from.
- **Read live, never mirrored** (BR-008): documents are fetched at the PR branch's head, so a
  branch that has moved on shows its current content rather than a stale copy.

## Impact

- Affected specs: `connector-seam` (two vendor-abstract reads), `backlog-mirror` (the detail
  page's documents section — the read is live, so nothing joins the Mirror).
- Touched: Backlog module (seam, GitHub implementation, two read slices), frontend (documents
  list + renderer reusing #37's sanitiser), docs/product/mvp/04 (UC-023), tests.
- Out of scope: diffs, non-markdown files, approving or commenting from the portal, the
  Agent's Plan (UC-013, #22).
