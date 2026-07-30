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

## D7 — The habitat decides whether roles exist, and it is asked rather than guessed

Three habitats, and only one of them has roles. On a machine its owner owns, and in DEC-049's
self-host deployment with no provider, there is one caller and everything is theirs — so the reader is
composed per habitat exactly as the principal beside it is, and the row-consulting one is registered
only where people sign in.

**The rejected version of this is worth recording, because it was written and it was a hole.** The
first draft asked the principal whether it was a "sole occupant" and derived that from its id — the
two sentinels `local-owner` and `anonymous`, which the portal's shell already tests for. But the
provider mode calls its *pre-sign-in* caller `anonymous` too, exactly as the provider-less habitat
calls its only caller. So "nobody is signed in yet" and "this person owns the machine" were the same
value, and an unauthenticated caller would have held Admin everywhere. The pipeline's 401 stands in
front of it, so nothing was reachable — but a permission model whose correctness rests on a carve-out
list in unrelated middleware is one bad exemption from a breach. Configuration answers it now, through
one shared question both the host and the module read.

Cross-project reads are the other half. `FiltersToCaller` is not "no requirement": those operations
narrow their own answer to the caller's projects. Without it a signed-in person holding nothing saw
every project's name, every connector's health and every waiting Story in the inbox — while every
operation on them was refused. That contradicts the refusals themselves, which are worded so as not to
disclose that a project exists.

## D9 — A surface the pipeline cannot see has to check for itself

The decorator covers everything dispatched through `ISender`, which is every endpoint that carries a
command or a query. The run-log **hub** carries neither: it joins a caller to a group and streams into
it, and it therefore declared nothing and the decorator never saw it.

That was harmless while being authenticated implied being permitted — every signed-in caller was Admin
— and this change is exactly what ended that. Left alone, the slice that scoped every other read of a
Run would have left the *live* stream of an agent's raw output open to any signed-in caller who knew a
Run id. So `Watch` resolves the Run's project and asks the same seam, refusing a Run in somebody else's
project and a Run that does not exist with one message, for the reason every other refusal here has one.

Found by re-reading ds-connect, which reaches the same property from the other end: its permissions are
endpoint policies, and its `AuthorizationOptions.FallbackPolicy` denies any endpoint that declared
nothing. The two default-denies cover different things — theirs catches an endpoint that forgot,
ours catches a *use case* that forgot — and the hub is in neither's blind spot by accident: it is in the
gap between them. Worth remembering as more non-dispatching surfaces arrive.

## D8 — Whoever creates a project administers it

D4's configured list solves the deployment's first administrator. It does not solve the second person,
or the project created next year: with rows only, a new project would have nobody able to configure it
unless the creator happened to be in the configuration.

So creating a project grants its creator Admin on it, in the same write as the project itself — no
instant exists in which a project has nobody able to configure it, and no second call could close that
gap because closing it would need the role.

This is not the race D4 rejects. That one hands administration of *existing* things to whoever arrives
first; this hands authority over one new thing to the person who made it, which is what "create" has
always meant.

The invariant it created, and broke once: **a role-holder is somebody this deployment has met**. The
creator got a role without a people row, so the grant surface answered "that person has not signed in"
about the person who had just created the project — found by a test trying to demote them. One writer
now records people, and both paths that create the obligation use it.
