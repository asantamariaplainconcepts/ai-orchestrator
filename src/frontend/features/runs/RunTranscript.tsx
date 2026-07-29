import { useMemo } from "react";
import { renderStoryMarkdown } from "@/features/backlog/markdown";
import { t } from "@/shared/i18n";
import { parseTranscript } from "./transcript";
import type { TranscriptEntry, TranscriptTotals } from "./transcript";

/**
 * The Output section as a transcript (#130). The agent's words are prose, tools are one line each, and
 * anything this product cannot interpret is shown verbatim — completeness first, presentation second
 * (design D5).
 */
export function RunTranscript({ log }: { log: string }) {
  const transcript = useMemo(() => parseTranscript(log), [log]);

  return (
    <div className="flex flex-col gap-3">
      <Spend totals={transcript.totals} />
      <ol className="flex flex-col gap-2">
        {transcript.entries.map((entry, index) => (
          <li key={index}>
            <Entry entry={entry} />
          </li>
        ))}
      </ol>
    </div>
  );
}

/**
 * A running total from what the lines carry. Unknown rather than zero when they carry nothing
 * (BR-011, design D4) — zero is a claim, unknown is a fact.
 */
function Spend({ totals }: { totals: TranscriptTotals }) {
  const tokens =
    totals.inputTokens === null
      ? t("run.transcript.unknown")
      : `${totals.inputTokens.toLocaleString("en")} ${t("run.transcript.in")} / ${(
          totals.outputTokens ?? 0
        ).toLocaleString("en")} ${t("run.transcript.out")}`;

  const cost =
    totals.costUsd === null ? t("run.transcript.unknown") : `$${totals.costUsd.toFixed(4)}`;

  return (
    <p className="text-xs text-muted-foreground">
      {t("run.transcript.spend")}: {tokens} · {cost}
    </p>
  );
}

function Entry({ entry }: { entry: TranscriptEntry }) {
  if (entry.kind === "text") {
    return (
      <div
        // The same class the Plan uses for sanitised model output, so the two read alike. Worth
        // knowing: no CSS in this app defines `.prose`, so it currently styles nothing — matching the
        // Plan is still the right call, and giving it meaning is a design-system change, not this one.
        className="prose text-sm"
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
        <summary className="cursor-pointer">
          <span className="pill pill-neutral">{entry.tool}</span>
          {entry.subject ? (
            <span className="mono ml-2 text-xs text-muted-foreground">{entry.subject}</span>
          ) : null}
        </summary>
        <pre className="mono log-view mt-2 text-xs">{entry.detail}</pre>
      </details>
    );
  }

  if (entry.kind === "event") {
    return (
      <details className="text-sm">
        <summary className="cursor-pointer text-xs text-muted-foreground">{entry.label}</summary>
        <pre className="mono log-view mt-2 text-xs">{entry.detail}</pre>
      </details>
    );
  }

  // Verbatim, and deliberately not inside a disclosure: an uninterpretable line is often the crash or
  // the stack trace somebody opened this page to find.
  return <pre className="mono log-view text-xs">{entry.body}</pre>;
}
