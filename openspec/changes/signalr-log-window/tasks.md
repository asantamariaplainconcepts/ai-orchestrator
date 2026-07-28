# Tasks — signalr-log-window

- [ ] 1.1 The Postgres notification: the writer signals on commit, and a hosted listener in the
      portal wakes on it (design D1) — one connection per replica, reconnecting on loss.
- [ ] 2.1 The hub: one group per Run, join on open and leave on close; the listener pushes the
      chunks a group has not seen (design D4).
- [ ] 3.1 `RunLogWriter`'s flush interval to 500ms (design D2), with the reasoning at the
      constant.
- [ ] 4.1 The Run page prefers the hub and falls back to the poll (design D3), with no visible
      difference beyond speed.
- [ ] 5.1 Tests: a line delivered without polling, two viewers both served, the fallback when
      the hub is unavailable, and a Run unaffected when delivery is broken.
- [ ] 6.1 The measured latency stated in the PR; CI green; evidence on #106.
