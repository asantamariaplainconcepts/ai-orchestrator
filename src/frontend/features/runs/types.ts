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

export interface PulseAutomation {
  automationId: string;
  triggerLabel: string;
  action: string;
  fired: number;
  failed: number;
}

/** #108 — the 7-day window, exactly as the API reports it. Null means "no data", never zero. */
export interface ProjectPulse {
  runsStarted: number;
  terminalRuns: number;
  successRate: number | null;
  knownCostUsd: number;
  reportedRuns: number;
  unknownCostRuns: number;
  meanQueueWaitSeconds: number | null;
  meanDurationSeconds: number | null;
  automations: PulseAutomation[];
  storiesTotal: number;
  storiesNeverRun: number;
  waiting: { approval: number; input: number; failure: number };
  oldestOpenQuestionSeconds: number | null;
}

export interface ProjectCost {
  totalCostUsd: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  reportedRuns: number;
  unknownRuns: number;
}
