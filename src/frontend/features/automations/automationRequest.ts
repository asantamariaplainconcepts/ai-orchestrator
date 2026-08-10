import type { Automation, CreateAutomationRequest } from "./types";

/**
 * The whole Automation, as the endpoint needs it. Extracted because three callers now build it and
 * a field one of them forgets is a field that caller silently clears on every gesture — which is
 * exactly what happened to the model (#291) before this existed.
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
    requiresApproval: automation.requiresApproval,
    timeoutMinutes: automation.timeoutMinutes,
    promptPath: automation.promptPath ?? null,
    outputLabels: automation.outputLabels,
    model: automation.model,
    ...patch,
  };
}
