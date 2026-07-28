# Design: signalr-log-window

## D1 — The portal listens to the database; the pod never speaks to the hub

#106 assumed the shape #96 matured: the pod opens an outbound SignalR connection and pushes into
a per-Run group. That design's own summary calls its authentication "the design wrinkle" — a
per-Run token resolved from the vault, mandatory while OPN-002 leaves the portal
unauthenticated, and a new credential path on the hot side of a Run.

There is a cheaper source of the same event. The worker already commits every line to Postgres,
and Postgres can say so: `NOTIFY` fires on commit, the portal holds one listening connection,
and the payload is only a Run id — the portal reads the chunks it has not sent yet and pushes
them to the group.

What that buys, beyond fewer moving parts:

- **The ingest-authentication problem disappears** rather than being solved. There is no hub
  ingress to protect because nothing but the portal ever writes to the hub.
- **The worker is untouched.** Its only job stays "commit the line", which is also what makes
  the stream a witness and never a participant — the property #96 insisted on, now structural
  instead of maintained by care.
- **Acceptance criterion 4 becomes trivially true**: a pod cannot fail to authenticate to a hub
  it never contacts.
- It works identically in all three habitats: `LISTEN`/`NOTIFY` is core Postgres, present in the
  compose container and in Azure's flexible server alike.

The cost is that latency is now bounded by the flush rather than by the network, which D2
addresses, and that each portal replica holds one extra Postgres connection — a fixed cost per
replica rather than one growing with viewers.

**Rejected: the pod as SignalR client.** Genuinely lower latency (no flush in the path) and
genuinely more machinery: a client in the worker, a credential per Run, an authenticated ingress
on the hub, and a second delivery path whose divergence from the durable record would have to be
tested. Worth revisiting only if 500ms is ever measured as too slow.

## D2 — The flush interval is the latency budget, so it becomes 500ms

With the pod out of the delivery path, a line reaches a viewer at most one flush after the
runtime emitted it. 2s was chosen when the poll added 3s on top and precision was pointless;
sub-second requires the flush to be sub-second.

500ms, not less: a 30-minute chatty Run moves from ~900 commits to ~3,600, which Postgres does
not notice, while 100ms would quadruple that for latency no human can perceive. Batching by size
is untouched, so heavy output still commits in fifties and the interval never becomes the
bottleneck it was designed to bound.

## D3 — The poll remains the guarantee; the hub is best-effort

The page subscribes when it can and polls when it cannot, and the two cannot disagree because
both read the same table. The ≤5s promise DEC-050 made stays the contract; sub-second is what
you get when the hub is up. Nothing in the product depends on the hub being reachable — which is
what makes it safe to add without an operational story.

## D4 — Fanout is per Run group, and the listener is per portal replica

One SignalR group per Run id; a viewer joins on open and leaves on close. Each portal replica
listens to Postgres independently and pushes to the viewers it holds, so two replicas mean two
listening connections and no coordination. A line committed by the worker reaches every viewer
on every replica, because every replica hears the same `NOTIFY`.
