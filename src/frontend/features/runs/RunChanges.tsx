import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { useRunChanges } from "./useRuns";

/**
 * UC-024 — what the Agent actually changed, under the Plan it was approved against. The patch
 * is the vendor's own (design D2); colour distinguishes added from removed and nothing else
 * (D5). A file whose patch cannot be shown says why rather than showing a partial diff (D3).
 */
export function RunChanges({ projectId, runId }: { projectId: string; runId: string }) {
  const changes = useRunChanges(projectId, runId);
  const change = changes.data?.change ?? null;

  return (
    <Card className="gap-0 py-0">
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

        {change?.files.map((file) => (
          <div className="overflow-hidden rounded-md border" key={file.path}>
            <div className="flex flex-wrap items-center gap-2 border-b bg-muted/40 px-3 py-2">
              <span className="min-w-0 flex-1 truncate font-mono text-xs">{file.path}</span>
              <Badge variant="outline" className="text-[10px]">
                {file.status}
              </Badge>
              <span className="font-mono text-xs text-success">+{file.additions}</span>
              <span className="font-mono text-xs text-destructive">−{file.deletions}</span>
            </div>

            {file.patch ? (
              <div className="overflow-x-auto py-1 font-mono text-xs leading-relaxed">
                {file.patch.split("\n").map((line, index) => (
                  <span
                    // The patch has no stable per-line id; its index within this file is the only
                    // honest key, and the list never reorders.
                    key={`${file.path}:${index}`}
                    className={cn("block px-3 whitespace-pre", lineClass(line))}
                  >
                    {line}
                  </span>
                ))}
              </div>
            ) : (
              <p className="px-3 py-2 text-xs text-muted-foreground">
                {file.patchOmittedReason === "Binary"
                  ? t("run.changes.binary")
                  : t("run.changes.tooLarge")}
              </p>
            )}
          </div>
        ))}
      </div>
    </Card>
  );
}

function lineClass(line: string): string {
  if (line.startsWith("@@")) return "bg-info/10 text-info";
  // "+++"/"---" are file headers, not content — they must not read as a huge add/remove.
  if (line.startsWith("+") && !line.startsWith("+++")) return "bg-success/10";
  if (line.startsWith("-") && !line.startsWith("---")) return "bg-destructive/10";
  return "";
}
