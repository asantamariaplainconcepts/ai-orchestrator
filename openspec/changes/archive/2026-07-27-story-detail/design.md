# Design — story-detail

## D1 — The body is a mirrored field, not a fetch-on-demand

Reconciliation already computes "did anything change" from title/state/labels; the body joins
that comparison. Fetching the description lazily at detail-open would make the detail page a
second vendor call path with its own failure modes, its own rate-limit exposure, and no
`StoryChanged` when the requirement itself is edited — which is precisely the change an Agent
would want to react to. One source, one refresh path (BR-008, DEC-028).

## D2 — Sanitise at render, in the browser, with an allow-list

The body is untrusted input from any repository a project points at. Markdown renders through
a parser configured to emit no raw HTML, followed by a sanitiser with an allow-list — belt and
braces, because "the parser is configured safely" is one config regression away from an XSS.
Server-side stripping is deliberately NOT chosen: the Mirror must hold what the vendor holds
(BR-008), and a sanitised-at-rest body would silently differ from the issue it mirrors.

## D3 — The prompt gains the requirement, and that is the point

`StorySnapshot` already crosses to the Runs module through Contracts; the body rides along and
`RunExecutor` puts it in the prompt under a heading. The instruction stays what it was — the
Agent implements, the orchestrator publishes (#19 D1) — it simply now knows what to implement.
Length is bounded at the prompt, not at rest: a novel-length issue body is the vendor's truth,
but an unbounded prompt is a cost and timeout surprise.

## D4 — One read slice, not a widened list

The backlog list response stays as it is; a body per row would inflate every backlog read for
data one row at a time actually needs. The detail route reads its own endpoint.
