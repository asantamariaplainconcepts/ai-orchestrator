#!/usr/bin/env node
// SessionStart preflight: make sure the OTel Collector is listening before Claude Code
// emits telemetry, so nothing is silently dropped. FAST when it's already up; FAIL-SOFT
// (warn, never block the session) if it can't start. The Collector is the durable sink;
// viewers (Grafana, previously the Aspire dashboard) are fed by it and never authoritative.
//
// This script starts ONLY the Collector, not the Grafana dashboard stack (grafana-lgtm.compose.yaml)
// — that's heavier and opt-in, started on demand per .telemetry/README.md.
//
// Cross-platform (Windows/PowerShell, macOS, Linux): pure Node, no nc/bash needed. The port
// probe uses a native TCP socket; the Collector is started via `docker compose`.
import { spawnSync } from 'node:child_process';
import { connect } from 'node:net';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const OTLP_HOST = 'localhost';
const OTLP_PORT = 4317;
const scriptDir = dirname(fileURLToPath(import.meta.url));
const COMPOSE_FILE = join(scriptDir, 'otel-collector.compose.yaml');

const warn = (msg) => process.stderr.write(`${msg}\n`);

// Resolve true once the socket connects, false on any error/timeout — never rejects.
function isListening(host, port, timeoutMs = 1000) {
  return new Promise((resolvePromise) => {
    const socket = connect({ host, port });
    const done = (result) => {
      socket.destroy();
      resolvePromise(result);
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => done(true));
    socket.once('timeout', () => done(false));
    socket.once('error', () => done(false));
  });
}

// Already listening? Do nothing (fast path).
if (await isListening(OTLP_HOST, OTLP_PORT)) {
  process.exit(0);
}

// Not up — try to start it, but never block the session on failure.
// `shell: true` lets `docker` resolve via PATHEXT on Windows (docker.exe) and the PATH on Unix.
const result = spawnSync('docker', ['compose', '-f', COMPOSE_FILE, 'up', '-d'], {
  stdio: 'ignore',
  shell: true,
});

if (result.error && result.error.code === 'ENOENT') {
  warn('otel: WARNING — docker not found; skipping local telemetry capture for this session.');
} else if (result.status === 0) {
  warn('otel: started local Collector (persisting to .telemetry/).');
} else {
  warn('otel: WARNING — could not start the Collector; this session\'s telemetry may not be captured.');
}

// Always succeed: telemetry is best-effort, not a gate on doing work.
process.exit(0);
