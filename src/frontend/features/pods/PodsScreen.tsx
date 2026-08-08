import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { RuntimesReadiness } from "./RuntimesReadiness";
import { usePods } from "./usePods";

/**
 * The agent runtimes of the machine that executes Runs (#279). This page also showed pods until
 * #296 retired that substrate — a container per Run, watched over the docker socket — at which
 * point the pods half could only ever have said "not hosted here", in every habitat, forever.
 * What a Run waiting its turn looks like now is what it always really was: a Queued Run on the
 * Runs list. Reached from the environment chip, which is where "this machine" lives in the shell.
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

        {pods.data && <RuntimesReadiness view={pods.data} />}
      </div>
    </AppShell>
  );
}
