# Design — webhook-ingest

## D1 — The webhook triggers reconciliation; it is never parsed into an event

BR-015 says webhook and polling events must be identical before matching. Two ways to get
there: translate the payload into a `StoryChanged`, or make the webhook run the reconciler that
already produces them. The first creates a second normalisation path that must stay
byte-identical forever — and BR-015 exists *because* those drift, so building two is choosing
the drift. The second makes identity structural: there is one producer of story events, and a
webhook is just an earlier reason to ask it to look.

The cost is honest and small: reconciling fetches the repository's Stories rather than reading
one from the payload. For a backlog-sized repository that is one API call we were going to make
within the poll interval anyway.

## D2 — Signature verification is not optional, and is constant-time

The endpoint is unauthenticated by necessity — the vendor calls it — and it triggers work. HMAC
over the raw body with the Connector's secret, compared with a fixed-time comparison so the
check cannot be turned into an oracle. An unsigned or wrongly signed request is refused before
anything is looked up.

## D3 — Refusals do not leak existence

An unknown repository and a bad signature both answer the same way. A webhook endpoint that
distinguishes them tells an unauthenticated caller which repositories this installation watches.

## D4 — Uninteresting events succeed without working

A vendor that receives errors eventually stops delivering. Anything not worth reconciling —
a ping, an event type we do not act on — returns success and does nothing. "Accepted, ignored"
and "rejected" are different answers to different questions.

## D5 — Polling is not replaced, and the test says so

Webhooks are an optimisation over a baseline that must keep working: they are lossy (a delivery
can fail, a secret can rotate, an outage can swallow an hour of them) and BR-008's mirror must
converge regardless. The poller keeps its interval, and a test asserts a Story changed with no
webhook still reconciles.
