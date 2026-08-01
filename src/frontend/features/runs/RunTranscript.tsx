import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { t } from "@/shared/i18n";
import type { TranscriptEntry, TranscriptTotals } from "./transcript";

/**
 * The Output section as a transcript (#130). The agent's words are prose, tools are one line each, and
 * anything this product cannot interpret is shown verbatim — completeness first, presentation second
 * (design D5). The card and its header live on the Run screen, which also carries the running spend;
 * this renders the lines.
 */
export function RunTranscript({ entries }: { entries: readonly TranscriptEntry[] }) {
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
      <details className="text-sm">
        <summary className="flex cursor-pointer items-center gap-2 rounded-sm outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          <span className="rounded-sm bg-accent px-2 py-0.5 font-mono text-[10.5px] font-semibold text-accent-foreground">
            {entry.tool}
          </span>
          {entry.subject ? (
            <span className="min-w-0 truncate font-mono text-xs text-muted-foreground">
              {entry.subject}
            </span>
          ) : null}
        </summary>
        <pre className="mt-2 overflow-x-auto rounded-md bg-muted p-2 font-mono text-xs">
          {entry.detail}
        </pre>
      </details>
    );
  }

  if (entry.kind === "event") {
    return (
      <details className="text-sm">
        <summary className="cursor-pointer rounded-sm text-xs text-muted-foreground outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50">
          {entry.label}
        </summary>
        <pre className="mt-2 overflow-x-auto rounded-md bg-muted p-2 font-mono text-xs">
          {entry.detail}
        </pre>
      </details>
    );
  }

  // Verbatim, and deliberately not inside a disclosure: an uninterpretable line is often the crash or
  // the stack trace somebody opened this page to find.
  return (
    <pre className="overflow-x-auto rounded-md bg-muted p-2 font-mono text-xs">{entry.body}</pre>
  );
}
