import { Check, ChevronRight, TriangleAlert } from "lucide-react";
import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import type { Transcript, TranscriptEntry, TranscriptStep, TranscriptTotals } from "./transcript";

/**
 * The Output section as a transcript (#130, turn 10). The agent's words are prose, tools are one line
 * each naming what they touched, and anything this product cannot interpret is shown verbatim —
 * completeness first, presentation second (design D5).
 *
 * `step_start`/`step_finish` are not rows: they delimit a **step**, which becomes a collapsed block
 * carrying its own tool count and duration. Collapsed is the default because a Run's shape is the
 * thing a reader wants first; the last step and any that failed open themselves, because those are
 * the ones somebody came to read.
 *
 * A log the runtime marked no steps in renders flat, exactly as it did before this existed.
 */
export function RunTranscript({
  transcript,
  verbatim = false,
}: {
  transcript: Transcript;
  /** Show every line flat, in order, grouping nothing — the Raw view (design D5). */
  verbatim?: boolean;
}) {
  const { preamble, steps, entries } = transcript;

  if (verbatim || steps.length === 0) {
    return <Lines entries={entries} />;
  }

  return (
    <div className="flex flex-col gap-2">
      {preamble.length > 0 ? <Preamble entries={preamble} /> : null}
      <ol className="flex flex-col gap-1">
        {steps.map((step, index) => (
          <li key={index}>
            <Step step={step} position={index + 1} last={index === steps.length - 1} />
          </li>
        ))}
      </ol>
    </div>
  );
}

/**
 * What the launcher said before the agent started — which runtime, whose credential. One quiet
 * banner rather than a code block: it is a sentence, and it was rendered as `<pre>` only because
 * nothing had claimed it.
 */
function Preamble({ entries }: { entries: readonly TranscriptEntry[] }) {
  return (
    <div className="rounded-md border bg-muted/40 px-3 py-2 text-xs leading-relaxed text-muted-foreground">
      {entries.map((entry, index) =>
        // The launcher's own lines are sentences, not code. They arrived as `raw` only because
        // nothing in the log is JSON until the runtime starts talking, and `<pre>` made a plain
        // English sentence look like a machine's output.
        entry.kind === "raw" ? (
          <p key={index} className="m-0">
            {entry.body}
          </p>
        ) : (
          <Entry key={index} entry={entry} />
        ),
      )}
    </div>
  );
}

/** The flat view: every entry in order, no grouping. */
function Lines({ entries }: { entries: readonly TranscriptEntry[] }) {
  return (
    <ol className="flex flex-col gap-2">
      {entries.map((entry, index) => (
        <li key={index}>
          <Entry entry={entry} />
        </li>
      ))}
    </ol>
  );
}

/**
 * One step, as a disclosure. Native `<details>` because the app has no accordion primitive and this
 * one needs none: the summary is the row, and the browser gives keyboard and screen-reader behaviour
 * for free.
 */
function Step({ step, position, last }: { step: TranscriptStep; position: number; last: boolean }) {
  const meta = [
    step.toolCount > 0
      ? tCount(step.toolCount, "run.transcript.toolsInStep.one", "run.transcript.toolsInStep.other")
      : null,
    duration(step.durationMs),
  ].filter((part): part is string => part !== null);

  return (
    <details
      // A crash never hides, and the last step is where a finished Run's answer is.
      open={step.failed || last}
      className="group rounded-md px-1 open:bg-muted/30"
    >
      <summary className="flex cursor-pointer list-none items-center gap-2 rounded-sm py-1.5 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
        <ChevronRight
          className="size-3.5 shrink-0 text-muted-foreground transition-transform group-open:rotate-90"
          aria-hidden="true"
        />
        {step.failed ? (
          <TriangleAlert className="size-4 shrink-0 text-destructive" aria-hidden="true" />
        ) : (
          <Check className="size-4 shrink-0 text-success" aria-hidden="true" />
        )}
        <span className="min-w-0 flex-1 truncate text-[13px] font-medium">
          {step.title ?? `${t("run.transcript.step")} ${position}`}
        </span>
        {step.failed ? (
          <Badge
            variant="outline"
            className="border-destructive/40 bg-destructive/10 text-destructive"
          >
            {t("run.transcript.stepFailed")}
          </Badge>
        ) : null}
        {step.truncated ? <TruncatedNote /> : null}
        {meta.length > 0 ? (
          <span className="shrink-0 text-xs text-muted-foreground">{meta.join(" · ")}</span>
        ) : null}
      </summary>
      {/* The rail ties a step's lines to the step, so a long one cannot be mistaken for the next. */}
      <div className="ml-2 border-l pt-1 pb-2 pl-4">
        <Lines entries={step.entries} />
      </div>
    </details>
  );
}

/** Seconds while that reads naturally, minutes once it does not. Null stays absent, never "0s". */
function duration(ms: number | null): string | null {
  if (ms === null || ms < 0) return null;
  const seconds = ms / 1000;
  if (seconds < 60) return `${seconds < 10 ? seconds.toFixed(1) : Math.round(seconds)}s`;
  const minutes = Math.floor(seconds / 60);
  return `${minutes}m ${Math.round(seconds - minutes * 60)}s`;
}

/**
 * A running total from what the lines carry. Unknown rather than zero when they carry nothing
 * (BR-011, design D4) — zero is a claim, unknown is a fact.
 */
export function TranscriptSpend({ totals }: { totals: TranscriptTotals }) {
  const tokens =
    totals.inputTokens === null
      ? t("run.transcript.unknown")
      : `${totals.inputTokens.toLocaleString("en")} ${t("run.transcript.in")} / ${(
          totals.outputTokens ?? 0
        ).toLocaleString("en")} ${t("run.transcript.out")}`;

  const cost =
    totals.costUsd === null ? t("run.transcript.unknown") : `$${totals.costUsd.toFixed(4)}`;

  return (
    <span className="font-mono text-xs text-muted-foreground" title={t("run.transcript.spend")}>
      {tokens} · {cost}
    </span>
  );
}

export type OutputViewMode = "readable" | "raw";

/**
 * Readable or Raw (turn 10 ④). Two radios rather than a switch, because they are two named views
 * and a switch would have to imply which one is "on". Segmented so the choice and its alternative
 * are visible at once.
 */
export function OutputView({
  value,
  onChange,
}: {
  value: OutputViewMode;
  onChange: (next: OutputViewMode) => void;
}) {
  return (
    <div
      role="radiogroup"
      aria-label={t("run.transcript.viewLabel")}
      className="flex rounded-md bg-muted p-0.5"
    >
      {(
        [
          ["readable", t("run.transcript.viewReadable")],
          ["raw", t("run.transcript.viewRaw")],
        ] as const
      ).map(([mode, copy]) => (
        <button
          key={mode}
          type="button"
          role="radio"
          aria-checked={value === mode}
          onClick={() => onChange(mode)}
          className={cn(
            "rounded-sm px-2 py-0.5 text-xs font-medium outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50",
            value === mode
              ? "bg-background text-foreground shadow-sm"
              : "text-muted-foreground hover:text-foreground",
          )}
        >
          {copy}
        </button>
      ))}
    </div>
  );
}

/** How many steps and tool calls the Run took — its shape, before any of its content. */
export function TranscriptShape({ transcript }: { transcript: Transcript }) {
  if (transcript.steps.length === 0) return null;

  return (
    <span className="text-xs text-muted-foreground">
      {tCount(
        transcript.steps.length,
        "run.transcript.stepCount.one",
        "run.transcript.stepCount.other",
      )}
      {" · "}
      {tCount(
        transcript.toolCount,
        "run.transcript.toolCount.one",
        "run.transcript.toolCount.other",
      )}
    </span>
  );
}

/**
 * A detail block. Wraps rather than scrolls: these carry whole files and 8 KB fragments, and a
 * single unwrapped line turns the panel into a horizontal scroll strip showing ~1% of itself.
 * `break-all` because the long tokens here are paths, JSON and shell commands, which have no
 * spaces to break at.
 */
function Detail({ children }: { children: string }) {
  return (
    <pre className="mt-2 max-h-96 overflow-y-auto rounded-md bg-muted p-2 font-mono text-xs whitespace-pre-wrap break-all">
      {children}
    </pre>
  );
}

/** Says the record is a fragment, wherever one is shown. Same treatment readiness uses to warn. */
function TruncatedNote() {
  return (
    <Badge
      variant="outline"
      className="border-warning/40 bg-warning/15 text-warning"
      title={t("run.transcript.truncatedTitle")}
    >
      {t("run.transcript.truncated")}
    </Badge>
  );
}

function Entry({ entry }: { entry: TranscriptEntry }) {
  if (entry.kind === "text") {
    return (
      <div
        // The same class the Plan uses for sanitised model output, so the two read alike. Worth
        // knowing: no CSS in this app defines `.prose`, so it currently styles nothing — matching the
        // Plan is still the right call, and giving it meaning is a design-system change, not this one.
        className="prose text-sm leading-relaxed"
        // Sanitised by renderStoryMarkdown (design D3): agent output is model output, exactly as
        // untrusted as a Story description, so it takes the pipeline that already made that judgement
        // rather than a second opinion.
        dangerouslySetInnerHTML={{ __html: renderStoryMarkdown(entry.body) }}
      />
    );
  }

  if (entry.kind === "tool") {
    return (
      <details className="group text-sm">
        <summary className="flex cursor-pointer list-none items-center gap-2 rounded-sm py-0.5 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          <ChevronRight
            className="size-3 shrink-0 text-muted-foreground transition-transform group-open:rotate-90"
            aria-hidden="true"
          />
          <span className="shrink-0 rounded-sm bg-accent px-2 py-0.5 font-mono text-[10.5px] font-semibold text-accent-foreground">
            {entry.tool}
          </span>
          {entry.subject ? (
            <span
              className="min-w-0 flex-1 truncate font-mono text-xs text-muted-foreground"
              title={entry.subject}
            >
              {elide(entry.subject)}
            </span>
          ) : (
            <span className="flex-1" />
          )}
          {entry.status ? <ToolStatus status={entry.status} /> : null}
          {entry.truncated ? <TruncatedNote /> : null}
        </summary>
        <Detail>{entry.detail}</Detail>
      </details>
    );
  }

  if (entry.kind === "event") {
    return (
      <details className="group text-sm">
        <summary className="flex cursor-pointer list-none items-center gap-2 rounded-sm text-xs text-muted-foreground outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          <ChevronRight
            className="size-3 shrink-0 transition-transform group-open:rotate-90"
            aria-hidden="true"
          />
          {entry.label}
          {entry.truncated ? <TruncatedNote /> : null}
        </summary>
        <Detail>{entry.detail}</Detail>
      </details>
    );
  }

  if (entry.kind === "boundary") {
    // Only the flat view reaches this: the grouped view reads boundaries to build its blocks and
    // never renders them. Kept visible here so the verbatim view stays truly complete.
    return (
      <details className="group text-sm">
        <summary className="flex cursor-pointer list-none items-center gap-2 rounded-sm text-xs text-muted-foreground outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          <ChevronRight
            className="size-3 shrink-0 transition-transform group-open:rotate-90"
            aria-hidden="true"
          />
          {`step ${entry.edge}`}
        </summary>
        <Detail>{entry.detail}</Detail>
      </details>
    );
  }

  // Verbatim, and deliberately not inside a disclosure: an uninterpretable line is often the crash or
  // the stack trace somebody opened this page to find. Wrapped and height-capped all the same — a
  // line nobody can read without dragging a scrollbar sideways is not really visible either.
  return (
    <>
      {entry.truncated ? <TruncatedNote /> : null}
      <pre className="max-h-96 overflow-y-auto rounded-md bg-muted p-2 font-mono text-xs whitespace-pre-wrap break-all">
        {entry.body}
      </pre>
    </>
  );
}

/**
 * How many trailing path segments identify a file well enough. Three reads as
 * `…/docs/process/definition-of-ready.md` — the name, and enough of its parents to place it.
 */
const PATH_SEGMENTS = 3;

/**
 * A subject at a readable length, shortened from whichever end matters least.
 *
 * A path is identified by its **tail** — the filename — while a command is identified by its
 * **head**, `gh issue view 108 …`. So commands keep the CSS tail-truncation they already suited,
 * and paths are cut here to their last few segments.
 *
 * Two wrong turns worth recording. `direction: rtl` ellipsizes the front, but it rewrites the
 * string's bidi order too: a path beginning `/var` rendered with its leading slash relocated to the
 * far right, so the row read `…/definition-of-ready.md/`. Cutting to a fixed character budget then
 * left the result still wider than the column, so CSS truncated the tail as well and the filename —
 * the one part worth showing — disappeared from both ends at once. Segments avoid both: the result
 * is short enough that nothing clips it, and the name always survives.
 */
function elide(subject: string): string {
  const looksLikeAPath = subject.includes("/") && !subject.trimStart().includes(" ");
  if (!looksLikeAPath) return subject;

  const segments = subject.split("/").filter((segment) => segment.length > 0);
  if (segments.length <= PATH_SEGMENTS) return subject;

  return `…/${segments.slice(-PATH_SEGMENTS).join("/")}`;
}

/** The tool's own verdict, quiet when ordinary and loud when not. */
function ToolStatus({ status }: { status: string }) {
  const ok = status === "completed" || status === "success" || status === "ok";

  return (
    <span
      className={cn(
        "shrink-0 font-mono text-[10.5px]",
        ok ? "text-muted-foreground" : "text-destructive",
      )}
    >
      {status}
    </span>
  );
}
