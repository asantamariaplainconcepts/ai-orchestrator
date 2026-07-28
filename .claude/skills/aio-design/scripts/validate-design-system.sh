#!/usr/bin/env bash
# Three-stage design-system validator. Runs identically locally and in the CI lint lane.
#
#   1. adherence     — frontend source uses tokens, not literals
#   2. drift         — the generated layers match the canonical one
#   3. skill hygiene — the design skill carries no values, so it cannot drift
#
# Scoped deliberately: the canonical layer is *supposed* to contain values, and generated files
# are derived from it, so both are excluded. Rules apply to application source only.
#
# Since DEC-051 there are two token sources: this kit (retiring, screen by screen) and the
# Platform theme (@plainconceptsplatform/ui-theme, vendor code under node_modules and already
# excluded). The job here never changes: no visual decision in app code outside a token source —
# migrated screens take values through Tailwind utilities, unmigrated ones through kit classes,
# and neither may carry a raw hex, a raw px, or an off-stack font.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
cd "$REPO_ROOT" || exit 1

SRC="src/frontend"
SKILL=".claude/skills/aio-design"
GEN_TOKENS="$SRC/shared/design/tokens.ts"
FAILED=0

fail() {
  printf '\033[31mFAIL\033[0m  %s\n' "$1"
  FAILED=1
}
pass() { printf '\033[32mok\033[0m    %s\n' "$1"; }

# Application source only: exclude node_modules, build output, and the generated adapter.
sources() {
  find "$SRC" \( -name '*.ts' -o -name '*.tsx' -o -name '*.css' \) \
    -not -path "*/node_modules/*" \
    -not -path "*/dist/*" \
    -not -path "$GEN_TOKENS" \
    -print 2>/dev/null
}

echo "── stage 1: adherence ──────────────────────────────────────────────"

# Raw hex colours. Six- and three-digit forms in a style or value position. Excludes comment
# lines so an explanatory note mentioning a hex is not a violation.
#
# `#110` is a valid three-digit colour AND this repository's way of writing an issue number, and
# the line-comment exclusion below never covered JSX comments — `{/* … */}` continuation lines
# start with neither `//` nor `*`. That collision cost two changes (it was worked around in
# #108's comment wording, then recurred here), so it is now handled rather than dodged: an
# all-digit `#NNN` reference is not a colour. Any hex containing a-f still fails, and a genuine
# digits-only colour would still be caught in a value position (`: "#110"`, `:#110`).
HEX=$(sources | xargs grep -nE '#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b' 2>/dev/null \
  | grep -vE ':\s*(//|\*|/\*)' \
  | grep -vE '#[0-9]{1,4}\b\s*(—|-|:)?\s*(issue|PR)' \
  | grep -E '[:"'"'"'(]\s*#[0-9a-fA-F]{3,6}\b|#[0-9a-fA-F]*[a-fA-F][0-9a-fA-F]*\b' || true)
if [ -n "$HEX" ]; then
  fail "raw hex colour — use a colour token (see DESIGN.md)"
  echo "$HEX" | sed 's/^/      /'
else
  pass "no raw hex colours"
fi

# Raw pixel values in CSS declarations, where a spacing/radius/type token exists.
# 0px and 1px are allowed: hairline borders and zero offsets have no token and never will.
PX=$(sources | grep -E '\.css$' | xargs grep -nE ':[^;]*[^0-9a-z-]([2-9]|[1-9][0-9]+)px' 2>/dev/null \
  | grep -vE '^\s*/\*' || true)
if [ -n "$PX" ]; then
  fail "raw pixel value — use a spacing/radius/type token"
  echo "$PX" | sed 's/^/      /'
else
  pass "no raw pixel values"
fi

# Font families outside the approved stack.
FONT=$(sources | xargs grep -nE 'font-family\s*:' 2>/dev/null \
  | grep -vE 'var\(--font-(sans|mono)\)|font-family:\s*inherit' || true)
if [ -n "$FONT" ]; then
  fail "font-family outside the approved stack — use var(--font-sans) or var(--font-mono)"
  echo "$FONT" | sed 's/^/      /'
else
  pass "fonts come from the token stack"
fi

# Hardcoded user-facing JSX copy. ESLint owns this rule; run it so the verdict is identical.
if [ -d "$SRC/node_modules" ]; then
  if (cd "$SRC" && pnpm exec eslint . --max-warnings=0 >/tmp/aio-eslint.log 2>&1); then
    pass "no hardcoded user-facing copy (eslint)"
  else
    fail "hardcoded user-facing copy, or another lint error"
    sed 's/^/      /' /tmp/aio-eslint.log | head -20
  fi
else
  printf '\033[33mskip\033[0m  eslint (dependencies not installed)\n'
fi

echo "── stage 2: drift ──────────────────────────────────────────────────"

if node "$SKILL/scripts/sync-design-tokens.mjs" --check >/tmp/aio-drift.log 2>&1; then
  pass "generated layers match the canonical tokens"
else
  fail "generated layers have drifted from the canonical tokens"
  sed 's/^/      /' /tmp/aio-drift.log
fi

echo "── stage 3: skill hygiene ──────────────────────────────────────────"

# The skill must be a router. A literal value here could contradict the canonical layer silently.
# Scripts are excluded: the generator necessarily handles values as data.
SKILL_VALUES=$(grep -rnE 'oklch\(|#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b|[^0-9a-z-][0-9]+px' \
  "$SKILL" --include='*.md' 2>/dev/null || true)
if [ -n "$SKILL_VALUES" ]; then
  fail "the design skill contains literal values — it must be a value-free router"
  echo "$SKILL_VALUES" | sed 's/^/      /'
else
  pass "the design skill is value-free"
fi

echo "────────────────────────────────────────────────────────────────────"
if [ "$FAILED" -ne 0 ]; then
  echo "design-system validation FAILED"
  exit 1
fi
echo "design-system validation passed"
