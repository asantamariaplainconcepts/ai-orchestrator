import { Link } from "react-router";
import { useAutomations } from "@/features/automations/useAutomations";
import { t, tCount } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { formatCost, useRuns } from "./useRuns";
import type { RunView } from "./types";

/**
 * UC-021 — the loop's output, observable. Automation columns are a client-side join with the
 * automations query (design D1): the Run records the id, current configuration supplies the
 * details, and a Run whose Automation is gone shows empty cells rather than a guess.
 * <p>
 * One tree, two layouts (dashboard-tabs design D6): the same rows read as a line from md up and
 * stack below it, so no action can exist on one width and not the other.
 * </p>
 */
export function RunsSection({
  projectId,
  storyFilter,
  onClearFilter,
}: {
  projectId: string;
  storyFilter: string | null;
  onClearFilter: () => void;
}) {
  const runs = useRuns(projectId, storyFilter);
  const automations = useAutomations(projectId);

  const rows = runs.data ?? [];
  const byId = new Map((automations.data ?? []).map((automation) => [automation.id, automation]));

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-semibold">{t("runs.heading")}</h2>
          <Badge variant="secondary">
            {tCount(rows.length, "runs.count.one", "runs.count.other")}
          </Badge>
          {storyFilter ? (
            <Badge variant="outline">
              {t("runs.filteredByStory")} <span className="ml-1 font-mono">#{storyFilter}</span>
            </Badge>
          ) : null}
        </div>
        {storyFilter ? (
          <Button variant="outline" size="sm" type="button" onClick={onClearFilter}>
            {t("runs.clearFilter")}
          </Button>
        ) : null}
      </div>

      {runs.isPending && <p className="text-sm text-muted-foreground">{t("runs.loading")}</p>}
      {runs.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("runs.error")}
        </p>
      )}
      {runs.data && rows.length === 0 && (
        <p className="text-sm text-muted-foreground">
          {storyFilter ? t("runs.emptyForStory") : t("runs.empty")}
        </p>
      )}

      {rows.length > 0 && (
        <Card>
          <CardContent>
            <ul className="divide-y">
              {rows.map((run) => {
                const automation =
                  run.automationId !== null ? byId.get(run.automationId) : undefined;
                return (
                  <li
                    key={run.id}
                    className="flex flex-col gap-2 py-3 first:pt-0 last:pb-0 md:flex-row md:items-center md:justify-between"
                  >
                    <div className="flex min-w-0 flex-wrap items-center gap-2">
                      <Link
                        className="font-mono text-sm font-medium transition-colors hover:text-primary"
                        to={`/projects/${projectId}/runs/${run.id}`}
                      >
                        {run.targetChangeNumber !== null
                          ? `PR #${run.targetChangeNumber}`
                          : `#${run.vendorStoryId}`}
                      </Link>
                      <StateBadge state={run.state} />
                      {automation ? (
                        <>
                          <Badge variant="secondary">{automation.triggerLabel}</Badge>
                          <span className="truncate text-xs text-muted-foreground">
                            {automation.action} · {automation.runtime}
                          </span>
                        </>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </div>

                    <div className="flex shrink-0 flex-wrap items-center gap-3 text-xs text-muted-foreground">
                      <span>{formatWhen(run.createdAt)}</span>
                      <span className="font-mono">
                        {formatCost(run.costUsd) ?? t("runs.cost.unknown")}
                      </span>
                      {run.outputLink ? (
                        <a
                          className="text-primary hover:underline"
                          href={run.outputLink}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {t("runs.table.openOutput")}
                        </a>
                      ) : null}
                    </div>
                  </li>
                );
              })}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function StateBadge({ state }: { state: RunView["state"] }) {
  if (state === "Succeeded" || state === "Executing" || state === "Planning") {
    return <Badge className="bg-success text-success-foreground">{state}</Badge>;
  }
  if (state === "AwaitingApproval") {
    return <Badge className="bg-warning text-warning-foreground">{state}</Badge>;
  }
  if (state === "Failed") {
    return <Badge variant="destructive">{state}</Badge>;
  }
  return <Badge variant="outline">{state}</Badge>;
}

/** Relative for recency, absolute past a day — the content fundamentals' rule. */
function formatWhen(iso: string): string {
  const then = new Date(iso);
  const minutes = Math.round((Date.now() - then.getTime()) / 60000);

  if (minutes < 1) return new Intl.RelativeTimeFormat("en").format(0, "minute");
  if (minutes < 60) return new Intl.RelativeTimeFormat("en").format(-minutes, "minute");
  if (minutes < 60 * 24) {
    return new Intl.RelativeTimeFormat("en").format(-Math.round(minutes / 60), "hour");
  }
  return then.toLocaleDateString("en", { dateStyle: "medium" });
}
