import { Link } from "react-router";
import { t, tCount } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";
import { PodsHealthBadge, PodsUnavailableCard } from "./PodsHealth";
import { RuntimesReadiness } from "./RuntimesReadiness";
import { usePods, type PodRow } from "./usePods";

/** Minutes-first: a pod's working life is minutes, and "0s" churn would read as a glitch. */
function formatElapsed(iso: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  return `${hours}h`;
}

/**
 * The Agent pods of this machine (design review 5b) — what today only `docker ps` shows, as a
 * page: each pod linked to its Run, the queue explained, the machine's concurrency stated with
 * its scope, and remote pods declared as a direction rather than pretended into existence.
 * Reached from the environment chip, which is where "this machine" already lives in the shell.
 */
export function PodsScreen() {
  // Run cadence while the panel is watched — the same 5s a run's own screen polls at.
  const pods = usePods({ refetchInterval: 5_000 });

  return (
    <AppShell crumbs={[{ label: t("pods.title") }]} title={t("pods.title")}>
      <div className="flex max-w-3xl flex-col gap-4">
        {pods.isPending && <p className="text-sm text-muted-foreground">{t("pods.loading")}</p>}
        {pods.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("pods.error")}
          </p>
        )}

        {pods.data && !pods.data.hosted && (
          <p className="text-sm text-muted-foreground">{t("pods.notHosted")}</p>
        )}

        {/* The runtimes ride the same page whichever way Runs execute here (#279): the dev
            loop has runtimes and no pods, the compose habitat has both. Unhosted renders
            nothing — the worker's image carries its own. */}
        {pods.data && <RuntimesReadiness view={pods.data.runtimes} />}

        {pods.data && pods.data.hosted && (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="flex items-baseline gap-2">
                <h2 className="text-sm font-semibold">{t("pods.heading")}</h2>
                <span className="text-xs text-muted-foreground">{t("pods.onThisMachine")}</span>
              </span>
              <PodsHealthBadge view={pods.data} />
            </div>

            <PodsUnavailableCard view={pods.data} />

            <Card className="gap-0 py-0">
              {pods.data.pods.length === 0 ? (
                <p className="px-4 py-3 text-sm text-muted-foreground">{t("pods.empty")}</p>
              ) : (
                <ul className="flex flex-col">
                  {pods.data.pods.map((pod) => (
                    <PodRowItem key={pod.runId} pod={pod} />
                  ))}
                </ul>
              )}
            </Card>

            <div className="grid gap-2.5 sm:grid-cols-2">
              <Card className="gap-1 px-3.5 py-3">
                <span className="text-xs text-muted-foreground">{t("pods.concurrency")}</span>
                <span className="text-sm font-semibold">
                  {tCount(
                    pods.data.maxConcurrentPods,
                    "pods.concurrency.one",
                    "pods.concurrency.other",
                  )}
                </span>
                <span className="text-[11px] text-muted-foreground">
                  {t("pods.concurrencyNote")}
                </span>
              </Card>
              {/* Declared, not simulated (mock 5b): a dashed cell with no control states the
                  direction — the same Runs on another substrate — without pretending it exists. */}
              <Card className="gap-1 border-dashed px-3.5 py-3">
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  {t("pods.remoteTitle")}
                  <Badge variant="secondary" className="text-[10px]">
                    {t("pods.remoteComing")}
                  </Badge>
                </span>
                <span className="text-xs leading-relaxed text-muted-foreground">
                  {t("pods.remoteNote")}
                </span>
              </Card>
            </div>
          </>
        )}
      </div>
    </AppShell>
  );
}

/**
 * One pod, read like the mock's row: state first, then the Run it is — linked, because the pod
 * is never the destination — then how long it has been at it, or why it has not started.
 */
function PodRowItem({ pod }: { pod: PodRow }) {
  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b px-3.5 py-2.5 last:border-0">
      {pod.executing ? (
        <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
          <span aria-hidden="true" className="size-1.5 animate-pulse rounded-full bg-info" />
          {t("run.state.executing")}
        </Badge>
      ) : (
        <Badge variant="outline" className="border-border bg-muted text-muted-foreground">
          {t("run.state.queued")}
        </Badge>
      )}
      <span className="min-w-0 flex-1 text-xs">
        {pod.projectName ? `${pod.projectName} · ` : null}
        {t("pods.run")}{" "}
        <Link
          className="font-mono text-primary underline-offset-4 hover:underline"
          to={`/projects/${pod.projectId}/runs/${pod.runId}`}
        >
          #{pod.vendorStoryId}
        </Link>
        {pod.triggerLabel ? (
          <span className="font-mono text-[11px] text-muted-foreground">
            {" · "}
            {pod.triggerLabel}
            {pod.runtime ? ` · ${pod.runtime}` : null}
          </span>
        ) : null}
      </span>
      <span className="text-[11px] text-muted-foreground">
        {pod.executing ? formatElapsed(pod.sightedAt) : t("pods.waitsForSlot")}
      </span>
    </li>
  );
}
