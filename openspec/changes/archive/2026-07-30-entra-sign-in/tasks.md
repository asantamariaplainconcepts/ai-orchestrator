# Tasks — entra-sign-in

- [x] 1.1 Microsoft.Identity.Web in the Server: code flow, cookie session, secret by vault
      reference (design D1).
- [x] 1.2 Cookie policy: SameSite=Strict on the session only; handshake cookies at library
      defaults; Secure conditional for the plain-http dev profile (design D1).
- [x] 2.1 Composition keys on AzureAd configuration presence; the LocalOwner mode and the warned
      stopgap are untouched (design D2).
- [x] 2.2 The hosted ICurrentPrincipal reads HttpContext.User: object id, name claim, Admin for
      every signed-in user — the interim rule stated in the requirement (design D3).
- [x] 3.1 The surface split: navigations challenge, /api/* answers 401 with a problem body
      (design D4).
- [x] 3.2 Sign-out ends the cookie and the provider session, landing signed-out (design D5).
- [x] 4.1 The SPA reacts to 401 by offering sign-in; the shell shows the signed-in name from
      /api/me and offers sign-out.
- [x] 5.1 Functional test with AzureAd configuration set: /api/* answers 401 unauthenticated, and
      a navigation challenge points at the provider — asserting the wiring with no live tenant
      (design D6).
- [x] 5.2 The existing tiers pass unchanged: functional tests keep injecting the principal, E2E
      runs providerless in the stopgap row.
- [x] 6.1 infra/README.md gains the Server configuration block entra-app.sh prints, wired to the
      vault reference.
- [ ] 7.1 CI green; evidence on #12, including a signed-in session on the deployed portal.
