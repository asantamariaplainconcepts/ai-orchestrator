import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { ModelChoice } from "./ModelChoice";
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

export type Locus = "Local" | "Sandbox";

/**
 * Mock 3b (#211) — the sandbox-vs-local choice, made where the Run starts.
 *
 * The dialog exists **only when a choice exists**: a project with no local folder dispatches
 * exactly as before, and the caller never mounts this. Each radio card states its consequences
 * in plain words, the sandbox card is disabled with its reason on a LocalFolder project, and the
 * primary button repeats the choice — so there is never a surprise about where work executed.
 */
const RUNTIMES = ["ClaudeCodeHeadless", "OpenCode"] as const;

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
  automations: {
    id: string;
    triggerLabel: string;
    runtime: string | null;
    model: string | null;
  }[];
  /** Null for a repository project: no locus choice exists, only the runtime one (#244). */
  localPath: string | null;
  pending: boolean;
  error: unknown;
  onRun: (
    automationId: string,
    locus: Locus | null,
    runtime: string | null,
    model: string | null,
  ) => void;
}) {
  const [automationId, setAutomationId] = useState(automations[0]?.id ?? "");
  // A local-folder project defaults to Local — a sandbox physically cannot see the folder.
  const [locus, setLocus] = useState<Locus>("Local");
  // The human's choice for this Run only (#244, AC3). Empty means "as resolved": the
  // Automation's explicit runtime or the Project default, decided at execution time.
  const [runtime, setRuntime] = useState("");
  // The same "for this Run only" rule the runtime follows (#291): empty means the resolution.
  const [model, setModel] = useState("");

  const chosen = automations.find((automation) => automation.id === automationId);
  // Pre-selection is the resolution (AC3): an explicit Automation runtime shows selected;
  // absent one, the "Project default" option is the honest pre-selection — the default itself
  // is resolved at execution time, which is what makes changing it later actually work.
  const resolved = runtime || (chosen?.runtime ?? "");
  const resolvedModel = model || (chosen?.model ?? "");

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

        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium">{t("automations.runtime")}</span>
          <NativeSelect
            value={resolved}
            onChange={(event) => setRuntime(event.target.value)}
            aria-label={t("automations.runtime")}
          >
            <option value="">{t("runs.runNow.projectDefaultRuntime")}</option>
            {RUNTIMES.map((candidate) => (
              <option key={candidate} value={candidate}>
                {candidate}
              </option>
            ))}
          </NativeSelect>
        </div>

        <ModelChoice runtime={resolved} value={resolvedModel} onChange={setModel} enabled={open} />

        {localPath !== null ? (
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
              title={t("runs.locus.sandbox.title")}
              description={t("runs.locus.sandbox.unavailable")}
            />
          </div>
        ) : null}

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
            onClick={() =>
              onRun(
                automationId,
                localPath !== null ? locus : null,
                resolved || null,
                resolvedModel || null,
              )
            }
          >
            {pending
              ? t("runs.runNow.pending")
              : localPath === null
                ? t("runs.runNow.confirm")
                : locus === "Local"
                  ? t("runs.runNow.confirmLocal")
                  : t("runs.runNow.confirmSandbox")}
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
