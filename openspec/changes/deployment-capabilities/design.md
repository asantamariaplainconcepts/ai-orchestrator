## Context

Two facts about a deployment are decided at startup and never change while it runs: whether
callers sign in (`AzureAd:ClientId` present) and whether this is the machine one person owns
(`Identity:Mode=LocalOwner`). `IdentityHabitat` is where both are asked, deliberately below the
composition so the host and the Projects module cannot disagree.

The portal has no equivalent. #211 needed the posture to decide whether the code-source surface
exists, and — with no read to ask — inferred it by provoking `validate-path`'s 404. Nothing is
wrong with the *answer*; the problem is the question, and it is a question a second consumer now
also has (the credential's vault-dependent path).

## Goals / Non-Goals

**Goals:**
- One read the portal can ask, answered from the same source the modules use.
- Each posture offers only what can succeed there.
- The inference is deleted, not left beside its replacement.

**Non-Goals:**
- Changing `ValidateLocalPath`'s 404 — the surface-level gate stays; only the portal's *use of it
  as an oracle* goes.
- Any change to the credential's storage, resolution, or the API's exclusive-or rule.
- Deciding whether self-host could avoid a vendor credential entirely (#223, OPN-006).

## Decisions

**D1 — the read lives in Projects, beside `GetCurrentPrincipal`.** That endpoint already answers
"what is this habitat, for this caller"; capabilities answers "what is this habitat" full stop.
Putting them in one module keeps both derived from `IdentityHabitat` in one place. *Alternative
rejected:* a host-level minimal endpoint outside any module — it would be the only route not
composed by a module, and the ArchTests exist to keep that from happening quietly.

**D2 — the answer names capabilities, not configuration.** It reports whether the code-source
surface exists and whether a secret can be named — not `Identity:Mode`, not a vault URI. A portal
that learns *what it may offer* stays correct if the underlying condition changes; a portal that
learns *the mode* re-derives the same rules on the client and drifts. *Alternative rejected:*
returning the raw posture string — it invites `if (mode === "LocalOwner")` in three components.

**D3 — "a secret can be named" is derived from the vault's presence, not from the posture.** They
coincide today (self-host composes no Key Vault) but they are different facts, and the honest
condition is the one that makes the option succeed or fail. A future self-host with a vault would
then simply offer it.

**D4 — anonymous-readable, like `/api/me`'s shape question.** It discloses no project, no name and
no configuration value: it says which controls a form should render. Gating it behind a permission
would mean the sign-in screen cannot know what kind of deployment it is on.

## Risks / Trade-offs

- [One more request per page load] → it is a per-deployment constant; the query layer caches it
  with an infinite stale time, exactly as the probe was cached, so the count does not rise.
- [Two sources of truth if the probe is left behind] → the change deletes `useCodeSourceSurface`
  rather than layering on it; the tasks make that explicit.
- [A capability list grows into a dumping ground] → it holds only what a *rendering decision*
  needs; anything requiring a permission belongs on the resource it guards.

## Migration Plan

Additive on the backend, substitutive on the frontend. No schema, no configuration, no contract
change. Rollback is reverting.

## Open Questions

(none — #223 carries the one question this change deliberately does not answer)
