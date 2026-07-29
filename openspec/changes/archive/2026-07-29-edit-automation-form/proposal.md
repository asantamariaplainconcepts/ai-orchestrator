# Proposal: edit-automation-form

## Why

Issue #151 (ACT-001; UC-006, UC-005; BR-003, BR-005). UC-006 promised an Admin can edit an Automation
and *the backend delivers it*: `UpdateAutomation` replaces the whole Automation behind the same
`OverlapGuard` as create, with a validator whose own comment says "an edit cannot be laxer than the
thing it edits".

The portal never grew the form. The catalogue offers create, enable/disable and delete; the canvas
edits exactly two things. Every other field — trigger label, state, action, runtime, timeout, document
path — is reachable only by deleting the Automation and recreating it, which throws away its identity
and, with it, the audit trail every Run keeps a reference to (BR-014). The workaround costs history.

## What changes

- **One form serves create and edit** (design D1). Not two forms: the acceptance criterion is "the edit
  form mirrors create's rules", and two forms is the arrangement in which that stops being true
  without anyone noticing.
- **Edit seeds from the row and resends every field** (design D2), because the endpoint is a full
  replace. This is not a hypothetical: create currently sends `timeoutMinutes: null`, so reusing its
  submit unchanged would reset a 45-minute Automation to the default on any edit.
- **The timeout becomes a visible field** in both modes (design D2), which is what makes "resent as-is"
  something a person can see rather than something the code promises.
- **Changing the action to one that reads no document clears the document name** (design D3) — hidden
  because inapplicable is not the same as hidden because unshown.
- **The API's refusal renders on the form** (design D4), in create's voice: overlap (BR-003, `409`) and
  the self-trigger refusal.

## Impact

- Specs: `automation-configuration` — one MODIFIED requirement (the portal now reaches what the
  capability already promised).
- Code: frontend only. The form gains a mode and a timeout field; the row gains an edit control. The
  existing `useUpdateAutomation` hook and the existing `PUT` endpoint are used unchanged.
- No API change, no schema change, no new endpoint.

## Out of scope

- Reorganizing the catalogue or the workflow tab — #136 did that, and this lands inside what it drew.
- Enable, disable and delete: they exist and are not touched.
- Editing from the canvas beyond the two things it already edits.
- Per-role enforcement — an Admin-only form is not enforceable until OPN-002 closes, and pretending
  otherwise would be a lock with no key.
