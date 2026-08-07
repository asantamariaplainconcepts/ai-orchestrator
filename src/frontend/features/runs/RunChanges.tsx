import { useEffect, useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { useRunChanges } from "./useRuns";

/** How much of a long patch renders before the reader asks for the rest (design D4). */
const PATCH_PAGE_LINES = 40;

/** The width below which the diff wraps instead of scrolling sideways (turn 7b). */
const NARROW = "(max-width: 47.999rem)";

function useNarrowViewport() {
  const [narrow, setNarrow] = useState(() => window.matchMedia(NARROW).matches);

  useEffect(() => {
    const query = window.matchMedia(NARROW);
    const read = () => setNarrow(query.matches);
    read();
    query.addEventListener("change", read);
    return () => query.removeEventListener("change", read);
  }, []);

  return narrow;
}

/**
 * One parsed diff line: the marker lives in the gutter, never inside the text, so a wrapped line
 * keeps its meaning (turn 7b) — and the old/new numbers come from the hunk headers the patch
 * already carries, because a diff without line numbers is a quote without a page.
 */
interface DiffLine {
  kind: "hunk" | "add" | "remove" | "context";
  text: string;
  oldLine: number | null;
  newLine: number | null;
}

/** Walks `@@ -a,b +c,d @@` headers to number every line — data the patch already holds. */
function parsePatch(patch: string): DiffLine[] {
  const lines: DiffLine[] = [];
  let oldLine = 0;
  let newLine = 0;

  for (const raw of patch.split("\n")) {
    const hunk = /^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(raw);
    if (hunk) {
      oldLine = Number(hunk[1]);
      newLine = Number(hunk[2]);
      lines.push({ kind: "hunk", text: raw, oldLine: null, newLine: null });
      continue;
    }

    // "+++"/"---" are file headers, not content — they must not read as a huge add/remove.
    if (raw.startsWith("+") && !raw.startsWith("+++")) {
      lines.push({ kind: "add", text: raw.slice(1), oldLine: null, newLine: newLine++ });
    } else if (raw.startsWith("-") && !raw.startsWith("---")) {
      lines.push({ kind: "remove", text: raw.slice(1), oldLine: oldLine++, newLine: null });
    } else {
      lines.push({
        kind: "context",
        text: raw.startsWith(" ") ? raw.slice(1) : raw,
        oldLine: oldLine++,
        newLine: newLine++,
      });
    }
  }

  return lines;
}

/**
 * UC-024 — what the Agent actually changed, at the width reading needs (turn 7): the body's, with
 * line numbers, a sticky per-file header and per-file collapse. The patch is the vendor's own
 * (design D2); colour distinguishes added from removed and nothing else (D5). A file whose patch
 * cannot be shown says why rather than showing a partial diff (D3).
 */
export function RunChanges({ projectId, runId }: { projectId: string; runId: string }) {
  const changes = useRunChanges(projectId, runId);
  const change = changes.data?.change ?? null;
  const narrow = useNarrowViewport();

  // Which files the reader collapsed or expanded by hand. Null means "nobody touched anything":
  // the default is then computed per render — on a phone every file after the first arrives
  // collapsed (turn 7b), on a desktop none do — and the first toggle materialises it.
  const [collapsed, setCollapsed] = useState<ReadonlySet<string> | null>(null);
  // Files whose full patch the reader asked for; everything else shows the first page.
  const [expandedPatches, setExpandedPatches] = useState<ReadonlySet<string>>(() => new Set());

  const isCollapsed = (path: string, index: number) =>
    collapsed ? collapsed.has(path) : narrow && index > 0;

  const toggle = (path: string, index: number) =>
    setCollapsed(() => {
      const defaults = new Set(
        collapsed ?? (narrow ? (change?.files.slice(1).map((file) => file.path) ?? []) : []),
      );
      if (!defaults.delete(path)) defaults.add(path);
      // A collapse toggle must not silently change other files' state on the way.
      void index;
      return defaults;
    });

  return (
    <Card className="gap-0 py-0" id="run-changes">
      <div className="flex flex-wrap items-center gap-2 border-b px-4 py-3">
        <h2 className="text-sm font-semibold">{t("run.section.changes")}</h2>
        {change ? (
          <Badge variant="secondary" className="font-mono">
            #{change.number}
          </Badge>
        ) : null}
        {change ? (
          <Button asChild variant="outline" size="xs" className="ml-auto">
            <a href={change.url} target="_blank" rel="noreferrer">
              {t("runs.table.openOutput")}
            </a>
          </Button>
        ) : null}
      </div>

      <div className="flex flex-col gap-3 px-4 py-3">
        {changes.isPending && (
          <p className="text-sm text-muted-foreground">{t("run.changes.loading")}</p>
        )}
        {changes.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("run.changes.error")}
          </p>
        )}

        {/* Three absences, three answers: no pull request yet, a change that touched nothing,
            and a read that failed (above). */}
        {changes.data && !change && (
          <p className="text-sm text-muted-foreground">{t("run.changes.noChange")}</p>
        )}
        {change && change.files.length === 0 && (
          <p className="text-sm text-muted-foreground">{t("run.changes.noFiles")}</p>
        )}

        {change?.files.map((file, index) => {
          const fileCollapsed = isCollapsed(file.path, index);

          return (
            <div className="overflow-hidden rounded-md border" key={file.path}>
              {/* Sticky within the page scroll, so a long diff never loses which file it is in.
                  The whole header is the collapse toggle — the affordance a per-file reader
                  actually wants is "get this one out of my way". */}
              <button
                type="button"
                aria-expanded={!fileCollapsed}
                onClick={() => toggle(file.path, index)}
                className="sticky top-0 z-10 flex w-full flex-wrap items-center gap-2 border-b bg-muted px-3 py-2 text-left outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:ring-inset"
              >
                {/* Left truncation: the file name is the meaningful end of a long path (7b). */}
                <span className="min-w-0 flex-1 truncate font-mono text-xs [direction:rtl]">
                  <span className="[direction:ltr] [unicode-bidi:isolate]">{file.path}</span>
                </span>
                <Badge variant="outline" className="text-[10px]">
                  {file.status}
                </Badge>
                <span className="font-mono text-xs text-success">+{file.additions}</span>
                <span className="font-mono text-xs text-destructive">−{file.deletions}</span>
                <span className="text-[10px] text-muted-foreground">
                  {fileCollapsed ? t("run.changes.expand") : t("run.changes.collapse")}
                </span>
              </button>

              {fileCollapsed ? null : file.patch ? (
                <Patch
                  path={file.path}
                  patch={file.patch}
                  narrow={narrow}
                  expanded={expandedPatches.has(file.path)}
                  onExpand={() => setExpandedPatches((current) => new Set(current).add(file.path))}
                />
              ) : (
                <p className="px-3 py-2 text-xs text-muted-foreground">
                  {file.patchOmittedReason === "Binary"
                    ? t("run.changes.binary")
                    : t("run.changes.tooLarge")}
                </p>
              )}
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function Patch({
  path,
  patch,
  narrow,
  expanded,
  onExpand,
}: {
  path: string;
  patch: string;
  narrow: boolean;
  expanded: boolean;
  onExpand: () => void;
}) {
  const lines = parsePatch(patch);
  const visible = expanded ? lines : lines.slice(0, PATCH_PAGE_LINES);
  const hidden = lines.length - visible.length;

  return (
    <div className={cn("py-1 font-mono text-xs leading-relaxed", !narrow && "overflow-x-auto")}>
      {visible.map((line, index) => (
        <div
          // The patch has no stable per-line id; its index within this file is the only
          // honest key, and the list never reorders.
          key={`${path}:${index}`}
          className={cn(
            "flex",
            line.kind === "hunk" && "bg-info/10 text-info",
            line.kind === "add" && "bg-success/10",
            line.kind === "remove" && "bg-destructive/10",
          )}
        >
          {/* Line numbers at reading width; on a phone the gutter is the marker itself, fixed
              while the text wraps (7b) — a wrapped line keeps saying what it is. */}
          {narrow ? (
            <span aria-hidden="true" className="w-5 shrink-0 text-center select-none">
              {line.kind === "add" ? "+" : line.kind === "remove" ? "−" : ""}
            </span>
          ) : (
            <>
              <span className="w-10 shrink-0 pr-2 text-right text-muted-foreground select-none">
                {line.oldLine ?? ""}
              </span>
              <span className="w-10 shrink-0 pr-2 text-right text-muted-foreground select-none">
                {line.newLine ?? ""}
              </span>
              <span aria-hidden="true" className="w-4 shrink-0 text-center select-none">
                {line.kind === "add" ? "+" : line.kind === "remove" ? "−" : ""}
              </span>
            </>
          )}
          <span
            className={cn(
              "min-w-0 flex-1 pr-3",
              narrow ? "whitespace-pre-wrap break-words" : "whitespace-pre",
            )}
          >
            {line.text}
          </span>
        </div>
      ))}

      {hidden > 0 ? (
        <Button type="button" variant="ghost" size="sm" className="mx-3 my-1" onClick={onExpand}>
          {t("run.changes.showMore")}{" "}
          {tCount(hidden, "run.changes.moreLine.one", "run.changes.moreLine.other")}
        </Button>
      ) : null}
    </div>
  );
}
