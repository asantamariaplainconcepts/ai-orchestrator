# ADR-0010: A habitat contract is asked, never inferred

- **Status:** Accepted
- **Date:** 2026-07-30
- **Deciders:** repository owner; agent working #12, #13 and their hotfixes
- **Tags:** infra, security, backend, agent-behaviour

## Context

This product runs in three habitats: a machine one person owns (`aspire run`), a self-hosted
deployment with no identity provider (DEC-049), and the provisioned Azure deployment. Every seam here
is composed per habitat — secrets, identity, dispatch, the Data Protection key ring.

Between #12 and #13, **seven defects in a row** came from the same shape: code decided which habitat
it was in, or what that habitat provided, by inferring it from a value rather than by asking a
question whose answer is that fact.

- **#99, and again in `IdentityComposition`.** The environment name was used to mean "this is a
  deployment". The self-host compose sets no `ASPNETCORE_ENVIRONMENT`, so ASP.NET calls it Production,
  and the check refused to start the very habitat DEC-049 exists to protect.
- **#170.** `Microsoft.Identity.Web` was assumed to default its `Instance`. It does not, and it
  refuses *per request*, so the deployed portal answered 500 on everything — health probes included.
- **#174.** The OIDC handler built `redirect_uri` from the request scheme. Container Apps terminates
  TLS and forwards `http`, so the challenge asked Entra for a URL the registration cannot carry.
- **#176/#182.** "Strict breaks login" was inferred from a login loop. The cause was our own
  `RequireAuthorization` on a public bundle; two decision entries exist to walk the wrong inference
  back.
- **#180.** The Data Protection ring was left in memory. With `min_replicas = 0` and a revision per
  deploy, the process that issues a challenge is not the one that handles the callback.
- **#13, caught before it shipped.** A permission reader asked the *principal* whether it was the
  habitat's sole occupant, and derived that from its id — the sentinel `anonymous`. But the provider
  habitat calls its pre-sign-in caller `anonymous` too, exactly as the provider-less habitat calls its
  only caller. One value, two opposite meanings: "nobody has signed in" would have been read as "this
  person owns the machine", handing Admin to an unauthenticated caller.

The last one is the clearest, because nothing about it was a surprising fact about a library or a
platform. It was a value that two habitats legitimately share, used as though it identified one.

## Decision

**We will express every habitat fact as configuration that is asked for by name, and never infer it
from a value that more than one habitat can produce.**

Concretely:

- Composition keys on the **presence of the configuration that provides the capability** —
  `AzureAd:ClientId`, `Secrets:KeyVaultUri`, `DataProtection:KeyRingBlobUri` — never on an environment
  name, a bind address, or a sentinel identifier.
- When two components need the same habitat fact, they read it through **one shared question**
  (`IdentityHabitat.CallersSignIn`), so they cannot answer it differently.
- Where a habitat contract cannot be asked because it belongs to something we do not run — an
  ingress's forwarded scheme, a library's required option, a platform's scale-to-zero — the
  requirement is **stated in code at the composition site**, with the failure it prevents named.

## Consequences

- **Positive:** the three habitats stop being implicit. A reader of a composition site can see which
  question decides it and what the absence of the answer means. Adding a fourth habitat is a
  configuration decision, not an archaeology exercise.
- **Positive:** the class of bug where a value means two things dies at review, because the shared
  question has one caller-visible name.
- **Negative:** more configuration keys, and each one is another thing a deployment can get wrong.
  Mitigated by the standing rule that the absence of a key is a real, stated state — not a silent
  default (`Auth:BootstrapAdmins` empty means nobody administers anything, and the host says so).
- **Neutral:** the older habitat checks that predate this were fixed one hotfix at a time. Nothing
  sweeps for the next one; this ADR is what a reviewer cites.

## Alternatives considered

- **Keep inferring, and test each habitat.** Rejected: six of the seven were only found in the
  deployed habitat, which is the one hardest and slowest to exercise. The inference is the defect;
  more tests around it would have caught instances, not the shape.
- **One `Habitat` enum set at startup.** Rejected: it recreates the same failure one level up — the
  enum has to be *derived* from something, and every deployment that guesses wrong guesses wrong
  everywhere at once. Presence of the capability's own configuration is local and self-describing.
- **Treat the sentinel ids as the contract and document them.** Rejected on #13: documenting that
  `anonymous` means two things does not stop code from reading it as one.

## References

- Issues: #12, #13, #99, #170, #174, #176, #180, #182
- Decisions: DEC-049 (self-host habitat), DEC-058 (Entra + BFF), DEC-059/DEC-060 (cookie scope)
- Retro entries: `docs/process/retro-log.md`, the `sign-in-*`, `strict-with-landing` and
  `project-roles` entries — the last of which promised this ADR once #13 landed
- Related: ADR-0001 (verify claims by exercising them), ADR-0009 (a claim about existing behaviour
  cites where it lives)
