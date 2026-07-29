# Design: edit-automation-form

## D1 — One form, two modes

The create form and the edit form validate the same things, offer the same vocabularies, and refuse for
the same reasons. The acceptance criterion says so outright: "the edit form mirrors create's rules".

Two components would satisfy that on the day they are written and stop satisfying it silently
afterwards — the next field added to create is a field edit forgets, and nothing fails. So the existing
form takes a mode: absent means create, an Automation means edit. The submit picks the endpoint; the
fields, their rules and their refusals are one implementation.

This is the same argument #150 used for keeping one relabelled document field instead of two inputs:
where two surfaces must agree, one surface is how they agree.

## D2 — An edit resends everything, and the timeout is why that must be visible

`PUT /api/projects/{id}/automations/{id}` is a **full replace**: its `Command` carries the trigger,
action, runtime, approval flag, timeout, document path and output label, and whatever it is given wins.
A field omitted is not "left alone" — it is replaced with the default for absent.

Create's submit sends `timeoutMinutes: null`, because create has no timeout field and null means "use
BR-005's default". Reusing that submit for edit would therefore reset every Automation with a
configured timeout to 30 minutes, on any edit, for any reason — quietly, since the row would keep
rendering a number and the number would simply be a different one.

The canvas already codes around this, passing `timeoutMinutes: automation.timeoutMinutes` explicitly.
That precedent is evidence the trap is real rather than theoretical, and it is also the shape of the
fix: seed every field from the Automation, send every field back.

Rather than resend an invisible value, the timeout becomes a field in both modes. A value a person
cannot see is a value they cannot verify, and "resent as-is" is a promise about data the user is
entitled to look at. It also closes create's own gap: BR-005's bound is configurable per Automation and
the portal has never let anyone configure it.

## D3 — Switching action clears a document name that no longer applies

The document field appears only for the actions that read one (the grill's rubric, the repository
prompt's file name). On edit, an Admin may switch a grill to `TransitionState`, and the stored path then
belongs to nothing.

It is cleared, not carried. A hidden value that no visible control can change or clear is a value the
Admin cannot manage — and on the next edit it would be resent, so it would persist forever without ever
being displayed. This does not contradict D2's "resend everything": that rule is about fields the form
does not *show*, while this is a field the form has deliberately made inapplicable.

## D4 — The refusal is the API's, in create's voice

Overlap is `Error.Conflict` — `409`, not `400` — and it arrives as an RFC 7807 problem whose `detail`
`ApiError` already surfaces. The self-trigger refusal (#115) arrives the same way.

The form renders that text. Substituting a generic message would throw away the one thing that tells
the Admin which other Automation they collided with, and the create path already learned this lesson.

## D5 — Enabled is out of reach by construction, not by discipline

"An edit does not change whether the Automation is enabled" is guaranteed by the contract rather than
by care: `UpdateAutomation.Command` has no `Enabled` member, and enable/disable is a separate
`SetEnabled` command behind its own two routes. The form cannot change the flag because the endpoint it
posts to cannot express it.

Worth stating in the spec anyway, and worth a test — the guarantee is only as durable as the command's
shape, and a future field added there would remove it without touching this change.
