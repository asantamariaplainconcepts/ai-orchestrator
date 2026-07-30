# authentication Specification

## Purpose
TBD - created by archiving change entra-sign-in. Update Purpose after archive.
## Requirements
### Requirement: a user signs in through the identity provider, and the browser never holds a token

The hosted portal SHALL authenticate users through the configured identity provider as a
confidential web client whose secret arrives by vault reference (BR-010). The sign-in response
SHALL be delivered to the server — the library's sign-in-only shape is the id_token hybrid by
form post, which the first deploy established against the provider itself (#172) — and SHALL
never be exposed to browser script in any flow. The session SHALL be an `HttpOnly` cookie; no access token, id token or
refresh token SHALL be sent to the browser. The session cookie SHALL be `SameSite=Strict`; the
sign-in handshake cookies SHALL NOT be, because the provider's response arrives cross-site and a
strict handshake cookie fails sign-in silently.

An unauthenticated browser navigation SHALL be challenged to the provider. An unauthenticated
API call SHALL receive `401` with a problem body and SHALL NOT be redirected, because a redirect
answers a fetch with an opaque failure.

Signing out SHALL end both sessions — the cookie and the provider's — and SHALL land on a
signed-out page that offers sign-in without forcing it.

The identity endpoint SHALL reflect the signed-in user, so the portal shows who is working.

#### Scenario: signing in

- **WHEN** an unauthenticated user navigates to the portal with the provider configured
- **THEN** they are challenged to the provider, and on return they hold a cookie session and the
  portal shows their name

#### Scenario: no token reaches the browser

- **WHEN** a signed-in session is inspected
- **THEN** the browser holds an HttpOnly session cookie and no token in any script-readable
  location

#### Scenario: an API call without a session is refused, not redirected

- **WHEN** an unauthenticated request hits an API route
- **THEN** it receives 401 with a problem body, never a redirect toward the provider

#### Scenario: signing out ends both sessions

- **WHEN** a signed-in user signs out
- **THEN** the cookie session ends, the provider session ends through the registered
  front-channel logout, and the next visit challenges again rather than silently re-entering

#### Scenario: the session survives the round trip the local profile makes

- **WHEN** the developer profile runs over plain http on localhost with the provider configured
- **THEN** the session cookie still arrives, because cookie security policy is conditional in
  development — a Secure cookie over plain http is one that never comes back

