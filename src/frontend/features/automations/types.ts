/** Mirrors the API's enum *names* — never ordinals. */
export const AUTOMATION_ACTIONS = [
  "ImplementToPullRequest",
  "RefineOrComment",
  "TransitionState",
  "Estimate",
] as const;

export type AutomationAction = (typeof AUTOMATION_ACTIONS)[number];

/**
 * Which actions an Agent can actually perform today. The catalogue ships whole (DEC-026) but
 * only one action has an implementation, and an Automation that silently never runs is a trap —
 * so the interface marks the difference rather than hiding it (design D3).
 */
export const EXECUTABLE_ACTIONS: readonly AutomationAction[] = ["ImplementToPullRequest"];

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
