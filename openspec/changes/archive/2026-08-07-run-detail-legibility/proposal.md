## Why

The Run detail's two answers — what changed, and what to do about a failure — are crippled by
placement. The diff renders inside the 280px rail (`RunScreen.tsx:257`, mounted at `:440`), which
is illegible for a diff by definition; a failure is a red row in the rail (`:392–395`) while its
two decisions sit in the header's top-right (`:120–145`), far from the why, and the cause's
remedy is nowhere. run-on-a-pr just made this screen the place people read diffs. Issue
[#280](https://github.com/asantamariaplainconcepts/ai-orchestrator/issues/280), design review
turn 7.

## What Changes

- **The changes block moves from the rail to the body**: line numbers, a sticky per-file header,
  per-file collapse. The rail keeps only the summary (PR, files, ±) anchoring to the block.
- **A failure becomes a banner above the content**: the full reason, Run again and Dismiss inside
  it (and nowhere else), and — for causes with a known surface — a link to the remedy: an
  unresolved secret/credential → Connector settings; an unreadable prompt file → the Automations
  tab. No mapping, no link.
- **Empty Plan/Output cards collapse to one line.**
- **Mobile wraps instead of scrolling sideways**: +/− in a fixed gutter, paths truncated from the
  left, files collapsed from the second onward, long hunks behind "Show N more lines".

Frontend only; `GET /runs/{id}/changes` already carries everything needed. Not breaking.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `run-orchestration`: the file-changes requirement gains the reading contract (body width, line
  numbers, sticky/collapse, the rail-as-summary rule, the mobile no-sideways-scroll rules), and
  one added requirement — a failure arrives as a banner with its decisions and, where mapped, its
  remedy. The re-run requirement's behaviour is untouched; only the controls' stated home moves.

## Impact

`src/frontend`: `features/runs/RunScreen.tssx`-adjacent files — `RunScreen.tsx` (layout, banner,
empty collapse), `RunChanges.tsx` (line numbers, sticky, collapse, mobile), i18n keys, mock.
Tests: any E2E/functional pinned to the old placement, updated not weakened.
