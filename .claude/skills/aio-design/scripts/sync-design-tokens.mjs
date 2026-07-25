#!/usr/bin/env node
// Generates the derived design layers from the canonical one:
//
//   docs/design-system/tokens/*.css  →  DESIGN.md (L2)  and  shared/design/tokens.ts (L3)
//
// Dependency-free on purpose: it must run identically on a fresh clone, in CI, and inside an
// agent session, with nothing installed. Derivation is strictly one-way — this script never
// writes back to the canonical layer.
//
//   --write   regenerate the derived files
//   --check   report drift and exit non-zero; write nothing (used by CI)
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..');
const tokensDir = join(repoRoot, 'docs', 'design-system', 'tokens');
const designMd = join(repoRoot, 'DESIGN.md');
const tokensTs = join(repoRoot, 'src', 'frontend', 'shared', 'design', 'tokens.ts');

const mode = process.argv.includes('--write') ? 'write' : process.argv.includes('--check') ? 'check' : null;
if (!mode) {
  console.error('usage: sync-design-tokens.mjs --write | --check');
  process.exit(2);
}

const REGEN = 'node .claude/skills/aio-design/scripts/sync-design-tokens.mjs --write';

/**
 * Collect custom-property names per canonical file, in declaration order.
 * Only `:root` declarations are collected: theme overrides restate the same names, and the
 * derived layers describe the token *vocabulary*, not one theme's values.
 */
function readTokens() {
  const files = readdirSync(tokensDir).filter((f) => f.endsWith('.css')).sort();
  const groups = [];

  for (const file of files) {
    const css = readFileSync(join(tokensDir, file), 'utf8');
    // The first :root block only — later blocks are theme overrides of the same names.
    const rootStart = css.indexOf(':root');
    if (rootStart === -1) continue;
    const open = css.indexOf('{', rootStart);
    const close = css.indexOf('}', open);
    const body = css.slice(open + 1, close);

    // Split on declarations, not lines: a formatter may wrap a long value (a font stack) across
    // several lines, and a line-based parser would silently drop it — which it did once.
    const tokens = [];
    const withoutComments = body.replace(/\/\*[\s\S]*?\*\//g, '');
    for (const declaration of withoutComments.split(';')) {
      const match = declaration.match(/\s*(--[a-z0-9-]+)\s*:\s*([\s\S]+)/i);
      if (match) tokens.push({ name: match[1], value: match[2].trim().replace(/\s+/g, ' ') });
    }
    if (tokens.length) groups.push({ group: file.replace(/\.css$/, ''), tokens });
  }

  return groups;
}

function renderDesignMd(groups) {
  const lines = [];
  lines.push('---');
  lines.push('# GENERATED token block — derived from docs/design-system/tokens/*.css (canonical).');
  lines.push('# Do not edit by hand. Regenerate:');
  lines.push(`#   ${REGEN}`);
  for (const { group, tokens } of groups) {
    lines.push(`${group}:`);
    for (const { name, value } of tokens) {
      lines.push(`  ${name.replace(/^--/, '')}: "${value.replace(/"/g, "'")}"`);
    }
  }
  lines.push('---');
  lines.push('');
  lines.push('# DESIGN.md — the design contract');
  lines.push('');
  lines.push('<!-- The frontmatter above is generated. The prose below is written by hand and');
  lines.push('     deliberately contains no values: values live in the canonical layer. -->');
  lines.push('');
  lines.push('Read this before any UI work. The canonical system is');
  lines.push('[`docs/design-system/`](docs/design-system/README.md) — this file is derived from it and');
  lines.push('is guaranteed current by the CI drift gate.');
  lines.push('');
  lines.push('## Rules');
  lines.push('');
  lines.push('1. **Compose the kit, do not inline styles.** Use the classes in');
  lines.push('   `docs/design-system/ui-kit/`. If the kit lacks what a screen needs, add it there —');
  lines.push('   not in the screen.');
  lines.push('2. **Every value is a token.** No raw hex, no raw pixel value where a scale exists, no');
  lines.push('   font outside the approved stack. A literal cannot theme, which is why this is a');
  lines.push('   failing check rather than a preference.');
  lines.push('3. **Never branch on the theme.** Consume the variable; the theme swaps its value.');
  lines.push('   Both light and dark are first-class and both must be checked.');
  lines.push('4. **All user-facing copy comes from the typed i18n catalogue** and follows the content');
  lines.push('   fundamentals (voice, sentence-case labels, verb-first buttons, the four state');
  lines.push('   patterns) in the canonical README. Hardcoded JSX text fails lint.');
  lines.push('5. **Use the locked vocabulary exactly** — Agent, Connector, Automation, Run, Plan.');
  lines.push('6. **Every interactive element shows the shared focus treatment.** Do not remove an');
  lines.push('   outline without replacing it.');
  lines.push('');
  lines.push('## Before you push');
  lines.push('');
  lines.push('```bash');
  lines.push('bash .claude/skills/aio-design/scripts/validate-design-system.sh');
  lines.push('```');
  lines.push('');
  lines.push('It runs the same three stages CI runs: adherence, drift, and skill hygiene.');
  lines.push('');
  return lines.join('\n');
}

function renderTokensTs(groups) {
  const lines = [];
  lines.push('// GENERATED — derived from docs/design-system/tokens/*.css (canonical).');
  lines.push('// Do not edit by hand. Regenerate:');
  lines.push(`//   ${REGEN}`);
  lines.push('//');
  lines.push('// These are token NAMES bound to their CSS variable references, never copied values:');
  lines.push('// changing a value in the canonical layer cannot leave this file stale in substance.');
  lines.push('');
  for (const { group, tokens } of groups) {
    const constName = group.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
    lines.push(`export const ${constName} = {`);
    for (const { name } of tokens) {
      const key = name.replace(/^--/, '').replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());
      lines.push(`  ${key}: 'var(${name})',`);
    }
    lines.push('} as const;');
    lines.push('');
  }
  const all = groups.map(({ group }) => group.replace(/-([a-z])/g, (_, c) => c.toUpperCase()));
  lines.push(`export const tokens = { ${all.join(', ')} } as const;`);
  lines.push('');
  return lines.join('\n');
}

const groups = readTokens();
if (!groups.length) {
  console.error(`no tokens found in ${tokensDir}`);
  process.exit(1);
}

const targets = [
  { path: designMd, label: 'DESIGN.md', content: renderDesignMd(groups) },
  { path: tokensTs, label: 'src/frontend/shared/design/tokens.ts', content: renderTokensTs(groups) },
];

if (mode === 'write') {
  for (const { path, label, content } of targets) {
    writeFileSync(path, content);
    console.log(`wrote ${label}`);
  }
  process.exit(0);
}

let drifted = false;
for (const { path, label, content } of targets) {
  const current = existsSync(path) ? readFileSync(path, 'utf8') : null;
  if (current !== content) {
    console.error(`DRIFT: ${label} does not match the canonical tokens.`);
    drifted = true;
  }
}

if (drifted) {
  console.error(`\nRegenerate with:\n  ${REGEN}`);
  process.exit(1);
}

console.log('design tokens: derived layers match canonical');
