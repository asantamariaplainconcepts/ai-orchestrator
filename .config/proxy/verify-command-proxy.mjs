#!/usr/bin/env node
// Does a command whose bytes gate a decision survive the filtering proxy? Asserts the OUTCOME by
// running commands and comparing bytes — never the configuration (#341, ADR-0004).
//
// Why this exists. `rtk` rewrites this repository's commands through a global Claude Code hook.
// When it filters output it sometimes drops content while still exiting 0, and the truncated result
// stays syntactically valid, so nothing signals the loss. Measured, four times:
//
//   * `cat .claude/workflow.json` returned the file WITHOUT `holdLabel` — the one key the hold gate
//     depends on. The plausible next step was hardcoding `hitl` or skipping the hold, either of
//     which breaks the gate that exists to stop work proceeding without a person.
//   * `git diff --name-only origin/main...HEAD > file` wrote formatted prose including a
//     `--- Changes ---` header, so a branch-overlap check computed 0 files where `rtk proxy` gave
//     the real 23.
//   * `rtk pnpm build` reported a failed build as succeeded.
//   * Prettier read clean over a non-zero exit.
//
// Three retro entries and an ADR did not stop the recurrence, which is the evidence that more prose
// is not the missing part. So this is executable: it reproduces the two failures that have concrete
// artifacts and fails if either comes back.
//
// A green config would prove nothing here — the proxy's passthrough list is a token-wise prefix
// match with no regex, so a pattern can look right and match nothing at all. Only the bytes decide.
//
// Exit 0 when the decision-gating commands are faithful, 1 when they are not. Never throws.
import { execFileSync, execSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

const results = [];
const check = (name, ok, detail) => results.push({ name, ok, detail });

/** Run a command exactly as written, with nothing in the way. */
const shell = (command) => {
  try {
    return {
      ok: true,
      out: execSync(command, {
        cwd: repoRoot,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
        timeout: 30_000,
      }),
    };
  } catch (error) {
    return { ok: false, out: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
};

/**
 * What an agent would ACTUALLY run for `command`, and its output.
 *
 * The rewrite must be applied here explicitly. The proxy reaches an agent through a Claude Code
 * `PreToolUse` hook, which rewrites the command *before* the Bash tool runs it — a child process
 * spawned from this script inherits none of that. An earlier draft of this file simply ran the
 * command and compared it with itself: three green checks that exercised nothing. So the rewrite is
 * resolved the way the hook resolves it, through `rtk rewrite`, and the rewritten form is what gets
 * executed and compared.
 *
 * `rtk rewrite` exit codes: 0 and 3 carry a rewritten command on stdout, 1 and 2 mean it passes
 * through untouched — which is faithful by construction and needs no comparison.
 */
const asAgentRuns = (command) => {
  // spawnSync, not execFileSync: a rewrite that needs confirmation exits **3**, and execFileSync
  // throws on any non-zero status. An earlier draft caught that throw and reported "no rewrite",
  // so the guard could be removed entirely and every check still passed. The status has to be read,
  // not inferred from whether a call threw.
  const probe = spawnSync('rtk', ['rewrite', command], {
    cwd: repoRoot,
    encoding: 'utf8',
    timeout: 15_000,
  });

  const rewriteFound = probe.status === 0 || probe.status === 3;
  const rewritten = (probe.stdout ?? '').trim();

  if (!rewriteFound || !rewritten || rewritten === command) {
    return { filtered: false, command, ...shell(command) };
  }

  return { filtered: true, command: rewritten, ...shell(rewritten) };
};

const hasProxy = shell('command -v rtk').ok;

// 0. No proxy on this machine is a PASS, not a failure: nothing can filter what nothing rewrites.
//    Said explicitly so a green run on a clean machine is not mistaken for a verified guard.
if (!hasProxy) {
  check('no filtering proxy installed', true, 'rtk absent — nothing rewrites commands here');
} else {
  // 1. The hook the proxy installs must be the one it shipped. A modified hook makes the proxy
  //    refuse to execute at all ("hook integrity check FAILED"), which silently disables every
  //    rewrite — the opposite failure, and just as invisible. Measured 2026-08-13.
  const verify = shell('rtk verify');
  check(
    'proxy hook is unmodified',
    verify.ok,
    verify.ok ? 'hook integrity verified' : 'hook integrity FAILED — the proxy will not execute'
  );

  // 2. The recorded failure, reproduced. `holdLabel` is the key `set-issue-status` depends on, so
  //    its loss is the difference between honouring the hold and skipping it.
  const workflowPath = join(repoRoot, '.claude', 'workflow.json');
  if (!existsSync(workflowPath)) {
    check('config read keeps its keys', false, '.claude/workflow.json not found');
  } else {
    const truth = JSON.parse(readFileSync(workflowPath, 'utf8'));
    const read = asAgentRuns('cat .claude/workflow.json');
    const survived = read.out.includes('holdLabel') && read.out.includes(truth.holdLabel);
    check(
      'config read keeps its keys',
      survived,
      survived
        ? `holdLabel survived (${truth.holdLabel})` +
          (read.filtered ? ` via '${read.command}'` : ', read unfiltered')
        : `holdLabel was DROPPED by '${read.command}' — the hold gate reads this file`
    );
  }

  // 3. The other recorded failure. Porcelain exists to be parsed, so a formatted answer is a wrong
  //    answer: the count is compared against git's own, asked without the shell in the way.
  let realCount = null;
  try {
    realCount = execFileSync('git', ['diff', '--name-only', 'origin/main...HEAD'], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    })
      .split('\n')
      .filter(Boolean).length;
  } catch {
    // No origin/main to compare against (a shallow clone, a fresh fork). Skipped, not failed.
  }

  if (realCount === null) {
    check('porcelain output is not reformatted', true, 'origin/main unavailable — comparison skipped');
  } else {
    const read = asAgentRuns('git diff --name-only origin/main...HEAD');
    const seenCount = read.out.split('\n').filter(Boolean).length;
    const decorated = /^---|Changes ---|^\s*\d+ files? changed/m.test(read.out);
    const faithful = seenCount === realCount && !decorated;
    check(
      'porcelain output is not reformatted',
      faithful,
      faithful
        ? `${realCount} path(s)` + (read.filtered ? ` via '${read.command}'` : ', read unfiltered')
        : `git reported ${realCount} path(s), '${read.command}' reported ${seenCount}` +
          (decorated ? ' and added prose decoration' : '')
    );
  }
}

const width = Math.max(...results.map((r) => r.name.length));
const failed = results.filter((r) => !r.ok);

for (const { name, ok, detail } of results) {
  const label = ok ? '\x1b[32mok  \x1b[0m' : '\x1b[31mFAIL\x1b[0m';
  console.log(`${label} ${name.padEnd(width)}  ${detail}`);
}

console.log('');
if (failed.length === 0) {
  console.log('commands that gate a decision are faithful.');
} else {
  console.log(
    `${failed.length} check(s) failed — a gate in this repository may be decided on bytes that ` +
      'are not what the command produced. Re-run the command under `rtk proxy` to see the truth, ' +
      'and add its prefix to [hooks] exclude_commands (see AGENTS.md).'
  );
}

process.exit(failed.length === 0 ? 0 : 1);
