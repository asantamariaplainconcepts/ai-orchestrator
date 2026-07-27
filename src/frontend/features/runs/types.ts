/**
 * Exactly the BR-014 subset the API records today — nothing invented client-side either.
 * Output link, logs and cost arrive with their producing features (#19, #25); until then the
 * table renders the design system's empty value for them.
 */
export interface RunView {
  id: string;
  vendorStoryId: string;
  automationId: string;
  state:
    "Queued" | "Planning" | "AwaitingApproval" | "Executing" | "Succeeded" | "Failed" | "Cancelled";
  createdAt: string;
  dispatchedAt: string | null;
  outputLink: string | null;
  plan: string | null;
  approvedAt: string | null;
  failureReason: string | null;
  /** Null means the runtime reported nothing (BR-011) — never the same as a zero cost. */
  inputTokens: number | null;
  outputTokens: number | null;
  costUsd: number | null;
}

export interface ProjectCost {
  totalCostUsd: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  reportedRuns: number;
  unknownRuns: number;
}
