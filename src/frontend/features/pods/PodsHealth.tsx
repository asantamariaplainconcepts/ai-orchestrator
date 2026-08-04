import { TriangleAlert } from "lucide-react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { CopyLine } from "@/shared/ui/copy-line";
import type { AgentPodsView } from "./usePods";

/**
 * The pod host's condition as one word — the single vocabulary the health pill, the chip's
 * status line and the unavailable card all derive from (design review 5b/5c).
 */
export type PodsHealth = "checking" | "down" | "imageMissing" | "ready";

export function podsHealth(view: AgentPodsView): PodsHealth {
  if (view.checkedAt === null) return "checking";
  if (!view.dockerReady) return "down";
  if (view.imagePresent === false) return "imageMissing";
  return "ready";
}

const HEALTH_STYLE = {
  checking: "border-border bg-muted text-muted-foreground",
  down: "border-destructive/40 bg-destructive/10 text-destructive",
  imageMissing: "border-warning/40 bg-warning/15 text-warning",
  ready: "border-success/40 bg-success/10 text-success",
} as const satisfies Record<PodsHealth, string>;

export function podsHealthLabel(health: PodsHealth): string {
  switch (health) {
    case "checking":
      return t("pods.checking");
    case "down":
      return t("pods.dockerDown");
    case "imageMissing":
      return t("pods.imageMissing");
    default:
      return t("pods.dockerReady");
  }
}

/** The health pill beside the panel's heading — mock 5b's "Docker ready". */
export function PodsHealthBadge({ view }: { view: AgentPodsView }) {
  const health = podsHealth(view);
  return (
    <Badge variant="outline" className={HEALTH_STYLE[health]}>
      {podsHealthLabel(health)}
    </Badge>
  );
}

/** Relative for recency, absolute past a day — the content fundamentals' rule. */
function formatAgo(iso: string): string {
  const then = new Date(iso);
  const seconds = Math.max(0, Math.round((Date.now() - then.getTime()) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return then.toLocaleDateString();
}

/**
 * The not-ready state, said where the Run was going to launch (mock 5c). Two causes, two
 * remedies: a down daemon is started (destructive, with the command), a missing image is built
 * (warning, with its one-time command) — and in both, the rule the copy leads with is that the
 * Run never fails for this; it queues and dispatches on its own.
 *
 * `compact` is the environment-chip form: the sentence and the command, without the cadence
 * line the panel carries.
 */
export function PodsUnavailableCard({
  view,
  compact = false,
}: {
  view: AgentPodsView;
  compact?: boolean;
}) {
  const health = podsHealth(view);
  if (health !== "down" && health !== "imageMissing") return null;

  const down = health === "down";

  return (
    <div className="flex flex-col gap-2.5">
      <div
        className={cn(
          "flex items-start gap-2.5 rounded-lg border p-3",
          down ? "border-destructive/40 bg-destructive/10" : "border-warning/40 bg-warning/10",
        )}
      >
        <TriangleAlert
          aria-hidden="true"
          className={cn("mt-0.5 size-3.5 shrink-0", down ? "text-destructive" : "text-warning")}
        />
        <span className="flex flex-col gap-1">
          <span className="text-[13px] font-semibold">
            {down ? t("pods.unavailableTitle") : t("pods.imageMissingTitle")}
          </span>
          <span className="text-xs leading-relaxed text-muted-foreground">
            {down ? t("pods.unavailableBody") : t("pods.imageMissingBody")}
          </span>
        </span>
      </div>
      <div className="flex flex-col gap-1.5">
        <span className="text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
          {t("pods.try")}
        </span>
        <CopyLine text={down ? t("pods.tryCommand.compose") : t("pods.tryCommand.build")} />
        {!compact && view.checkedAt ? (
          <span className="text-[11px] text-muted-foreground">
            {t("pods.checked")} {formatAgo(view.checkedAt)} · {t("pods.retries")}{" "}
            {view.retrySeconds}s
          </span>
        ) : null}
      </div>
    </div>
  );
}
