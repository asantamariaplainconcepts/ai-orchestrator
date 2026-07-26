#!/usr/bin/env node
// Is telemetry actually being captured? Asserts the artifacts, never the configuration.
//
// This exists because the configuration was right and nothing worked, for four consecutive
// changes. `.claude/settings.json` held valid env and hook blocks; the desktop client applied
// neither, so no OTLP export happened and no session was ever mapped. Every retro in that window
// recorded time as "manual" and the process treated that as a footnote rather than a defect.
//
// So this checks outcomes: variables present in *this* process, a collector actually listening,
// bytes on disk, and this session mapped. A green config proves nothing (ADR-0004).
//
// Exit 0 when telemetry is being captured, 1 when it is not. Never throws.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { createConnection } from 'node:net';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const telemetryDir = join(repoRoot, '.telemetry');
const usageFile = join(telemetryDir, 'usage.jsonl');
const sessionsFile = join(telemetryDir, 'sessions.jsonl');

const results = [];
const check = (name, ok, detail) => results.push({ name, ok, detail });

// 1. The export switch and endpoint, in this process. If these are absent the client is not
//    exporting at all and everything downstream is moot.
// Both, not either. Enabled-without-endpoint is the worst state of the three: the client
// exports happily to the OTLP default port, which on this machine belongs to a different
// project's collector — so telemetry looks on and lands somewhere else entirely. That is
// precisely how four changes' measurements disappeared without a single error.
const enabled = process.env.CLAUDE_CODE_ENABLE_TELEMETRY === '1';
const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? '';
check(
  'exporter enabled AND pointed here',
  enabled && endpoint !== '',
  enabled && endpoint
    ? `endpoint ${endpoint}`
    : !enabled
      ? 'CLAUDE_CODE_ENABLE_TELEMETRY is not set'
      : 'enabled but OTEL_EXPORTER_OTLP_ENDPOINT is UNSET — exports are going to the OTLP ' +
        'default port, not ours. Set it in the shell profile the app inherits; project ' +
        '.claude/settings.json does not deliver OTEL_* to every client.'
);

// 2. Something is listening where the exporter points. A configured endpoint with nothing behind
//    it fails silently — the client does not surface export errors.
const reachable = await new Promise((done) => {
  const match = /^https?:\/\/([^:/]+):(\d+)/.exec(endpoint || 'http://localhost:4327');
  const socket = createConnection({ host: match?.[1] ?? 'localhost', port: Number(match?.[2] ?? 4327) });
  const settle = (value) => {
    socket.destroy();
    done(value);
  };
  socket.setTimeout(1500);
  socket.once('connect', () => settle(true));
  socket.once('timeout', () => settle(false));
  socket.once('error', () => settle(false));
});
check(
  'collector accepting connections',
  reachable,
  reachable ? 'OTLP endpoint answered' : 'nothing listening — run .config/otel/ensure-collector.mjs'
);

// 3. Is our collector the one listening? A port that answers is not proof it is ours: an earlier
//    change lost a day to another project's collector holding the port.
let ours = false;
try {
  const names = execFileSync('docker', ['ps', '--format', '{{.Names}}'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'ignore'],
  });
  ours = names.split('\n').some((n) => n.trim() === 'ai-orchestrator-otel-collector');
} catch {
  // Docker absent or not running — reported as a failed check, not a crash.
}
check('our collector is the one running', ours, ours ? 'ai-orchestrator-otel-collector up' : 'container not found');

// 4. Bytes on disk. The durable file sink is the system of record; dashboards are disposable.
const hasUsage = existsSync(usageFile) && statSync(usageFile).size > 0;
check(
  'usage.jsonl has data',
  hasUsage,
  hasUsage
    ? `${(statSync(usageFile).size / 1024).toFixed(1)} KiB, last written ${statSync(usageFile).mtime.toISOString()}`
    : 'no bytes — nothing has ever been exported and captured'
);

// 5. This session mapped, so collect-usage can attribute anything that did arrive.
const sessionId = process.env.CLAUDE_SESSION_ID ?? '';
let mapped = false;
let sessionCount = 0;
try {
  const lines = readFileSync(sessionsFile, 'utf8').split('\n').filter(Boolean);
  sessionCount = lines.length;
  // Probe rows are not evidence: a hand-run probe proves the script works, never that the
  // hook fires. Only records the client actually produced count.
  const real = lines.filter((l) => !l.includes('probe'));
  sessionCount = real.length;
  mapped = sessionId ? real.some((l) => l.includes(sessionId)) : real.length > 0;
} catch {
  // No file yet.
}
check(
  'sessions are being mapped',
  mapped,
  mapped
    ? `${sessionCount} real session record(s)`
    : 'no non-probe records — the SessionStart hook has never fired in a real session'
);

const width = Math.max(...results.map((r) => r.name.length));
for (const { name, ok, detail } of results) {
  process.stdout.write(`${ok ? '[32mok  [0m' : '[31mFAIL[0m'} ${name.padEnd(width)}  ${detail}\n`);
}

const failed = results.filter((r) => !r.ok);
if (failed.length === 0) {
  process.stdout.write('\ntelemetry is being captured.\n');
  process.exit(0);
}

process.stdout.write(
  `\n${failed.length} check(s) failed — retros for work done now will have no measured time, ` +
    'and nothing recovers telemetry that was never written.\n'
);
process.exit(1);
