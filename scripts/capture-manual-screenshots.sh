#!/usr/bin/env bash
# Recapture the screenshots in docs/product/manual/.
#
# Source is the **mock preview**, never a real tenant: the manual must not carry somebody's project
# names, repositories or object ids. Start it first — `pnpm dev` in src/frontend, or the
# `frontend-mock` preview — then run this.
#
# Playwright's own CLI does the driving; the browser is the one the E2E suite already installs, so
# there is nothing extra to acquire.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI="${ROOT}/src/tests/AiOrchestrator.EndToEndTests/bin/Debug/net10.0"
OUT="${ROOT}/docs/product/manual/img"
BASE="${BASE:-http://localhost:5173}"
# Any project from the mock's seed; the id is stable there.
PROJECT="${PROJECT:-00000000-0000-7000-8000-00000000000a}"

command -v pwsh >/dev/null || { echo "pwsh is required (brew install powershell)" >&2; exit 1; }
[ -f "${CLI}/playwright.ps1" ] || { echo "Build the E2E project first: dotnet build src/AiOrchestrator.slnx" >&2; exit 1; }
curl -sf -o /dev/null "${BASE}" || { echo "No preview at ${BASE} — start the frontend first." >&2; exit 1; }

mkdir -p "${OUT}"

shot() {
  local name="$1" url="$2"
  # A fixed wait rather than network-idle: the dev server holds an HMR websocket open, so "idle"
  # never arrives and the capture hangs forever.
  (cd "${CLI}" && pwsh playwright.ps1 screenshot \
      --viewport-size "1440,900" --wait-for-timeout 3500 \
      "${url}" "${OUT}/${name}.png" >/dev/null)
  echo "✓ ${name}.png"
}

shot 01-projects       "${BASE}/projects"
shot 02-operate        "${BASE}/projects/${PROJECT}?tab=operate"
shot 03-runs           "${BASE}/projects/${PROJECT}?tab=runs"
shot 04-automations    "${BASE}/projects/${PROJECT}?tab=automations"
shot 05-ask            "${BASE}/projects/${PROJECT}?tab=ask"
shot 06-settings       "${BASE}/projects/${PROJECT}?tab=settings"
shot 07-inbox          "${BASE}/inbox"

# Two surfaces are deliberately absent, and the manual says so rather than shipping a picture taken
# under different conditions:
#   * the code-source control (Settings → Edit Connector → advanced) needs a click, and this CLI can
#     only navigate;
#   * a Run's detail page has no stable URL in the mock, whose run ids are generated per page load.
# Adding a console project to click one disclosure was judged disproportionate to one screenshot.

echo
echo "Captured into ${OUT}. Check no real names crept in before committing."
