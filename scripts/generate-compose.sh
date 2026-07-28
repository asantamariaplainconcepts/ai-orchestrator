#!/usr/bin/env bash
# Regenerates the committed self-host compose from the AppHost (ADR-0003: one owner).
# CI runs this and fails on drift; run it after any AppHost change.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${ROOT}/selfhost"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP}"' EXIT

(cd "${ROOT}/src/root/AiOrchestrator.AppHost" && aspire publish -o "${TMP}" > /dev/null)

mkdir -p "${OUT}"
cp "${TMP}/docker-compose.yaml" "${OUT}/docker-compose.yaml"
echo "✓ selfhost/docker-compose.yaml regenerated"
