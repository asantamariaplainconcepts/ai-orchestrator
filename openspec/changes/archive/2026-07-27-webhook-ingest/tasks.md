# Tasks — webhook-ingest

## 1. The Connector's secret

- [x] 1.1 `WebhookSecretName` on Connector (nullable — webhooks are opt-in) + migration; the
      configure slice accepts it; it is a name, never a value (BR-010).

## 2. The endpoint

- [x] 2.1 `POST /api/webhooks/github`: read the raw body, resolve the Connector from the
      repository the payload names, verify HMAC constant-time (design D2), refuse
      indistinguishably (D3), acknowledge-and-ignore what is uninteresting (D4).
- [x] 2.2 On an interesting event, run `BacklogSynchroniser` — the same call the poller makes
      (design D1). Nothing is read from the payload beyond routing.

## 3. Tests

- [x] 3.1 A signed event reconciles and produces the same story event a poll produces.
- [x] 3.2 Bad signature, missing signature and unknown repository are all refused the same way.
- [x] 3.3 An uninteresting event returns success and does not reconcile.
- [x] 3.4 Polling still reconciles when no webhook arrives (design D5).
- [x] 3.5 No secret value appears in any response or log.

## 4. Close-out

- [x] 4.1 README: configuring the webhook at the vendor, and that polling remains the baseline.
      CI's own filtered command; CI green.
