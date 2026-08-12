import type { Automation, CreateAutomationRequest } from "./types";

/**
 * The whole Automation, as the endpoint needs it. Extracted because three callers now build it and
 * a field one of them forgets is a field that caller silently clears on every gesture — which is
 * exactly what happened to the model (#291) before this existed.
 * <p>
 * The claimed transition (#310) is the second field that would have gone the same way, which is why
 * this builder is now the only way any surface writes an Automation. The board's own gesture
 * restated eight fields inline and omitted <c>model</c>, so pressing "require a person" on a column
 * header reverted a chosen model to the deployment's — and would have cleared the claim too.
 * </p>
 */
export function requestFor(
  automation: Automation,
  patch: Partial<CreateAutomationRequest>,
): CreateAutomationRequest {
  return {
    triggerLabel: automation.triggerLabel,
    triggerState: automation.triggerState,
    action: automation.action,
    runtime: automation.runtime,
    timeoutMinutes: automation.timeoutMinutes,
    promptPath: automation.promptPath ?? null,
    outputLabels: automation.outputLabels,
    model: automation.model,
    toStage: automation.toStage,
    ...patch,
  };
}
