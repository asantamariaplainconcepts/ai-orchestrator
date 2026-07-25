#!/usr/bin/env node
// SessionStart preflight: make sure THIS PROJECT's OTel Collector is running before the agent
// emits telemetry, so nothing is silently dropped or delivered to somebody else's sink.
//
// The check is by CONTAINER NAME, not by port occupancy. A port being occupied is not evidence
// that our collector is running — it is evidence that *something* is listening. That distinction
// cost four changes' worth of telemetry: another project's collector held the default OTLP port,
// this preflight saw a listener and concluded "already up", and every session exported into that
// project's sink while ours was never created at all.
//
// FAIL-SOFT: warn to stderr, always exit 0. Telemetry never gates doing work.
// Cross-platform (Windows/PowerShell, macOS, Linux): pure Node plus `docker`, no nc/bash.
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { connect } from 'node:net';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const CONTAINER = 'ai-orchestrator-otel-collector';
const scriptDir = dirname(fileURLToPath(import.meta.url));
const COMPOSE_FILE = join(scriptDir, 'otel-collector.compose.yaml');

const warn = (msg) => process.stderr.write(`${msg}\n`);

/** The host port this project publishes, read from the compose file so there is one source. */
function publishedPort() {
  try {
    const match = readFileSync(COMPOSE_FILE, 'utf8').match(/"(\d+):4317"/);
    if (match) return Number(match[1]);
  } catch {
    // Fall through to the documented default.
  }
  return 4327;
}

function isListening(host, port, timeoutMs = 1000) {
  return new Promise((resolve) => {
    const socket = connect({ host, port });
    const done = (result) => {
      socket.destroy();
      resolve(result);
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => done(true));
    socket.once('timeout', () => done(false));
    socket.once('error', () => done(false));
  });
}

const port = publishedPort();

// The endpoint the agent exports to must match the port we publish, or telemetry goes nowhere
// useful. Two files hold this value; this is where a disagreement becomes visible instead of
// silently costing a change's worth of data.
const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;
if (endpoint && !endpoint.includes(`:${port}`)) {
  warn(
    `otel: WARNING — OTEL_EXPORTER_OTLP_ENDPOINT is ${endpoint} but the Collector publishes ` +
      `port ${port}. Update .claude/settings.json or the compose file so they agree.`
  );
}

// Authoritative check: is OUR container running? Never infer it from an occupied port.
const running = spawnSync('docker', ['ps', '--filter', `name=^${CONTAINER}$`, '--format', '{{.Names}}'], {
  encoding: 'utf8',
  shell: true,
});

if (running.error?.code === 'ENOENT') {
  warn('otel: WARNING — docker not found; skipping local telemetry capture for this session.');
  process.exit(0);
}

if (running.stdout?.trim() === CONTAINER) {
  process.exit(0);
}

// Not running. If the port is already taken, it is taken by something that is not ours — say so
// plainly rather than starting a container that cannot bind.
if (await isListening('localhost', port)) {
  warn(
    `otel: WARNING — port ${port} is held by a process that is not ${CONTAINER}. This session's ` +
      "telemetry is NOT being captured (it would land in another project's collector). Free the " +
      'port, or change the published port in .config/otel/otel-collector.compose.yaml and the ' +
      'endpoint in .claude/settings.json.'
  );
  process.exit(0);
}

const started = spawnSync('docker', ['compose', '-f', COMPOSE_FILE, 'up', '-d'], {
  stdio: 'ignore',
  shell: true,
});

if (started.status === 0) {
  warn(`otel: started ${CONTAINER} on port ${port} (persisting to .telemetry/).`);
} else {
  warn("otel: WARNING — could not start the Collector; this session's telemetry may not be captured.");
}

process.exit(0);
