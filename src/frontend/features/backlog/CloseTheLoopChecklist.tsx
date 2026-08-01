import { Check } from "lucide-react";
import { useAutomations } from "@/features/automations/useAutomations";
import { useRuns } from "@/features/runs/useRuns";
import { t, type TranslationKey } from "@/shared/i18n";
import { Card, CardContent } from "@/shared/ui/card";
import type { ConnectorView } from "./types";

/**
 * Mock 3d (#211) — three steps from an empty project to a Run somebody watched.
 *
 * Every state is **derived** from data these screens already fetch (connector, automations,
 * runs); nothing about progress is stored, so the checklist can never disagree with reality.
 * It disappears permanently once the loop has closed once — any terminal Run.
 */
export function CloseTheLoopChecklist({
  projectId,
  connector,
  onConfigure,
  onAutomations,
}: {
  projectId: string;
  connector: ConnectorView | null;
  onConfigure: () => void;
  onAutomations: () => void;
}) {
  const automations = useAutomations(projectId);
  const runs = useRuns(projectId, null);

  // Wait for the reads rather than claiming "step 1 pending" over a loading spinner.
  if (!automations.data || !runs.data) return null;

  const closed = runs.data.some((run) => ["Succeeded", "Failed", "Cancelled"].includes(run.state));
  if (closed) return null;

  const steps: { title: TranslationKey; done: boolean; action?: () => void }[] = [
    { title: "onboarding.step.connect", done: connector !== null, action: onConfigure },
    {
      title: "onboarding.step.code",
      // Repository counts: this is not a local-only checklist (spec).
      done: connector !== null,
      action: onConfigure,
    },
    {
      title: "onboarding.step.automations",
      done: automations.data.some((automation) => automation.enabled),
      action: onAutomations,
    },
  ];

  const current = steps.findIndex((step) => !step.done);

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("onboarding.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("onboarding.explainer")}</p>
        </div>

        <ol className="flex flex-col gap-2">
          {steps.map((step, index) => (
            <li
              key={step.title}
              className="flex items-center gap-3 rounded-md border border-border p-3"
              // The state is in the accessible name, not only in the disc (spec).
              aria-label={`${t(step.title)} — ${step.done ? t("onboarding.state.done") : index === current ? t("onboarding.state.current") : t("onboarding.state.later")}`}
            >
              <span
                aria-hidden="true"
                className={[
                  "flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold",
                  step.done
                    ? "bg-success text-success-foreground"
                    : index === current
                      ? "bg-primary text-primary-foreground"
                      : "border border-border text-muted-foreground",
                ].join(" ")}
              >
                {step.done ? <Check className="size-3.5" /> : index + 1}
              </span>
              <span className="flex-1 text-sm">{t(step.title)}</span>
              {index === current && step.action ? (
                <button
                  type="button"
                  onClick={step.action}
                  className="text-primary text-sm font-medium underline-offset-4 hover:underline"
                >
                  {t("onboarding.go")}
                </button>
              ) : null}
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}
