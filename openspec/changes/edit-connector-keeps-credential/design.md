# Design: edit-connector-keeps-credential

## D1 — "Not both" is about the request; "not neither" is about the world

One rule was doing two jobs. `Must(SecretName is blank != AccessToken is blank)` is an XOR over the
request, evaluated by FluentValidation before the handler runs — which means it is evaluated where the
database is not.

That is exactly why editing broke. Whether "neither" is acceptable depends on whether this project
already has a Connector with a secret name to fall back on, and no amount of care in a request
validator can answer that.

So the rule splits along the seam it was straddling:

- **The Validator keeps "not both."** Two credential inputs is a caller who believes two different
  things about where the credential lives, and that is wrong regardless of what the database holds.
- **The handler decides "not neither."** With a Connector, absent means *keep what you have*. Without
  one, absent still means there is nothing to verify against, and the refusal reads as it does today.

## D2 — Reuse resolves and re-verifies; it does not trust

The stored credential is resolved by the existing Connector's own `SecretName` and then put through the
same `VerifyAccess` probe as any connect.

Re-verifying is not ceremony. The reason it must happen is that an edit can change the very thing the
credential is checked against: a new owner or repository is a new question about whether that PAT can
read this. Skipping the probe would let an edit produce a stored Connector that has never worked — which
is the precise failure UC-004's verify-before-persist exists to prevent, arrived at from a new
direction.

The value is resolved, used for one probe, and dropped. Nothing is re-stored (there is nothing new to
store), nothing is returned, and BR-010 is untouched: the name travels, the value does not.

## D3 — The Connector is loaded before the credential is chosen

Today the handler resolves and verifies, then loads the Connector to decide create-or-update. Reuse
needs the stored name *before* resolution, so the load moves up.

Two consequences worth stating. It removes a second query rather than adding one. And the ordering that
the existing comments defend — store before verify, so a value that failed to round-trip is caught here
rather than at the first poll — is unaffected: that ordering is between *storing* and *verifying*, and
this moves a *read* that happens before both.

## D4 — A vendor switch cannot reuse the other vendor's credential

The derived secret name is a function of the project **and the vendor**. A Connector configured against
GitHub holds a GitHub PAT; switching it to Azure DevOps while supplying nothing would resolve a
credential minted for a different system and probe the wrong vendor with it.

That is refused, naming why, rather than attempted. The probe would fail anyway in almost every case —
but "almost every" is the problem: a refusal that explains beats a vendor error that has to be
interpreted, and the Admin's actual next step is to paste the new vendor's token.

## D5 — The reuse path is not a way around the role check

#119 put the product's first role check on the path that stores a credential. Reuse stores nothing, so
the check would not fire — and configuration would become editable by a caller who is not allowed to
paste a token, which inverts the point.

So the reuse path carries the same Admin check. Note what this does *not* change: the naming path
(supply an existing secret's name) has never had a check, and this change does not add one. Widening
the product's first role check is a decision with its own consequences, and doing it as a side effect
here would be the kind of quiet scope growth that is hard to review.

## D6 — The form's default on an existing Connector is "keep it"

The credential inputs become optional once a Connector exists, and the form says so rather than leaving
an empty required-looking field. The submit guard that today refuses to send without a token is what
made this a hard stop in the portal even before the API refused, so it goes too.

Pasting still works and still means *replace* — rotation is unchanged. The difference is that the
default action on an edit is now the safe one, which is the state a person is usually in when they open
Settings to change a path.
