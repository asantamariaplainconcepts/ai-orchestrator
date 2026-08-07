## Context

The Inbox (`GetInbox`, Runs module) is cross-project, returns a bare `Entry[]`, and one query feeds
both the page and the shell badge, which polls every 30 s from every page. The connector seam
(`IBacklogConnector`, Backlog module) has no repository-wide change listing — its closest member is
story-scoped (`FindLinkedChange`) — and its doc fixes the vocabulary: "change", never
"PullRequest". Runs already store where an implement Run landed (`Run.OutputLink`). Module rule:
Runs consumes vendor data through `Backlog.Contracts` only.

## Goals / Non-Goals

**Goals:** open changes visible in the Inbox as a distinct group; product-created ones marked with
a link to their Run; vendor stays the truth; the rate limit survives the feature.

**Non-Goals:** in-product review (diff/comment/merge); webhook-driven freshness; storing changes;
including changes in the ambient count; the Azure DevOps live path (keeps its unexercised reason);
launching Runs on a change (#275, sequenced behind this).

## Decisions

### D1 — A second endpoint, not a wider Entry

`GET /api/inbox/changes` beside `GET /api/inbox`, with its own frontend query.

*Rejected — widening the existing array with a discriminator.* The badge computes `data.length`,
so changes would silently inflate a count that means "Runs waiting on you"; and one query means the
shell's 30 s poll would carry N vendor reads from every page of the app. The bare-array response
shape is also consumed as-is by two call sites — an envelope change is a bigger break than a new
endpoint.

### D2 — The vendor read is page-scoped and slower

The changes query mounts with `InboxScreen` only and refetches at 120 s; the shell badge keeps its
existing query untouched. Cost bound: vendor reads happen only while somebody is actually looking
at the Inbox. This is the deliberate narrowing of #274's "included in the count" phrasing, made for
the seam's own rate-limit requirement, and stated in the delta spec so the narrowing is contract,
not accident.

### D3 — One new Contracts interface, one new seam member

`IChangeReader.Open(projectId, ct)` in `Backlog.Contracts`, returning open changes plus a
`Reason`-style failure, implemented by a `ChangeReader` adapter over a new
`IBacklogConnector.OpenChanges(coordinates, token, ct)`. GitHub: Octokit
`PullRequest.GetAllForRepository(open)`. The record carries the **head branch name** — the existing
`LinkedChange.HeadRef` is a SHA, useless for recognising `run/{id}` branches, so the new record is
its own type rather than a reuse that almost fits.

*Rejected — reusing `IChangeFileReader`.* Story-scoped by design; overloading it repo-wide would
give it two shapes of answer.

### D4 — The marker is a DB join, not a vendor question

`GetInboxChanges` joins the listed URLs against `Run.OutputLink` over the caller's visible
projects. Exact, no extra vendor read, and yields the Run id for the link back. Branch-prefix
matching (`run/{id}`) stays a fallback the head-branch field makes possible later, not a second
source of truth now.

### D5 — Distinct presentation, same page grammar

The group reuses the Inbox's section grammar (uppercase header, card, divided rows) with its own
identity: an outline/info treatment instead of the severity spine, the action chip reading as
"review on the vendor" (external link), and the product-created marker as a quiet badge linking to
the Run. Colour never alone, per the screen's own rule.

## Risks / Trade-offs

- **N projects → N vendor reads per refresh while the Inbox is open.** → Page-scoped + 120 s
  cadence; each project's failure degrades independently (one bad Connector must not blank the
  group).
- **The AzDO member ships unexercised.** → It answers with the same recorded reason the rest of its
  connector does; the delta spec says so explicitly.
- **GitHubStub has no `/pulls` route; E2E would 404.** → The stub gains one returning an empty
  list, so existing E2E flows see "no changes" rather than an error.
- **`Entry`'s vocabulary (`approval | input | failure`) is a compile-time gate two maps enforce.**
  → Changes are NOT a fourth `waitingFor` kind — they are their own type and their own group, so
  the maps stay untouched.

## Migration Plan

None. New endpoint + new seam member; rollback is not calling them.

## Open Questions

None.
