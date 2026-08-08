#!/usr/bin/env bash
# Everything that must be true before this spike can start (ADR-0017: name the access up front).
# Checks only — nothing here creates, installs, or spends. Run it the day the subscription exists.
set -uo pipefail

ok=0; bad=0
pass() { printf '%-30s ok        %s\n' "$1" "${2:-}"; ok=$((ok+1)); }
fail() { printf '%-30s MISSING   %s\n' "$1" "$2"; bad=$((bad+1)); }
manual() { printf '%-30s MANUAL    %s\n' "$1" "$2"; }

command -v az >/dev/null \
  && pass "azure cli" "$(az version --query '"azure-cli"' -o tsv 2>/dev/null)" \
  || fail "azure cli" "install the Azure CLI"

if az account show -o none 2>/dev/null; then
  pass "az signed in" "$(az account show --query name -o tsv 2>/dev/null)"
else
  fail "az signed in" "az login"
fi

# The aca CLI is its OWN surface, not 'az containerapp'. The install URL was verified on
# 2026-08-08 to redirect to microsoft/azure-container-apps aca-cli/preview/install.sh (HTTP 200).
command -v aca >/dev/null \
  && pass "aca cli" "$(aca --version 2>/dev/null | head -1)" \
  || fail "aca cli" "curl -fsSL https://aka.ms/aca-cli-install | sh   then: aca auth login"

if command -v aca >/dev/null; then
  aca doctor >/dev/null 2>&1 && pass "aca doctor" || fail "aca doctor" "run 'aca doctor' and read it"
fi

if az account show -o none 2>/dev/null; then
  state=$(az provider show -n Microsoft.App --query registrationState -o tsv 2>/dev/null)
  [ "$state" = "Registered" ] \
    && pass "Microsoft.App provider" "$state" \
    || fail "Microsoft.App provider" "az provider register -n Microsoft.App  (now: ${state:-unknown})"
fi

# Human-only, and listing them is the point (ADR-0017): an agent cannot mint either.
manual "github PAT (fine-grained)" "the copilot credential rejects classic ghp_ tokens"
manual "anthropic key" "for the anthropic-claude credential type"

printf '\n%d ok, %d missing\n' "$ok" "$bad"
[ "$bad" -eq 0 ] || { echo "Preflight incomplete — nothing else in this directory will work yet."; exit 1; }
