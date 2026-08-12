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
  /** Null means the Project default, resolved at execution time (#244). */
  runtime: AgentRuntime | null;
  timeoutMinutes: number;
  enabled: boolean;
  /**
   * The **marks** this Automation applies when it succeeds (#310). Not the flow: since the
   * transition/mark split, a member of this set names no stage and draws no boundary — it is a
   * label the vendor carries for somebody else to read.
   */
  outputLabels: string[];
  /**
   * The to-stage of the one transition this Automation claims (#310); null claims none — it acts, it
   * may mark the Story, and the flow ends there. The from-stage is {@link Automation.triggerLabel},
   * so there is no second field for it, and there is no second transition either (AC 13).
   */
  toStage: string | null;
  /** Grill only: where the readiness document lives. Null means the framework's convention. */
  promptPath: string | null;
  /**
   * The model this Automation's Runs think with (#291). Null inherits the deployment's, resolved
   * at execution time — every Automation until an Admin chooses one.
   */
  model: string | null;
}

export interface CreateAutomationRequest {
  triggerLabel: string;
  triggerState: string | null;
  action: AutomationAction;
  runtime: AgentRuntime | null;
  timeoutMinutes: number | null;
  /** Grill only: where the readiness document lives. Null means the framework's convention. */
  promptPath?: string | null;
  /** The marks applied to the Story when a Run of this Automation succeeds (#310). Marks only: the
   *  lifecycle move is {@link CreateAutomationRequest.toStage} and never a member of this set. */
  outputLabels?: string[];
  /** The chosen model; null inherits. Always sent, because an update replaces the whole
   *  Automation and a field the client cannot carry is a field every edit silently clears. */
  model?: string | null;
  /** The claimed transition's to-stage; null claims none (#310). Always sent, for exactly the
   *  reason `model` is — the PUT is wholesale, so a client that omits this clears the claim. */
  toStage?: string | null;
}
