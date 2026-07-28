/** Mirrors the API's enum *names* — never ordinals. */
export const AUTOMATION_ACTIONS = [
  "ImplementToPullRequest",
  "RefineOrComment",
  "TransitionState",
  "Estimate",
  "GrillToReady",
  "ProposeSpec",
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
  "GrillToReady",
  "ProposeSpec",
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
  /** Grill only: where the readiness document lives. Null means the framework's convention. */
  rubricPath?: string | null;
  /** Grill only: the label applied when the bar is met. Null means the convention. */
  outputLabel?: string | null;
}

/** What applying the framework defaults did — partial success is the normal shape (design D2). */
export interface AutomationDefaultsResult {
  created: Automation[];
  skipped: { triggerLabel: string; reason: string }[];
  /** Null when every trigger label is present at the vendor; otherwise why it is not. */
  labelNote: string | null;
}
