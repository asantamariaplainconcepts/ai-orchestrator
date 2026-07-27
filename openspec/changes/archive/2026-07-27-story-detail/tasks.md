# Tasks — story-detail

## 1. The mirrored field

- [x] 1.1 `Body` on Story (nullable, bounded) + migration; `VendorStory` carries it; the GitHub
      connector maps `issue.Body`; `UpdateFrom` counts it as a change (design D1).
- [x] 1.2 `StorySnapshot` (Backlog.Contracts) carries the body.
- [x] 1.3 Functional: an edited description is a change and re-announces; unchanged bodies stay
      a no-op poll.

## 2. The read slice + detail view

- [x] 2.1 `GET /api/projects/{projectId}/backlog/stories/{vendorStoryId}` (404 when absent);
      the list response is unchanged (design D4).
- [x] 2.2 Portal: detail route reached from the backlog table; markdown rendered through a
      no-raw-HTML parser plus an allow-list sanitiser (design D2); empty state; catalog copy.
- [x] 2.3 The XSS case is asserted, not assumed: a body with `<script>` and a `javascript:`
      link renders inert (unit or E2E — whichever can assert the rendered DOM honestly).
      **Chosen: E2E** — there is no frontend unit runner, and adding one for a single test
      would exceed the feature; the browser asserts `window.__pwned` never set, no surviving
      `script` element, and no `javascript:` href.

## 3. The prompt

- [x] 3.1 `RunExecutor` includes the body under a heading, bounded with an explicit
      truncation notice (design D3).
- [x] 3.2 Functional: the instruction handed to the fake runtime contains the description;
      an over-long body is truncated and says so.

## 4. Close-out

- [x] 4.1 UC-022 added to docs/product/mvp/04-mvp-use-cases.md; ARCHITECTURE.md touch if
      warranted; full suite + frontend lint/build; CI green.
