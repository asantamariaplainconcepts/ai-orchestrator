/** Mirrors the API's enum *names* — never ordinals. */
export const AUTOMATION_ACTIONS = [
  "ImplementToPullRequest",
  "RefineOrComment",
  "TransitionState",
  "Estimate",
] as const;

export type AutomationAction = (typeof AUTOMATION_ACTIONS)[number];

/**
 * Every catalogue action executes since #26–#28 — the list stays so a future action added to
 * the catalogue ahead of its implementation can be marked again, which is the situation this
 * existed for.
 */
export const EXECUTABLE_ACTIONS: readonly AutomationAction[] = [
  "ImplementToPullRequest",
  "RefineOrComment",
  "TransitionState",
  "Estimate",
];

export const AGENT_RUNTIMES = ["ClaudeCodeHeadless", "OpenCode"] as const;

export type AgentRuntime = (typeof AGENT_RUNTIMES)[number];

export interface Automation {
  id: string;
  triggerLabel: string;
  /** null means "any state" — the trigger places no state constraint. */
  triggerState: string | null;
  action: AutomationAction;
  runtime: AgentRuntime;
  requiresApproval: boolean;
  timeoutMinutes: number;
  enabled: boolean;
}

export interface CreateAutomationRequest {
  triggerLabel: string;
  triggerState: string | null;
  action: AutomationAction;
  runtime: AgentRuntime;
  requiresApproval: boolean;
  timeoutMinutes: number | null;
}
