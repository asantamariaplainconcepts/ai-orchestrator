# Design: entra-sign-in

## D1 — A BFF, because the shape was already decided

DEC-058 settled this: the portal is a same-origin single web app, so it authenticates as a
backend-for-frontend. Microsoft.Identity.Web, code flow, the session in an `HttpOnly` cookie, the
client secret redeemed server-side by vault reference. No token in the browser, which also means no
token refresh logic in JavaScript, no storage question, and no XSS-reads-the-token class of bug at all.

The two traps DEC-058 recorded become configuration in this slice, not discoveries later:

- **`SameSite=Strict` for the session cookie only.** The OIDC handshake cookies stay at the library's
  defaults, because that response arrives cross-site from `login.microsoftonline.com` and `Strict`
  would drop them — failing sign-in in a way that looks like nothing happened.
- **The local profile is plain http** (`http://localhost:5080`), so cookie security policy must be
  conditional in development or the session never arrives. Entra permits http redirect URIs for
  localhost only, which is why this works at all.

## D2 — Two modes, chosen by what exists rather than by what a name claims

The modes the product already has are the modes it keeps:

| Mode | Trigger | Principal |
|---|---|---|
| Local owner | `Identity:Mode=LocalOwner` | the machine's owner, Admin, no sign-in |
| Hosted with Entra | `AzureAd` configuration present | whoever signed in |
| Hosted without a provider | neither | `UnauthenticatedCaller`, warned at startup |

Entra composition keys on **configuration presence** — the same idiom the local-owner guard already
uses for the vault URI, and for the reason its own comment records: environment names lie. The
self-host compose sets no `ASPNETCORE_ENVIRONMENT`, ASP.NET defaults it to Production, and gating on
that once refused to start the very habitat DEC-049 protects.

The third row survives on purpose. The self-host compose has no tenant to sign into, and deleting its
stopgap here would break DEC-049's habitat as a side effect of authenticating a different one. It
keeps its startup warning; it is a state with a voice.

## D3 — The principal is the session's, and the role is deliberately too broad

The hosted `ICurrentPrincipal` reads the authenticated `HttpContext.User`: object id as the stable id,
name claim as the display name. Consumers keep never branching on whether identity is configured —
the seam's contract from #119 holds.

Every signed-in user gets **Admin**, and that is stated in the requirement rather than smuggled: role
assignment per project is #13, sitting on this same seam. The alternative — inventing a mapping here —
would be proposing #13 inside #12. The honest sequencing is: #12 makes "who" true, #13 makes "what may
they do" true. Until #13, signing in is the boundary.

## D4 — A 401 is an answer, a redirect is an ambush

Browser navigations that need a session are challenged to Entra — that is what sign-in *is*. API calls
are not: an XHR that gets a 302 to `login.microsoftonline.com` receives an opaque CORS failure and the
page breaks mysteriously mid-use.

So the split is by surface: navigation challenges, `/api/*` returns `401` with a problem body. The SPA
already surfaces API errors through `ApiError`; a `401` becomes "your session ended, sign in again"
rather than a hung request. This is the standard BFF arrangement, and the reason it needs saying is
that the default template does the wrong one for APIs.

## D5 — Signed out means both sessions end

Sign-out ends the cookie session *and* redirects through Entra's end-session endpoint — the
front-channel logout URL `entra-app.sh` registered is the other half of that handshake. Ending only
the cookie leaves the Entra session alive, and the next challenge signs the user straight back in,
which reads as "sign out does nothing".

What the user lands on is the signed-out state of the portal, which offers sign-in. No auto-challenge
on arrival: a person who signed out chose to.

## D6 — The tiers keep their seams, which is why this is cheap

Functional tests inject `ICurrentPrincipal` — unchanged, that is what the seam is for. E2E runs the
real host with no `AzureAd` configuration, landing in the warned stopgap row — unchanged. The only
tier that can exercise the Entra branch is a functional test that *sets* `AzureAd` configuration and
asserts the composition: an unauthenticated `/api/*` call answers `401`, and a navigation challenge
points at `login.microsoftonline.com`. That asserts the wiring without needing a live tenant in CI —
the tenant was exercised for real once, by DEC-058, which is what reality checks are for.
