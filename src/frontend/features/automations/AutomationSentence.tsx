import { t } from "@/shared/i18n";
import type { AgentRuntime } from "./types";
import { HOLD_LABEL, fold } from "./workflowGraph";

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
  handsOn,
  toStage,
  marks,
}: {
  triggerLabel: string;
  triggerState: string;
  promptPath: string;
  /** "" means the Project default (#244) — named as such in the sentence. */
  runtime: AgentRuntime | "";
  /** The answer to question three, which is not the same as having named a stage yet. */
  handsOn: boolean;
  /** The claimed transition's to-stage (#310); "" while nobody has named one. */
  toStage: string;
  /** The marks — their own clause, because since #310 they are their own thing. */
  marks: string[];
}) {
  const trigger = triggerLabel.trim();
  const state = triggerState.trim();
  const prompt = promptPath.trim();
  // The hold is stored among the marks but said on its own — see the marks clause below.
  const held = marks.some((label) => fold(label) === HOLD_LABEL);
  const ordinaryMarks = marks.filter((label) => fold(label) !== HOLD_LABEL);

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
      , {t("automations.sentence.runs")}{" "}
      {prompt ? (
        <Token>{prompt}</Token>
      ) : (
        <Missing>{t("automations.sentence.missingPrompt")}</Missing>
      )}{" "}
      {t("automations.sentence.on")}{" "}
      <Token>{runtime === "" ? t("automations.runtimeProjectDefault") : runtime}</Token>{" "}
      {handsOn ? (
        <>
          {t("automations.sentence.handsOn")}{" "}
          {toStage.trim() ? (
            <Token>{toStage.trim()}</Token>
          ) : (
            <Missing>{t("automations.sentence.missingStage")}</Missing>
          )}
        </>
      ) : (
        t("automations.sentence.stops")
      )}
      {/* The marks, said as the separate thing they became (#310): the flow moves by the stage
          above, and these are labels the vendor carries for somebody else to read. The hold is
          lifted out of them (#321): it is stored as a mark, but it is the only one that changes
          what happens next, and burying "nothing runs until a person looks" in a list of labels
          would hide the most consequential thing this Automation does. */}
      {ordinaryMarks.length > 0 ? (
        <>
          {" "}
          {t("automations.sentence.marks")}{" "}
          {ordinaryMarks.map((label, index) => (
            <span key={label}>
              {index > 0 ? ", " : ""}
              <Token>{label}</Token>
            </span>
          ))}
        </>
      ) : null}
      {held ? <>, {t("automations.sentence.holds")}</> : null}.
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
