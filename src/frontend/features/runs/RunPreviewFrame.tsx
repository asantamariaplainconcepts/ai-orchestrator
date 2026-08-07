import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";

/**
 * The Run's own application, running, while the Run runs (run-previews). The live output's
 * sibling: both exist only while the Run does, and both disappear the same way — which is why
 * this renders nothing at all for a Run that is no longer live rather than explaining what used
 * to be here.
 *
 * What is framed is code an agent wrote, so the frame grants it nothing: `sandbox` permits
 * scripts and forms so the application is usable, and withholds `allow-same-origin`, so the
 * document cannot read the portal's session or call its API as the Member. The relay applies the
 * same confinement server-side; either alone is one mistake away from agent-authored script
 * running with the portal's authority.
 */
export function RunPreviewFrame({
  projectId,
  runId,
  available,
}: {
  projectId: string;
  runId: string;
  available: boolean;
}) {
  // Not a disabled control and not an explanation — nothing. A preview is not something a Run
  // leaves behind, and an affordance implying otherwise would promise what no Run can keep.
  if (!available) return null;

  return (
    <div className="flex flex-col gap-2">
      <span className="flex flex-wrap items-center gap-2">
        <h2 className="text-sm font-semibold">{t("run.preview.heading")}</h2>
        <Badge variant="outline" className="border-info/40 bg-info/10 text-info">
          <span className="size-1.5 animate-pulse rounded-full bg-info" aria-hidden="true" />
          {t("run.log.live")}
        </Badge>
        <span className="text-xs text-muted-foreground">{t("run.preview.whose")}</span>
      </span>
      <Card className="gap-0 overflow-hidden py-0">
        <iframe
          title={t("run.preview.heading")}
          src={`/api/projects/${projectId}/runs/${runId}/preview/serve/`}
          // Scripts and forms so the application works; no allow-same-origin, so it cannot
          // reach the portal it is framed in.
          sandbox="allow-scripts allow-forms allow-popups"
          referrerPolicy="no-referrer"
          className="h-[32rem] w-full border-0 bg-surface"
        />
      </Card>
    </div>
  );
}
