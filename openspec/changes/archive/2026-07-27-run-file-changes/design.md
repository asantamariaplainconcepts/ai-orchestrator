# Design — run-file-changes

## D1 — One vendor call, two consumers

`ListChangeFiles` returns every changed file with its patch; `ListChangeDocuments` (#38)
becomes a projection over it. Today they would be two calls returning overlapping data from the
same endpoint — the documents list is literally the markdown subset of the files list. One call,
filtered twice, keeps the vendor round-trips honest and the two features consistent.

## D2 — The vendor's patch, never a diff we compute

GitHub returns a unified patch per file; Azure DevOps will too. Computing our own from two
content reads would re-implement what the vendor already did, produce subtly different hunks
per vendor, and double the reads. The patch crosses the seam as text.

## D3 — Absence is typed, not truncated

A binary file arrives with no patch; a very large patch arrives whole and would freeze a page.
Both become an explicit `PatchOmitted` reason (binary / too large) carried on the file, so the
UI states *why* there is no diff and links to the vendor. A truncated patch shown as if
complete is the failure mode this design exists to prevent — a reviewer would approve half a
change believing they saw all of it.

## D4 — The Runs module reads through Contracts, as always

Runs must not reference the Backlog implementation. `IChangeFileReader` joins `IStoryReader`
and `IConnectorReader` in `Backlog.Contracts`; the Runs read slice resolves the Run's Story,
asks for its linked change's files, and returns them. No new cross-module coupling shape.

## D5 — Colour distinguishes added from removed, and nothing else

Added and removed lines get kit-token backgrounds; hunk headers are muted. Syntax highlighting
would mean a tokenizer per language and a much larger surface for a review that mostly asks
"what moved". Explicitly out of scope, not deferred by omission.
