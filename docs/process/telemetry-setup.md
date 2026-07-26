# Telemetry setup

Retros report human and agent time from OpenTelemetry data the Claude Code client exports to a
local collector. This document exists because that pipeline was **silently broken for four
consecutive changes**, and the way it broke is worth understanding before trusting it again.

## Check first, always

```bash
node .config/otel/verify-telemetry.mjs
```

It asserts artifacts, not configuration: variables present in the running process, a collector
listening, **our** collector rather than someone else's, bytes in `usage.jsonl`, and real session
records in `sessions.jsonl`. Exit 0 means telemetry is being captured. Anything else names the
failing check and what to do.

Run it at the start of a change, not at its retro. Telemetry that was never written cannot be
recovered afterwards.

## The owner action the repository cannot do for you

`.claude/settings.json` in this repo declares the exporter settings, and **that is not enough**.
Evidence from this machine: `CLAUDE_CODE_ENABLE_TELEMETRY` reached the process but **no `OTEL_*`
variable did**. The desktop client does not deliver project-level `env` to the client process the
way the terminal CLI does — the published documentation does not distinguish the two, so treat
project settings as insufficient rather than broken.

The consequence is nastier than "off": telemetry was **enabled with no endpoint**, so the client
happily exported to the OTLP default port — which on this machine belongs to a *different
project's* collector. Working-looking, and landing somewhere else entirely.

Put the variables where the app inherits them. In `~/.zshrc` (or your login shell's profile):

```bash
export CLAUDE_CODE_ENABLE_TELEMETRY=1
export OTEL_METRICS_EXPORTER=otlp
export OTEL_LOGS_EXPORTER=otlp
export OTEL_TRACES_EXPORTER=otlp
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4327
```

Then **restart the client** — the environment is read once, at process start — and re-run the
verifier. For a GUI app launched from Finder rather than a shell, a login shell profile may not
apply; `launchctl setenv OTEL_EXPORTER_OTLP_ENDPOINT http://localhost:4327` covers that session,
and a LaunchAgent makes it survive a reboot.

**Port 4327, not the OTLP default.** Another project's collector holds 4317 on this machine;
that collision is what the `fix-telemetry-collector-port` change was about, and pointing here
explicitly is what keeps our data ours.

## Why a missing measurement is a defect

`collect-usage` used to say: *if telemetry is missing, the entry says so (manual)*. That sentence
is why nobody noticed for four changes — every retro looked complete while the programme lost the
measurements it was built to collect.

A retro whose time source is `manual` **because capture is broken** must say so and name the
failing check. `manual` is legitimate only for work that genuinely predates the pipeline.

## What is still not proven

At the time of writing, the fix above is applied to `~/.claude/settings.json` but **has not been
observed working**: environment is read at process start, so the first session that can confirm
it is the next one. Until `verify-telemetry.mjs` exits 0 and `usage.jsonl` has bytes, this
document describes an intention, not a working pipeline (ADR-0005).
