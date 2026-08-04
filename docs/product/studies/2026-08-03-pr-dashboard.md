# Study: davidfowl/pr-dashboard

**Date:** 2026-08-03 · **Source:** <https://github.com/davidfowl/pr-dashboard> (C#, Aspire, the
Aspire team's own PR dashboard) · **Asked by:** the owner — _what can we copy or improve?_

A study, not a backlog. Nothing here was filed; the point of writing it down is that the next
person who has one of these ideas finds the reasoning instead of proposing it cold.

## What it is

A PR-review dashboard: GitHub OAuth, GraphQL PR snapshots behind a two-layer cache (memory + blob
"last-good" so scale-to-zero cold starts still render), a web-push notification loop that turns
"you are a requested reviewer" into a phone notification via a PWA service worker, an agent review
queue endpoint, and a single-file `apphost.cs`. Dense and disciplined — comments state concurrency
assumptions explicitly, the same habit this repository keeps.

## Rejected, with reasons

- **Two-layer response cache with last-good fallback.** They cache because they have no mirror. We
  mirror by decision (BR-008): backlog reads never touch the vendor, so vendor-outage resilience for
  reads is already structural. Our remaining live reads are prompts at Run time, and #150 chose
  _live_ deliberately — a "last-good" prompt would execute something the repository no longer says.
  Their cache solves a problem we designed away.
- **Agent review queue (a ranking of what needs review).** Our Inbox already answers "what waits on
  a human", and it is subtraction-based — entries _leave_ when acted on — which is a stronger
  property than a ranking. `WaitingSince` already orders it.
- **Ready-to-merge detection.** The pulse strip is our domain's equivalent.
- **Single-file `apphost.cs`.** Cosmetic; ours carries its decision comments and stays.

## Worth taking, when its time comes: web push for the Inbox

The one genuine gap it exposes. UC-026's Inbox exists, but nothing _tells_ anybody — an
approval-gated Run waits silently until someone opens the portal, and #166 left "notifying anybody"
explicitly out of scope. Their shape maps onto ours cleanly:

- `NotificationDetectorService`: a background loop over state we already query (inbox entries),
  pushing on _new_ waiting — not on every scan.
- **VAPID keys as configuration-presence** — absent means the detector idles and says so. That is
  ADR-0010's pattern verbatim.
- Their stated concurrency assumption transfers whole: one detector instance (their AppHost pins
  MinReplicas = MaxReplicas = 1), ETag-guarded dedupe state as defence-in-depth. Locally a
  non-issue; deployed, the same pin.
- Push subscriptions are per-browser state that needs a store. Theirs is a blob; ours would be the
  Postgres we already require — no new infrastructure in either habitat.
- Cost: a PWA manifest + service worker on the frontend, a subscription endpoint, the detector, and
  the keys as parameters. Bounded, but real.

**Decision (owner, 2026-08-03): recorded, not filed.** Notifications were not the priority. If that
changes, this section is most of the grill: the actor is ACT-002, the use case is UC-026, and the
open questions are only (a) per-user versus per-deployment subscriptions while project roles are
young, and (b) whether the local habitat pushes at all or the desktop is assumed present.

## A smaller habit worth noticing

Their `PRODUCT.md` opens with brand personality, anti-references, and design principles in one
page. Our equivalent lives across `DESIGN.md` and the corpus; the _anti-references_ section
("avoid an Azure portal clone…") has no counterpart here and is a cheap, useful fence. Noted, not
filed.
