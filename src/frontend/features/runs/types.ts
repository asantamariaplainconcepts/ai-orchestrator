/**
 * Exactly the BR-014 subset the API records today — nothing invented client-side either.
 * Output link, logs and cost arrive with their producing features (#19, #25); until then the
 * table renders the design system's empty value for them.
 */
export interface RunView {
  id: string;
  vendorStoryId: string;
  automationId: string;
  /** Mirrors the backend's RunState names. AwaitingInput (#78) was missing here, so the
   *  compiler believed a state the API has been sending since the conversational actions
   *  shipped could not occur. */
  state:
    | "Queued"
    | "Planning"
    | "AwaitingApproval"
    | "Executing"
    | "AwaitingInput"
    | "Succeeded"
    | "Failed"
    | "Cancelled";
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
  /** When a human decided this failure needs no re-run (#145); null until they do. */
  dismissedAt: string | null;
  /** Pod | Local (#210) — where this Run executes, fixed at creation. */
  locus: "Pod" | "Local";
  /** The host folder a Local run worked in; null for Pod runs. */
  workingFolder: string | null;
  /** The branch a Local run left behind — its output, where Pod runs carry a PR. */
  branchName: string | null;
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
