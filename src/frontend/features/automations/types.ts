/** Mirrors the API's enum *names* — never ordinals. */
export const AUTOMATION_ACTIONS = [
  // One action since #162: an Automation runs the repository's own prompt, and what it does is the
  // prompt's business. The list survives as a list because grants will widen what a row carries.
  "RepositoryPrompt",
] as const;

export type AutomationAction = (typeof AUTOMATION_ACTIONS)[number];

/**
 * Every catalogue action executes since #26–#28 — the list stays so a future action added to
 * the catalogue ahead of its implementation can be marked again, which is the situation this
 * existed for.
 */
export const EXECUTABLE_ACTIONS: readonly AutomationAction[] = ["RepositoryPrompt"];

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
  /** What this Automation hands on when it succeeds (#115/#165); empty ends the chain here. */
  outputLabels: string[];
  /** Grill only: where the readiness document lives. Null means the framework's convention. */
  promptPath: string | null;
}

export interface CreateAutomationRequest {
  triggerLabel: string;
  triggerState: string | null;
  action: AutomationAction;
  runtime: AgentRuntime;
  requiresApproval: boolean;
  timeoutMinutes: number | null;
  /** Grill only: where the readiness document lives. Null means the framework's convention. */
  promptPath?: string | null;
  /** Applied to the Story when a Run of this Automation succeeds; empty ends the chain here.
   *  Was one label until #165 made it a set, so one Automation can hand on to more than one place. */
  outputLabels?: string[];
}
