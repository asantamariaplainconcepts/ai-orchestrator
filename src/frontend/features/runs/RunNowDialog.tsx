import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { NativeSelect } from "@/shared/ui/native-select";

export type Locus = "Local" | "Pod";

/**
 * Mock 3b (#211) — the pod-vs-local choice, made where the Run starts.
 *
 * The dialog exists **only when a choice exists**: a project with no local folder dispatches
 * exactly as before, and the caller never mounts this. Each radio card states its consequences
 * in plain words, the pod card is disabled with its reason on a LocalFolder project, and the
 * primary button repeats the choice — so there is never a surprise about where work executed.
 */
export function RunNowDialog({
  open,
  onOpenChange,
  vendorStoryId,
  automations,
  localPath,
  pending,
  error,
  onRun,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  vendorStoryId: string;
  automations: { id: string; triggerLabel: string }[];
  localPath: string;
  pending: boolean;
  error: unknown;
  onRun: (automationId: string, locus: Locus) => void;
}) {
  const [automationId, setAutomationId] = useState(automations[0]?.id ?? "");
  // A local-folder project defaults to Local — the pod physically cannot see the folder.
  const [locus, setLocus] = useState<Locus>("Local");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {t("runs.runNow.dialogTitle")} <span className="font-mono">#{vendorStoryId}</span>
          </DialogTitle>
          <DialogDescription>{t("runs.runNow.dialogHint")}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium">{t("runs.runNow.automation")}</span>
          <NativeSelect
            value={automationId}
            onChange={(event) => setAutomationId(event.target.value)}
            aria-label={t("runs.runNow.pickAutomation")}
          >
            {automations.map((automation) => (
              <option key={automation.id} value={automation.id}>
                {automation.triggerLabel}
              </option>
            ))}
          </NativeSelect>
        </div>

        <div
          className="flex flex-col gap-2"
          role="radiogroup"
          aria-label={t("runs.runNow.whereItExecutes")}
        >
          <span className="text-sm font-medium">{t("runs.runNow.whereItExecutes")}</span>
          <LocusCard
            selected={locus === "Local"}
            onSelect={() => setLocus("Local")}
            title={t("runs.locus.local.title")}
            description={`${t("runs.locus.local.description")} ${localPath}`}
          />
          {/* Disabled with its reason rather than hidden: the reader learns why the other
              option is unavailable, which is the constraint the settings callout states. */}
          <LocusCard
            selected={false}
            disabled
            onSelect={() => undefined}
            title={t("runs.locus.pod.title")}
            description={t("runs.locus.pod.unavailable")}
          />
        </div>

        {/* The refusal renders where the gesture happened (spec): BR-001's conflict, BR-013's
            rules, and the clean-tree refusal all land here, announced politely. */}
        {error ? (
          <p className="text-destructive text-sm" role="alert" aria-live="polite">
            {(error instanceof ApiError && error.detail) || t("runs.runNow.failed")}
          </p>
        ) : null}

        <DialogFooter>
          <Button variant="outline" type="button" onClick={() => onOpenChange(false)}>
            {t("connector.cancel")}
          </Button>
          <Button
            type="button"
            disabled={pending || !automationId}
            onClick={() => onRun(automationId, locus)}
          >
            {pending
              ? t("runs.runNow.pending")
              : locus === "Local"
                ? t("runs.runNow.confirmLocal")
                : t("runs.runNow.confirmPod")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** A radio card: ≥48px, its consequences part of the accessible description (spec). */
function LocusCard({
  selected,
  disabled = false,
  onSelect,
  title,
  description,
}: {
  selected: boolean;
  disabled?: boolean;
  onSelect: () => void;
  title: string;
  description: string;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      aria-disabled={disabled}
      disabled={disabled}
      onClick={onSelect}
      className={[
        "flex min-h-12 flex-col items-start gap-1 rounded-lg border p-3 text-left",
        selected ? "border-primary bg-primary/5 border-2" : "border-border",
        disabled ? "opacity-60" : "hover:bg-muted/50",
      ].join(" ")}
    >
      <span className="text-sm font-semibold">{title}</span>
      <span className="text-muted-foreground text-xs leading-relaxed">{description}</span>
    </button>
  );
}
