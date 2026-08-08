# ADR-0019: a whole-object replace has one builder

- **Status:** Accepted
- **Date:** 2026-08-08
- **Deciders:** the repository owner (solo path, DEC-016)
- **Tags:** backend, frontend, correctness

## Context

`PUT /api/projects/{id}/automations/{id}` replaces the whole Automation. That is a reasonable
endpoint shape and it has one property nobody keeps in mind: **a caller that omits a field clears
it**, silently, with a 200.

The Automation's own API comments have warned about this since the field before last — "the update
endpoint replaces the whole Automation, so a caller that cannot read this field would silently
clear it on every edit" — and the warning has not been enough. Three instances, found by three
different accidents rather than by looking:

- **`previewPort`, the create endpoint.** The POST handler never forwarded `request.PreviewPort` to
  its command. Found while adding an adjacent field.
- **`previewPort`, the form.** The Automation form never reads or sends it, so editing an Automation
  through the UI clears its preview port. Found by grepping the frontend for the field and getting
  nothing. Spun off as its own task.
- **`model`, twice in one week.** #291 added it; the create endpoint dropped it on the way to the
  command (found because a functional test asserted on what the runtime was *handed*, not on what
  was stored), and the workflow canvas — a third caller nobody thought about — resent every field
  except that one, so any drag or approval toggle reverted a chosen model to the deployment's.

The pattern is not carelessness. Every one of these is invisible at the call site: the request
object is valid, the types check, the field is optional, and the test that would catch it is a test
somebody has to think to write. The failure is structural, so the defence has to be.

## Decision

Where an endpoint replaces a resource wholesale, the request sent to it SHALL be constructed by
**one function per client**, and every caller in that client SHALL go through it.

Adding a field to such a resource SHALL include updating that builder, and the field SHALL be
carried by every client that can edit the resource at all — a client that cannot read a field must
not be able to write the resource.

Where a client genuinely cannot carry a field, that is a signal the endpoint wants a partial update
rather than a replace, and the endpoint SHALL be changed rather than the field quietly lost.

## Consequences

- **Positive:** the one place is reviewable. "Does the builder carry every field?" is a question a
  reviewer can answer by reading one function, where "does every caller carry every field?" is a
  question nobody can answer by reading anything.
- **Positive:** it makes the next field's cost visible at the moment it is added, which is when it
  is cheap.
- **Negative:** a builder is indirection, and for a resource with two fields it is more ceremony
  than the problem deserves. The rule is for resources that replace wholesale, not for all of them.
- **Neutral:** it does not fix the instances already shipped. `previewPort` remains cleared by an
  edit through the form until its own task lands.

## Alternatives considered

- **Make the endpoint a partial update (PATCH semantics).** The honest fix and a bigger one: it
  changes what "absent" means for every field and every existing client at once. Worth doing if a
  third field goes missing; this ADR is the cheaper defence tried first.
- **Rely on the comments already there.** Rejected by evidence — the warning was written on the
  field that then went missing twice more.
- **A test per field per caller.** Rejected: that is the same "somebody has to think to write it"
  that failed three times, multiplied.
