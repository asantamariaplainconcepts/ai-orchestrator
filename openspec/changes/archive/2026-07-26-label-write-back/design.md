# Design — label-write-back

## D1 — Write-back first, then the ordinary sync; never a local patch

The mirror is a read model of the vendor (BR-008). The endpoint writes the label at the vendor,
then calls the same `BacklogSynchroniser.Synchronise` the poller and refresh use. Consequences,
all deliberate: the mirror never shows a label the vendor rejected; the `StoryChanged` event
that matching consumes is produced by the reconciler exactly as if the label had been applied
at the vendor (DEC-027's "equivalent" made structural); and a write that succeeds followed by
a sync that fails leaves the mirror stale-not-lying, with the existing failure surfacing.

## D2 — The seam's write vocabulary is two verbs, not a generic patch

`ApplyLabel(coordinates, vendorStoryId, label, token, ct)` and `RemoveLabel(...)`. A generic
`UpdateStory` would invite the mirror to become writable field-by-field, which BR-008 forbids —
labels are the one thing UC-008 licenses, so the seam says exactly that. Errors reuse the
existing taxonomy (`VendorUnavailable`, `CredentialRejected`, plus `StoryNotFound` for a
story the vendor no longer has).

## D3 — Idempotent by vendor semantics

GitHub's add-label is add-to-set and remove-label of an absent label is a 404 the
implementation translates to success — applying a label twice or removing a missing one is a
no-op, not an error. The endpoint inherits that: PUT and DELETE are idempotent, matching their
HTTP contracts.

## D4 — UI offers the labels that mean something

The backlog row composes with the automations query it already shares a page with: enabled
Automations' trigger labels not on the Story render as apply affordances; trigger labels
present render with a remove affordance; every other label stays a read-only pill. Arbitrary
label management is a vendor concern the vendor's own UI already does better.
