import { t } from "@/shared/i18n";
import { AppShell } from "@/shared/ui/AppShell";
import { SandboxTerminal } from "./SandboxTerminal";
import { useSandboxes } from "./useSandboxes";

/**
 * The sandboxes of the machine that executes Runs, each openable in a terminal (#311).
 *
 * Machine-scoped rather than project-scoped, like the runtimes panel it sits beside: a sandbox
 * belongs to this machine, and the one most worth entering — left behind by a process that was
 * killed — belongs to no project at all.
 *
 * The habitat's answer and the caller's are rendered as different sentences, deliberately. A
 * deployment hosts no terminal (ADR-0021) and saying "you may not" there would send a reader asking
 * for access that cannot help them.
 */
export function SandboxesScreen() {
  const sandboxes = useSandboxes();

  return (
    <AppShell crumbs={[{ label: t("sandboxes.title") }]} title={t("sandboxes.title")}>
      <div className="flex max-w-3xl flex-col gap-4">
        <p className="text-sm text-muted-foreground">{t("sandboxes.whose")}</p>

        {sandboxes.isPending && (
          <p className="text-sm text-muted-foreground">{t("sandboxes.loading")}</p>
        )}

        {sandboxes.isError && (
          <p className="text-sm text-destructive" role="alert">
            {t("sandboxes.error")}
          </p>
        )}

        {/* The habitat first, and on its own terms — not a permission, and not an empty list. */}
        {sandboxes.data && !sandboxes.data.hosted && (
          <p className="text-sm text-muted-foreground">{t("sandboxes.notHosted")}</p>
        )}

        {sandboxes.data?.hosted && !sandboxes.data.permitted && (
          <p className="text-sm text-muted-foreground" role="status">
            {t("sandboxes.forbidden")}
          </p>
        )}

        {sandboxes.data?.hosted &&
          sandboxes.data.permitted &&
          sandboxes.data.sandboxes.length === 0 && (
            <p className="text-sm text-muted-foreground">{t("sandboxes.none")}</p>
          )}

        {sandboxes.data?.hosted &&
          sandboxes.data.permitted &&
          sandboxes.data.sandboxes.map((sandbox) => (
            <SandboxTerminal key={sandbox.name} sandbox={sandbox} />
          ))}
      </div>
    </AppShell>
  );
}
