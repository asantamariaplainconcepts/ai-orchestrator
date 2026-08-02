import { t } from "@/shared/i18n";
import type { AgentRuntime } from "./types";

/**
 * The form, restated as prose (#231, design D2).
 *
 * Nothing on this screen answered "is this what I meant" before Save — the consequence of a
 * configuration was only visible afterwards, on the canvas, which is a slow way to discover a
 * trigger you mistyped. This says it back in the canvas's own vocabulary, so the two surfaces agree.
 *
 * **Deliberately not a second validation channel.** An incomplete configuration produces an
 * incomplete sentence that names what is missing; it never refuses anything. The field-level
 * refusals already exist, and two voices for one problem is worse than one.
 */
export function AutomationSentence({
  triggerLabel,
  triggerState,
  promptPath,
  runtime,
  requiresApproval,
  handsOn,
  outputLabels,
}: {
  triggerLabel: string;
  triggerState: string;
  promptPath: string;
  runtime: AgentRuntime;
  requiresApproval: boolean;
  /** The answer to question three, which is not the same as having named a label yet. */
  handsOn: boolean;
  outputLabels: string[];
}) {
  const trigger = triggerLabel.trim();
  const state = triggerState.trim();
  const prompt = promptPath.trim();

  return (
    <p className="rounded-lg bg-muted px-3.5 py-2.5 text-xs leading-relaxed text-muted-foreground">
      {t("automations.sentence.prefix")}{" "}
      {trigger ? (
        <Token>{trigger}</Token>
      ) : (
        <Missing>{t("automations.sentence.missingTrigger")}</Missing>
      )}{" "}
      {state ? (
        <>
          {t("automations.sentence.inState")} <Token>{state}</Token>
        </>
      ) : (
        t("automations.sentence.anyState")
      )}
      , {requiresApproval ? `${t("automations.sentence.gated")} ` : ""}
      {t("automations.sentence.runs")}{" "}
      {prompt ? (
        <Token>{prompt}</Token>
      ) : (
        <Missing>{t("automations.sentence.missingPrompt")}</Missing>
      )}{" "}
      {t("automations.sentence.on")} <Token>{runtime}</Token>{" "}
      {handsOn ? (
        <>
          {t("automations.sentence.handsOn")}{" "}
          {outputLabels.length === 0 ? (
            <Missing>{t("automations.sentence.missingLabel")}</Missing>
          ) : null}
          {outputLabels.map((label, index) => (
            <span key={label}>
              {index > 0 ? ", " : ""}
              <Token>{label}</Token>
            </span>
          ))}
        </>
      ) : (
        t("automations.sentence.stops")
      )}
      .
    </p>
  );
}

function Token({ children }: { children: React.ReactNode }) {
  return <span className="font-mono font-semibold text-foreground">{children}</span>;
}

/** Named, not blank: "a trigger label is missing" is actionable, an empty gap is a puzzle. */
function Missing({ children }: { children: React.ReactNode }) {
  return <span className="italic">{children}</span>;
}
