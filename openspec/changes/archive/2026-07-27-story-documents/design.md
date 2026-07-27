# Design — story-documents

## D1 — "The linked change", not "the pull request", on the seam

The seam speaks product vocabulary: `FindLinkedChange(coordinates, vendorStoryId, …)` returns a
change with a number, a title, a URL and a head ref. GitHub answers it from the issue timeline's
cross-reference events (a PR that says "Closes #41" appears there); Azure DevOps will answer it
from work-item relations. Neither vocabulary leaks: `PullRequest` as a seam type would be a
GitHub noun the second vendor has to pretend to speak.

## D2 — Documents are the markdown the change touches

`ListChangeDocuments` returns the added-or-modified `.md` paths of the linked change;
`ReadDocument(path, ref)` returns its content at that ref. Deletions are excluded (a removed
document is not a document), and non-markdown is excluded because rendering arbitrary file
types is a different feature with different risks. The grill's rejected alternative — fetching
`openspec/changes/<slug>/proposal.md` — would bake our own process into a vendor-abstract
product and require resolving branch names to slugs.

## D3 — Live reads, no mirroring

The Mirror exists because Stories are polled and matched; documents are neither. Reading at the
head ref each time keeps BR-008 true with no cache to invalidate, and makes the "branch has
moved on" scenario correct by construction rather than by expiry policy. The cost is a vendor
call per document view, which is a page a human opens deliberately — not a poll.

## D4 — One sanitiser, already written

The renderer is #37's `renderStoryMarkdown`. A document from a repository is exactly as
untrusted as a description from the same repository; a second, subtly different sanitising path
would be the way one of them ends up weaker.

## D5 — Absences are distinguishable

"No linked change", "the change adds no documents", and "the document could not be read" are
three different facts with three different next actions, and the page says which. Collapsing
them is how a vendor outage reads as an empty specification.
