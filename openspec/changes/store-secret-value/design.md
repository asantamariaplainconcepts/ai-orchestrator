# Design: store-secret-value

## D1 — A second seam, not a wider one

`ISecretResolver` is deliberately tiny: one method, resolve by name, per read. Widening it to
`Store` and `Rotate` would force every implementation and every test double to grow methods most
call sites must never reach — and the property that matters here is precisely that *most call
sites must never reach them*. A reader that could also write is a reader one refactor away from
writing.

So storing lives in `ISecretStore`, a sibling composed by the host the same way. The Runs module,
the connectors and the dispatcher keep depending on `ISecretResolver` alone; only the Connector's
configuration slice takes the store. The dependency graph then says out loud which single place
in the product can write a credential.

The store's contract is `Store(name, value)` and nothing else. There is no `Read`. Absence of a
method is the strongest guarantee available, stronger than a rule in a document, because it
cannot be forgotten under deadline.

## D2 — The product names the secret, from the project

The name is derived: a fixed prefix, the vendor, and the project's id. Three properties follow
that a user-chosen name cannot offer.

It cannot collide, because project ids do not. It is idempotent, so rotating writes the same name
and there is no orphan to clean up. And it is reconstructible, so an operator staring at a vault
can tell which project a secret belongs to without a lookup table.

The user never sees a naming decision. The existing `secretName` field stays exactly as it is for
the operator who supplies their own, and the two paths are distinguished by which field arrives —
not by a mode flag, because a mode flag is a third state that both paths then have to handle.

## D3 — Store first, verify second, persist third

The current ordering resolves the named secret, verifies access against the live vendor, and only
then writes the Connector — "a Connector that exists is one that works" (UC-004). The paste path
must keep that promise while adding a step that can fail on its own.

The order is: store the value, verify with the value we just stored, and write the Connector only
if verification passed. Verifying with the *stored* value rather than the one in the request is
the point — it proves the round trip, so a store that silently truncates or a habitat whose write
did not take is caught here rather than at the first poll.

A verification failure leaves a secret in the store with no Connector referencing it. That is the
right way round: an orphaned secret is inert, whereas a Connector pointing at a credential nobody
verified is the exact failure UC-004 exists to prevent. The deterministic name (D2) means the next
attempt overwrites it rather than accumulating.

## D4 — The self-host habitat encrypts, using the framework's own primitive

Key Vault holds the value in the deployed habitat, unchanged. The self-host habitat has no vault,
so its store is a table, and the value in that table is ciphertext produced by ASP.NET Core Data
Protection with the key ring persisted to a mounted path — outside the database, so a stolen
dump is not a stolen credential.

No hand-rolled cryptography, in the strict sense: no key derivation we invented, no cipher choice,
no IV handling, no bespoke envelope format. This is not a stylistic preference. Every one of those
decisions has a well-known wrong answer that looks correct in a passing test, and Data Protection
exists precisely so that applications stop making them.

The trade-off, stated plainly rather than buried: an operator who loses the key ring cannot
recover the tokens. That is the correct failure — the alternative is a key recoverable from the
same dump as the data, which is not encryption, it is obfuscation with extra steps. Re-pasting a
token is a minute's work; a database dump that yields live credentials is not recoverable at all.

## D5 — BR-010's intent survives; its letter cannot

BR-010 says Connector PATs exist in Postgres and logs "only as Key Vault references". That
sentence encodes a mechanism, and DEC-049 introduced a habitat where the mechanism does not
exist. A rule stated as a mechanism becomes false the moment the mechanism becomes optional.

The revision states the intent instead: **no secret in plaintext at rest outside the habitat's
secret store, and names only in logs, API responses and telemetry.** Every existing guarantee
holds under this wording — Key Vault deployments are unchanged, nothing new is logged, no
response gains a value — and the self-host habitat becomes expressible rather than
non-compliant-by-construction.

Locked as DEC-052 at propose time, because a business rule that changes silently is a rule the
next reader cannot trust.

## D6 — Storing requires an Admin, and this is the first role check in the product

#124's accepted risk expired when #119 landed. The safeguard it named is implemented here: the
store path reads `ICurrentPrincipal` and refuses a caller who is not an Admin.

This makes it the first operation in the product to check a role. BR-009 has been documented and
unimplemented since the beginning, waiting for "operations to name their permissions" — and the
honest place for the first one is the endpoint that accepts a credential, not a general
authorisation sweep chosen for tidiness. The check is one condition in one slice; generalising it
is a separate item, and doing that generalisation here would hide this change's real subject.

Note what it does not claim: on a machine-local habitat the principal is the machine's owner and
holds Admin, so the check passes trivially. It is not theatre — it is the seam being in place, so
that when an identity provider arrives the endpoint is already asking the right question.
