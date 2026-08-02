## Context

The Connector form grew a field per change — the code repository with the second vendor, the
prompts directory with #150, the credential mode with #124, the code source with #211 — and each
addition was locally correct. Nothing ever removed one, and the hints accumulated at the bottom
because that is where the form's paragraph slot happened to be. The result is eight controls of
equal visual weight, four of which are required.

What bounds the redesign is not taste: `ConfigureConnector`'s validator has **conditional** rules.
`LocalPath` is `NotEmpty` and `Path.IsPathFullyQualified` **when** `CodeSource = LocalFolder`. The
credential rule is exclusive-or (not both) and deliberately *not* not-neither — absence is the
handler's call, because whether a stored credential exists is a database fact (#160, design D1).
`CodeSource` and `Vendor` reject a misspelling rather than falling back. A form that folds fields
away has to respect which of them can become required.

## Goals / Non-Goals

**Goals:**
- A first connect asks four questions, and the rest are reachable in one deliberate gesture.
- Every hint sits beside the field it explains.
- The portal can never compose a request the API refuses for a reason the form could have
  prevented.

**Non-Goals:**
- Any change to the request shape, the validator, or the handler.
- A folder picker (still deferred — `local-code-source` spec's open question).
- Re-ordering or restyling the People card, or anything else on the Settings tab.
- Making the code source easier to *reach* — it stays in Advanced deliberately.

## Decisions

**D1 — Advanced is a disclosure, and its open state is derived, not remembered.** It opens when
the Connector already stores a prompts directory, a code repository, or a non-default code
source; it opens and **locks open** while LocalFolder is selected. *Alternative rejected:*
remembering the last open state per user — a stored preference would eventually hide a
newly-required field from somebody who collapsed it yesterday, which is precisely the failure
this rule exists to prevent.

**D2 — the credential is one input with a mode link, and the modes are mutually destructive.**
Switching clears the other value rather than preserving it, so the exclusive-or holds by
construction rather than by the form remembering to send only one. Blank still means "keep the
stored one" on an edit (#160). *Alternative rejected:* keeping both values in state and sending
only the active one — the invariant would then live in the submit handler, one refactor away from
being lost.

**D3 — hiding the code repository and clearing it are the same act.** Under LocalFolder the input
is not rendered *and* the request sends `codeRepository: null`. A hidden field whose stale value
still travels is the bug this rule forecloses; the validator does not forbid the combination, so
nothing else would catch it. *Alternative rejected:* disabling the field with its reason (the
pattern the Run-now dialog uses for the pod card) — there the disabled card teaches a constraint
the reader must understand to choose; here the field is simply inapplicable, and a disabled input
carrying a stale value is worse than an absent one.

**D4 — the four essentials are fixed, not computed.** Vendor, owner, repository, credential. The
vendor is essential despite having a default because choosing it changes what the other two
*mean* (owner/repository vs organisation/project), and a wrong vendor is verified against the
wrong host.

## Risks / Trade-offs

- [An Admin misses the code source because it moved into Advanced] → accepted deliberately: on
  cloud it does not exist at all (#211), and on self-host the owner who wants it is the one who
  went looking. The onboarding checklist (#211, mock 3d) is the surface that points at it.
- [A locked-open disclosure looks broken] → it states why it cannot close ("a local folder needs
  its path"), so the constraint reads as a rule rather than a bug.
- [Two hint paragraphs disappearing from the bottom loses information] → each becomes the hint of
  its own field; nothing is deleted, only relocated to where it applies.

## Migration Plan

Frontend-only and additive to behaviour: an existing Connector opens with Advanced already
showing whatever it stores, so no configuration becomes harder to reach than it is today.
Rollback is reverting the change.

## Open Questions

(none — the conditional-validation constraints were established at grill time on #220)
