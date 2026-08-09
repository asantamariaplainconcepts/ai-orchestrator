import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { RuntimesReadiness } from "./RuntimesReadiness";
import { useRuntimes } from "./useRuntimes";

/**
 * The agent runtimes of the machine that executes Runs (#279). Reached from the environment chip,
 * which is where "this machine" lives in the shell.
 */
export function RuntimesScreen() {
  // Run cadence while the panel is watched — the same 5s a run's own screen polls at.
  const runtimes = useRuntimes({ refetchInterval: 5_000 });

  return (
    <AppShell crumbs={[{ label: t("runtimes.title") }]} title={t("runtimes.title")}>
      <div className="flex max-w-3xl flex-col gap-4">
        {runtimes.isPending && (
          <p className="text-sm text-muted-foreground">{t("runtimes.loading")}</p>
        )}
        {runtimes.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("runtimes.error")}
          </p>
        )}

        {runtimes.data && !runtimes.data.hosted && (
          <p className="text-sm text-muted-foreground">{t("runtimes.notHosted")}</p>
        )}

        {runtimes.data && <RuntimesReadiness view={runtimes.data} />}
      </div>
    </AppShell>
  );
}
