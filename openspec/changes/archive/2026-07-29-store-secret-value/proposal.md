# Proposal: store-secret-value

## Why

Issue #124 (ACT-001 configures; UC-004). Connecting a backlog today asks for something nobody
has: the **name** of a secret that must already exist. `ConfigureConnector` resolves that name
before it will store anything, so a first-time user must leave the product, create the secret in
Key Vault or in a `Secrets__<name>` configuration entry, come back, and type the name exactly.
The product knows which credential it needs and where that credential should live. Asking the
person to put it there by hand is the tool declining to do its own job.

DEC-049 made self-hostability a product goal, which sharpens this: a self-hoster has no Key
Vault at all, so the naming path is not merely inconvenient for them, it is a second system they
must acquire before the first one starts.

## What changes

- **The Connector form accepts the token itself**, *alongside* naming an existing secret. An
  operator who already manages secrets keeps that path untouched — criterion 4 of the issue makes
  that a requirement, not a courtesy.
- **The product picks the name** (design D2): deterministic, derived from the project, never
  invented by the user, because a name a user chooses is a name they can collide with.
- **The secret seam gains storing** (design D1): a writing sibling to `ISecretResolver`, composed
  by the host exactly as resolution already is. A habitat whose store cannot accept a value
  refuses legibly and keeps the naming path working.
- **Stored, rotated, never read back** (design D3). No endpoint, log line, telemetry event or API
  response returns a value. The portal shows the name and when it was last set.
- **The self-host habitat stores locally, encrypted** (design D4): ASP.NET Core Data Protection,
  with the key ring persisted outside the database, so the ciphertext in Postgres is useless on
  its own. The framework's implementation or nothing — no hand-rolled cryptography.
- **BR-010 is revised, and the revision is locked as DEC-052** (design D5). Its letter — "PATs
  exist in Postgres only as Key Vault references" — cannot survive a habitat with no Key Vault.
  Its intent — a leaked database is not a leaked credential — survives encryption intact.

## Impact

- Specs: `connector-configuration` (one ADDED — the paste path; two MODIFIED — what is persisted,
  and what the seam does), `backend-architecture` (one MODIFIED — the host composes storing as it
  composes resolving).
- Docs: BR-010 reworded in `05-business-rules.md`; DEC-052 added to `10-locked-mvp-decisions.md`.
- Code: the writing seam and its two implementations; `ConfigureConnector` gains a second input
  and the store-then-verify ordering; the portal's Connector form gains the field.
- Schema: the Connector gains when its secret was last set. The encrypted local store is its own
  table, in the module that owns the seam's local implementation.

## The accepted risk has expired — and this change pays it

#124 recorded an accepted risk with an explicit expiry: the portal had no authentication, so an
endpoint accepting a credential would let any reachable stranger overwrite a project's token and
have the agent act with it. The expiry condition was **#119 landing**, and #119 is on `main`.

So the condition is not deferred here, it is implemented: storing a secret requires a principal
holding the Admin role, through the `ICurrentPrincipal` seam #119 introduced. This is the first
operation in the product to check a role, which makes it the first real exercise of BR-009 — and
that is a deliberate part of the slice, not a side effect.

## Out of scope

- Reading a stored secret back, ever, by any route.
- AI provider keys. The same seam will serve them; this slice is the Connector's token.
- Bring-your-own-vault configuration.
- Extending role checks to any other operation — BR-009 in general is a much larger item, and
  this change implements exactly the one check its own risk register demands.
