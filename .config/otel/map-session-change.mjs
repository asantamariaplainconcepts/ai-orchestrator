#!/usr/bin/env node
// Session→change mapping: append one JSONL record linking the CURRENT session's id
// to the change its branch is working, so collect-usage can attribute usage.jsonl
// records (which only carry session.id) to a change by joining on it.
//
// Fired on SessionStart AND UserPromptSubmit: a mapping taken only at start is stale
// the moment the session checks out a different branch — the dominant worktree flow
// starts on a generated `claude/...` branch and switches to the `change/...` branch
// afterwards, which is how 24 sessions accumulated with change="" while the work
// they measured belonged to real changes. The dedup below keeps re-firing free:
// a record is appended only when (session, branch, change) is a new combination.
//
// This exists because resource-attribute tagging (OTEL_RESOURCE_ATTRIBUTES) cannot
// attribute the running session: the env is read once at startup, and the desktop
// client does not apply it to exported resources at all. The hook payload on stdin
// carries session_id and cwd, which is everything needed (DEC-022).
//
// WORKTREE-SAFE: the branch is resolved from the session's cwd (the worktree), while
// the mapping file lives in the main repo root, resolved via `git rev-parse
// --git-common-dir`. FAIL-SOFT: warn to stderr, always exit 0.
import { execFileSync } from 'node:child_process';
import { appendFileSync, existsSync, mkdirSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const warn = (msg) => process.stderr.write(`${msg}\n`);

// Hook input: JSON on stdin ({ session_id, cwd, hook_event_name, source, ... }).
let input = {};
try {
  input = JSON.parse(readFileSync(0, 'utf8'));
} catch {
  // Not invoked as a hook (or malformed payload) — nothing to map.
  process.exit(0);
}
const sessionId = input.session_id;
const cwd = input.cwd || process.cwd();
if (!sessionId) process.exit(0);

const git = (args, dir) =>
  execFileSync('git', ['-C', dir, ...args], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'ignore'],
  }).trim();

// The mapping file lives at the MAIN repo root (parent of the COMMON git dir) so all
// worktrees feed ONE sessions.jsonl, next to the ONE usage.jsonl it is joined against.
// The change dirs come from the session's own checkout (a worktree's branch can carry
// a change directory the main checkout doesn't have yet).
// Fallback to the script's location (.config/otel/ → two levels up) outside git.
let repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
let checkoutRoot = repoRoot;
let branch = '';
try {
  repoRoot = resolve(git(['rev-parse', '--path-format=absolute', '--git-common-dir'], cwd), '..');
  checkoutRoot = git(['rev-parse', '--show-toplevel'], cwd);
  branch = git(['rev-parse', '--abbrev-ref', 'HEAD'], cwd);
} catch {
  // Not a git repo / git missing — record the session unattributed.
}

// Match the branch against active change directories. Substring match (not just
// suffix) because generated branches carry both prefixes and suffixes around the
// change name; longest match wins.
let change = '';
try {
  for (const entry of readdirSync(join(checkoutRoot, 'openspec', 'changes'), { withFileTypes: true })) {
    if (!entry.isDirectory() || entry.name === 'archive') continue;
    if (branch.includes(entry.name) && entry.name.length > change.length) change = entry.name;
  }
} catch {
  // No openspec/changes directory — record the session unattributed.
}

// .telemetry/ is gitignored (DEC-022): the repo is public and this data carries user ids.
const mapDir = join(repoRoot, '.telemetry');
const mapFile = join(mapDir, 'sessions.jsonl');
const record = {
  ts: new Date().toISOString(),
  session_id: sessionId,
  source: input.source || '',
  cwd,
  branch,
  change,
  project: 'ai-orchestrator',
};

try {
  mkdirSync(mapDir, { recursive: true });
  // One record per (session, branch, change) is enough — resume/clear/compact
  // restarts of the same session shouldn't pile up duplicate lines.
  if (existsSync(mapFile)) {
    const dup = readFileSync(mapFile, 'utf8')
      .split('\n')
      .some((line) => {
        if (!line.includes(sessionId)) return false;
        try {
          const r = JSON.parse(line);
          return r.session_id === sessionId && r.branch === branch && r.change === change;
        } catch {
          return false;
        }
      });
    if (dup) process.exit(0);
  }
  appendFileSync(mapFile, `${JSON.stringify(record)}\n`);
  warn(`otel: session ${sessionId.slice(0, 8)}… mapped to ${change ? `change=${change}` : 'no active change'} (.telemetry/sessions.jsonl).`);
} catch {
  warn('otel: WARNING — could not write .telemetry/sessions.jsonl; this session will be unattributed.');
}

// Best-effort: telemetry mapping never gates doing work.
process.exit(0);
