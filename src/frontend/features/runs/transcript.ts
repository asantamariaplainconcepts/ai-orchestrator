/**
 * Turns a Run's raw log into something a person can read (#130, design D2).
 *
 * **Dialect-tolerant, not dialect-specific.** Every line is "a JSON object if it parses, text if it
 * doesn't". Well-known fields are lifted when they happen to be present; whatever is left is kept for
 * a collapsible block. Nothing here asks *which runtime* wrote a line, so adding a third runtime needs
 * no change in this file — it only makes more of its fields collapse.
 *
 * The known cost, stated rather than discovered later: lifting is a heuristic. A runtime naming its
 * text field something this module does not look at degrades to a pretty-printed object rather than
 * prose. That is acceptable only because `kind: "raw"` keeps every line complete and visible — the
 * transcript is complete first and pretty second.
 */

export type TranscriptEntry =
  | { kind: "text"; body: string; usage: LineUsage | null }
  | {
      kind: "tool";
      tool: string;
      subject: string | null;
      detail: string;
      usage: LineUsage | null;
      /** The writer cut this line at the column's width, so `detail` is a fragment. */
      truncated?: boolean;
    }
  | { kind: "event"; label: string; detail: string; usage: LineUsage | null; truncated?: boolean }
  | { kind: "raw"; body: string; usage: null; truncated?: boolean };

export interface LineUsage {
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly costUsd: number | null;
}

/** What the counter shows. `null` totals mean unknown — never zero (BR-011, design D4). */
export interface TranscriptTotals {
  readonly inputTokens: number | null;
  readonly outputTokens: number | null;
  readonly costUsd: number | null;
}

export interface Transcript {
  readonly entries: readonly TranscriptEntry[];
  readonly totals: TranscriptTotals;
}

/** Identifiers a reader has no use for; lifted out of any object before it is pretty-printed. */
const NOISE = new Set([
  "sessionID",
  "session_id",
  "messageID",
  "message_id",
  "id",
  "timestamp",
  "uuid",
  "parent_tool_use_id",
]);

/** Field names observed to carry the agent's own words, in the order they are preferred. */
const TEXT_FIELDS = ["text", "content", "result", "message"];

/**
 * Field names observed to carry a tool's subject — the thing it acted on. Ordered most specific
 * first: `name` is last but one because a tool whose only argument is a name (a skill invocation) is
 * identified by it, while a tool that also names a file is better described by the file.
 */
const SUBJECT_FIELDS = [
  "file_path",
  "path",
  "filePath",
  "command",
  "pattern",
  "url",
  "name",
  "description",
];

export function parseTranscript(log: string): Transcript {
  const entries: TranscriptEntry[] = [];

  for (const line of log.split("\n")) {
    const trimmed = line.trim();
    if (trimmed.length === 0) continue;
    const entry = interpret(trimmed);
    if (entry !== null) entries.push(entry);
  }

  return { entries, totals: total(entries) };
}

/**
 * A line that says nothing and carries nothing — a text part whose body is only whitespace, which
 * both observed runtimes emit between steps. It used to render as a collapsible `text` row a reader
 * could open to find two newlines. Dropped only when it also carries no usage, so nothing that
 * contributes to the spend can ever vanish for being quiet (BR-011).
 */
function isSilent(parsed: Record<string, unknown>, usage: LineUsage | null): boolean {
  if (usage !== null) return false;
  const body = liftText(parsed);
  return body !== null && body.trim().length === 0;
}

function interpret(line: string): TranscriptEntry | null {
  const parsed = asObject(line);
  if (!parsed) {
    // A line that opened like JSON and did not parse is almost always one the writer cut at the
    // column's width — an agent event carrying a whole file in its result. Its *metadata* sits in
    // the first few hundred bytes and survives the cut, so the shape of what happened is still
    // recoverable even though the payload is not.
    const salvaged = salvage(line);
    if (salvaged) return salvaged;

    // Not JSON at all: a plain stderr line, a future dialect. Verbatim, always (design D5) —
    // these are the lines a reader is most often hunting for.
    return { kind: "raw", body: line, usage: null };
  }

  const usage = liftUsage(parsed);
  const tool = liftTool(parsed);
  if (tool) {
    return { ...tool, usage };
  }

  if (isSilent(parsed, usage)) return null;

  const body = liftText(parsed);
  if (body !== null && body.trim().length > 0) {
    return { kind: "text", body, usage };
  }

  return {
    kind: "event",
    label: label(parsed),
    detail: prettify(parsed),
    usage,
  };
}

/** How much of a cut line is read for its metadata — generous for nesting, far short of the cut. */
const SALVAGE_WINDOW = 2000;

/**
 * What a truncated JSON line still says about itself (#130's readability gap).
 *
 * A line the writer cut mid-string can never parse, and the honest fallback — print it verbatim —
 * puts 8 KB of JSON on one line, which is the least readable thing this screen renders. But the
 * fields a reader actually wants (which tool ran, on what) are written *before* the payload that
 * caused the overflow, so they are intact. This reads them off the surviving prefix with a scan
 * narrow enough that it cannot invent an entry from arbitrary prose: the line must have opened as
 * a JSON object, and only quoted scalars are matched.
 *
 * Returns null when nothing recognisable is there, which keeps the verbatim branch as the floor.
 */
function salvage(line: string): TranscriptEntry | null {
  if (!line.startsWith("{")) return null;

  const head = line.slice(0, SALVAGE_WINDOW);

  // `name` only where the line also declares itself a tool use — unqualified it is the most common
  // field name in any dialect and would label half the transcript as tools (the same restraint
  // `liftTool` applies to a parsed object).
  const tool =
    quoted(head, ["tool", "tool_name", "toolName"]) ??
    (/"type":"tool[_-]?(use)?"/.test(head) ? quoted(head, ["name"]) : null);

  if (tool !== null) {
    return {
      kind: "tool",
      tool,
      subject: quoted(head, SUBJECT_FIELDS),
      detail: line,
      usage: null,
      truncated: true,
    };
  }

  const type = quoted(head, ["type", "subtype", "event", "kind"]);
  if (type !== null) {
    return { kind: "event", label: type, detail: line, usage: null, truncated: true };
  }

  // It opened as JSON and said nothing this module recognises. Verbatim, but still flagged: the
  // reader deserves to know the line is a fragment rather than the whole of what the agent wrote.
  return { kind: "raw", body: line, usage: null, truncated: true };
}

/**
 * The first of `keys` present as a quoted string value, read without parsing. Deliberately refuses
 * escapes: a value containing `\"` stops at the escape rather than guessing where it really ended,
 * which loses a subject occasionally and never fabricates one.
 */
function quoted(head: string, keys: readonly string[]): string | null {
  for (const key of keys) {
    const marker = `"${key}":"`;
    const at = head.indexOf(marker);
    if (at === -1) continue;

    const from = at + marker.length;
    const to = head.indexOf('"', from);
    if (to === -1) continue;

    const value = head.slice(from, to);
    if (value.length > 0 && !value.endsWith("\\")) return value;
  }
  return null;
}

function asObject(line: string): Record<string, unknown> | null {
  if (!line.startsWith("{")) return null;
  try {
    const value: unknown = JSON.parse(line);
    return value !== null && typeof value === "object" && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

/** A short name for an object with no readable prose: its `type`, or the shape it turned out to be. */
function label(object: Record<string, unknown>): string {
  for (const key of ["type", "subtype", "event", "kind"]) {
    const value = object[key];
    if (typeof value === "string" && value.length > 0) return value;
  }
  return "event";
}

/**
 * The agent's words, wherever they turn out to live. Looks one level into a nested container — both
 * observed dialects wrap their text in `part` or `message` — and no further: a deeper search would
 * start lifting text out of things that are not the reply.
 */
function liftText(object: Record<string, unknown>): string | null {
  const direct = firstString(object, TEXT_FIELDS);
  if (direct !== null) return direct;

  for (const container of ["part", "message", "delta"]) {
    const nested = object[container];
    if (nested !== null && typeof nested === "object" && !Array.isArray(nested)) {
      const inner = firstString(nested as Record<string, unknown>, TEXT_FIELDS);
      if (inner !== null) return inner;
    }
    // Claude's assistant events carry an array of content blocks.
    if (Array.isArray(nested)) {
      const joined = joinBlocks(nested);
      if (joined !== null) return joined;
    }
    if (nested !== null && typeof nested === "object") {
      const blocks = (nested as Record<string, unknown>).content;
      if (Array.isArray(blocks)) {
        const joined = joinBlocks(blocks);
        if (joined !== null) return joined;
      }
    }
  }

  return null;
}

function joinBlocks(blocks: readonly unknown[]): string | null {
  const texts = blocks
    .filter(
      (block): block is Record<string, unknown> => typeof block === "object" && block !== null,
    )
    .map((block) => firstString(block, TEXT_FIELDS))
    .filter((text): text is string => text !== null);

  return texts.length > 0 ? texts.join("\n\n") : null;
}

function firstString(object: Record<string, unknown>, keys: readonly string[]): string | null {
  for (const key of keys) {
    const value = object[key];
    if (typeof value === "string" && value.length > 0) return value;
  }
  return null;
}

/**
 * A tool invocation, as one line naming the tool and what it acted on (design D2). The full object
 * stays in `detail`, one disclosure away — a reader wants the shape of what happened, and the argument
 * object almost never is it.
 */
function liftTool(
  object: Record<string, unknown>,
): { kind: "tool"; tool: string; subject: string | null; detail: string } | null {
  const containers = [object, object.part, object.message].filter(
    (candidate): candidate is Record<string, unknown> =>
      candidate !== null && typeof candidate === "object" && !Array.isArray(candidate),
  );

  for (const container of containers) {
    const name =
      firstString(container, ["tool", "tool_name", "toolName"]) ??
      (isToolUse(container) ? firstString(container, ["name"]) : null);
    if (name === null) continue;

    // `state.input` is opencode's own nesting, and omitting it cost every one of its tool rows its
    // subject: a transcript of bare `skill` and `bash` chips that never said which skill or which
    // command. Measured on a real Run 2026-08-10 — three `skill` rows and a `bash` row, all with an
    // empty subject, while the information sat one level below where this looked.
    const state = container.state;
    const nested =
      state !== null && typeof state === "object" && !Array.isArray(state)
        ? (state as Record<string, unknown>)
        : null;

    const input =
      container.input ??
      container.args ??
      container.parameters ??
      nested?.input ??
      nested?.args ??
      nested?.parameters;
    const subject =
      input !== null && typeof input === "object" && !Array.isArray(input)
        ? firstString(input as Record<string, unknown>, SUBJECT_FIELDS)
        : typeof input === "string"
          ? input
          : null;

    return { kind: "tool", tool: name, subject, detail: prettify(object) };
  }

  // Claude nests tool_use blocks inside a content array.
  const blocks = nestedBlocks(object);
  for (const block of blocks) {
    if (isToolUse(block)) {
      const name = firstString(block, ["name"]);
      if (name === null) continue;
      const input = block.input;
      const subject =
        input !== null && typeof input === "object" && !Array.isArray(input)
          ? firstString(input as Record<string, unknown>, SUBJECT_FIELDS)
          : null;
      return { kind: "tool", tool: name, subject, detail: prettify(object) };
    }
  }

  return null;
}

function isToolUse(object: Record<string, unknown>): boolean {
  const type = object.type;
  return (
    typeof type === "string" && (type === "tool_use" || type === "tool-use" || type === "tool")
  );
}

function nestedBlocks(object: Record<string, unknown>): Record<string, unknown>[] {
  const message = object.message;
  const content =
    message !== null && typeof message === "object" && !Array.isArray(message)
      ? (message as Record<string, unknown>).content
      : object.content;

  return Array.isArray(content)
    ? content.filter(
        (block): block is Record<string, unknown> => typeof block === "object" && block !== null,
      )
    : [];
}

/**
 * Token counts and cost, wherever they turn out to live. Absent stays absent — this returns null
 * rather than zeroes, because zero is a claim and unknown is a fact (BR-011).
 */
function liftUsage(object: Record<string, unknown>): LineUsage | null {
  for (const container of [object, object.part, object.message, object.usage]) {
    if (container === null || typeof container !== "object" || Array.isArray(container)) continue;
    const found = readUsage(container as Record<string, unknown>);
    if (found) return found;
  }
  return null;
}

function readUsage(container: Record<string, unknown>): LineUsage | null {
  const block = container.usage ?? container.tokens;
  const source =
    block !== null && typeof block === "object" && !Array.isArray(block)
      ? (block as Record<string, unknown>)
      : container;

  const input = firstNumber(source, ["input_tokens", "input", "inputTokens", "prompt_tokens"]);
  const output = firstNumber(source, [
    "output_tokens",
    "output",
    "outputTokens",
    "completion_tokens",
  ]);
  if (input === null && output === null) return null;

  const cost =
    firstNumber(container, ["total_cost_usd", "cost", "costUsd"]) ??
    firstNumber(source, ["total_cost_usd", "cost", "costUsd"]);

  return { inputTokens: input ?? 0, outputTokens: output ?? 0, costUsd: cost };
}

function firstNumber(object: Record<string, unknown>, keys: readonly string[]): number | null {
  for (const key of keys) {
    const value = object[key];
    if (typeof value === "number" && Number.isFinite(value)) return value;
  }
  return null;
}

/**
 * Sums what the lines carry. A transcript whose lines carry no usage at all totals to unknown, never
 * to zero — the one lie this screen has not told yet (design D4).
 */
function total(entries: readonly TranscriptEntry[]): TranscriptTotals {
  const carrying = entries.map((entry) => entry.usage).filter((usage) => usage !== null);

  if (carrying.length === 0) {
    return { inputTokens: null, outputTokens: null, costUsd: null };
  }

  const costs = carrying.map((usage) => usage.costUsd).filter((cost) => cost !== null);

  return {
    inputTokens: carrying.reduce((sum, usage) => sum + usage.inputTokens, 0),
    outputTokens: carrying.reduce((sum, usage) => sum + usage.outputTokens, 0),
    // A transcript reporting tokens but no cost says unknown for the cost alone.
    costUsd: costs.length === 0 ? null : costs.reduce((sum, cost) => sum + cost, 0),
  };
}

/** The object with reader-useless identifiers removed, indented for the disclosure block. */
function prettify(object: Record<string, unknown>): string {
  return JSON.stringify(strip(object), null, 2);
}

function strip(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(strip);
  if (value === null || typeof value !== "object") return value;

  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>)
      .filter(([key]) => !NOISE.has(key))
      .map(([key, nested]) => [key, strip(nested)]),
  );
}
