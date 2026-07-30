# Design: project-roles

## D1 — The check is a decorator, because omission must not be a way to pass

`AddVsaCqsArchitecture` composes a fixed chain and its own comment says the order "is not configurable
per call site". That is the property authorization needs: a use case that forgets to check must not
thereby become public.

So each command and query declares the permission it requires, and an authorization decorator enforces
it before the handler runs. The alternative — a check inside each handler — is what exists today in
`ConfigureConnector`, twice, hand-copied, and a third copy would have been written the next time
somebody needed one. BR-009 says *every operation names a required permission*; a declaration the
pipeline reads is that sentence made mechanical.

**The default is deny.** An operation that declares nothing requires Admin, not nothing: a new use case
added without thinking is then locked rather than open, and the failure mode of forgetting is a refusal
somebody notices rather than a hole nobody does.

## D2 — The principal answers *who*, a second seam answers *what here*

`Principal` carries `Role` today, and BR-009's roles are per project — so that field cannot be right.
It is not that it holds the wrong value; it is that the question "what is this caller's role" has no
answer without naming a project.

`ICurrentPrincipal` therefore keeps `Id` and `DisplayName` and loses `Role`. A new seam answers the
scoped question, and the decorator is its only consumer in this slice. Both live in BuildingBlocks
beside each other, because a module cannot compose either.

This is a breaking change to a Contracts type with three call sites, and that is the point of doing it
now: three is cheap, and every future feature that asks "may they?" would otherwise ask the wrong
question and get a plausible answer.

## D3 — Roles are rows on the project, in the Projects module

A role is a fact about a person's relationship to a project, and the Projects module owns projects. The
table lives in its schema, keyed by project and provider identity id, carrying one of DEC-034's two
bundles.

The identity id, not an email: emails change and get reassigned, and a role that follows a mailbox
follows whoever inherits it. The provider's stable object id is what #12 already records as the
principal's id, so the two agree by construction.

## D4 — Bootstrap administrators are configured, never claimed

Project-scoped rows create a chicken and egg: the first person to sign in has no role, so nobody can
grant one.

The rejected answer is **first-signed-in-user-claims-Admin**. It grants power by race — whoever reaches
the URL first wins — which is #12's interim rule with extra steps and a worse story, because now it is
permanent and invisible.

So configuration names them: a list of provider object ids that hold Admin on every project. Race-free,
auditable, revocable without a deploy, and it reuses the presence-of-configuration idiom every habitat
decision here already follows (`Identity:Mode`, `AzureAd:ClientId`, `Secrets:KeyVaultUri`).

**With none configured, nobody is Admin, and the portal says so.** That is the honest consequence and it
gets a voice rather than a silence — the same reasoning as #119's "this deployment authenticates
nobody" warning, which existed precisely so a temporary state could not become permanent unnoticed. A
deployment nobody can administer is a state an operator must be told about, not one they discover by
finding every button refused.

The list is deployed as a repository variable, like the Entra ids: object ids are not secrets, and they
stay out of git regardless.

## D5 — `/api/me` reports what is true after this, which is a different shape

Today it returns one role. After this, "your role" is not a fact without a project, so returning one
would be inventing an answer.

It reports the caller's identity plus their role per project they can see. The portal's shell shows the
name — which is all it ever needed — and screens that care about permission ask about the project they
are on.

## D6 — What is deliberately not built

**Inviting somebody who has never signed in.** A role attaches to a provider identity, and that identity
does not exist here until they sign in once. Pre-creating rows keyed by email would reintroduce exactly
the mailbox-following problem D3 rejects. It is a real limitation, named in the issue's out-of-scope,
and it wants its own slice with its own decision about what an invitation *is*.

**Tenant-wide administration.** Every role here is scoped to one project. A "platform admin" is a
different concept with a different blast radius, and inventing it as a side effect of making BR-009
enforceable would be the kind of scope growth that is hard to review.
