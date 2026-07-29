# Proposal: edit-connector-keeps-credential

## Why

Issue #160 (ACT-001; UC-004; BR-010). Configure Connector demands exactly one credential input —
"not both and not neither" (#124). That was the right rule for *connecting* and it silently forbids
*editing*: changing the prompts directory, the owner or the repository requires re-pasting a PAT the
product already holds, verified, under the Connector's own secret name.

This surfaced the day #150 shipped the prompts directory field — the Settings form refuses to save
because the Token field is empty. Asking a human to go and mint a token again in order to change a path
is friction UC-004 never intended, and it trains people to keep PATs lying around, which is the opposite
of what BR-010 is for.

The mechanical cause is worth naming, because it decides the design: the rule lives in the request
`Validator` (`ConfigureConnector.cs:110`), which runs before the handler and therefore cannot know
whether a Connector already exists. "Neither" is refused as a property of the request when it is really
a question about the world.

## What changes

- **The rule splits along that seam** (design D1). "Not both" stays in the Validator, because it is a
  fact about the request. "Not neither" moves to the handler, where the answer depends on whether a
  Connector exists.
- **An edit with no credential reuses the Connector's own secret name** (design D2), resolves it, and
  **re-verifies it** with the same `VerifyAccess` probe every connect uses — so "a Connector that
  exists is one that works" keeps holding. The value is not re-stored, not returned and not shown.
- **Keeping the stored credential becomes the default state of the form on an existing Connector**
  (design D3): the credential inputs are optional on edit, and say so.
- **A vendor switch without a new credential is refused, naming why** (design D4) — the stored secret
  was minted for the other vendor and cannot vouch for this one.
- **The reuse path carries the Admin check** (design D5), so editing configuration behind a stored
  credential is not less protected than pasting one (#119).

## Impact

- Specs: `connector-configuration` — one MODIFIED requirement (exactly-one becomes exactly-one *for a
  new Connector*), carrying its seven existing scenarios.
- Code: `ConfigureConnector` — the Validator loses one rule, the handler gains the decision and loads
  the Connector earlier so the stored name is available before resolution. The Settings form makes the
  credential inputs optional when a Connector exists.
- No schema change. No new endpoint: one configure surface, one set of refusals.

## Out of scope

- Rotating or deleting a stored credential — pasting a new one stays the rotation path, exactly as
  today.
- A settings-only endpoint, and per-field PATCH semantics: the form still submits the whole
  configuration.
- The role check on the **naming** path. It has never had one and this change does not add it; the
  asymmetry is noted rather than quietly fixed, because widening the first role check in the product
  deserves its own decision.
