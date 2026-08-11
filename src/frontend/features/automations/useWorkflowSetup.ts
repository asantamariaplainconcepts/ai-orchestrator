import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

/** One directory that holds prompt files, with the steps its names were recognised as. */
/** One row of what the button would create, computed at read time from the same listing (#233). */
export interface PlannedStep {
  trigger: string;
  /** The file it wires: an existing one, or the starter that would be installed. */
  promptFile: string;
  exists: boolean;
  gated: boolean;
  /**
   * Whether a starter can be written for this step **without** a consent. A gated step becomes
   * installable once its tier is consented to (#269), which is a decision this card makes — see
   * `tierId`.
   */
  installable: boolean;
  /**
   * The transition this step claims (#262, restated for #310): the stage a Story reaches when it
   * succeeds, or null for a step the flow ends at. Excluding the step that claims the transition into
   * another breaks a hand-off, which is what `handoffsBrokenBy` reports.
   */
  toStage: string | null;
  /** The tier this step came from, so toggling a consent adds and removes rows with no round trip. */
  tierId: string;
}

/** One starter tier as a consent decision (#269). */
export interface StarterTier {
  id: string;
  title: string;
  summary: string;
  /** What the tier assumes — and, since #269, the text of the consent. Null needs no consent. */
  requires: string | null;
  /** Repository-relative paths the consent would write, outside the prompt directory. */
  prerequisites: string[];
}

export interface PipelineCandidate {
  directory: string;
  files: string[];
  /** The triggers this directory can wire — the mapping is the file name (#229). */
  steps: string[];
  /** Files matching no step. Reported, never interpreted into a trigger nobody applies. */
  unmatched: string[];
  /** What pressing the button would create — before it is pressed. */
  plan: PlannedStep[];
}

export interface PipelineDiscovery {
  candidates: PipelineCandidate[];
  /** Where it looked. Present even when nothing was found — a bare empty list reads as broken. */
  searchedIn: string[];
  /** Why there is nothing to show, when that is the answer: no Connector, or a vendor refusal. */
  reason: string | null;
  /**
   * The catalogue's tiers (#269). Answered even where there is no Connector — a consent's content is
   * catalogue data, and reading what a press would write before connecting anything is ordinary.
   */
  tiers: StarterTier[];
}

export interface SkippedStep {
  trigger: string;
  reason: string;
}

export interface InstalledStarters {
  files: string[];
  pullRequestUrl: string | null;
  branch: string | null;
  /** Set when installing was asked for and refused — never silently dropped from the report. */
  failure: string | null;
  /**
   * Files written **outside** the prompt directory (#269). Its own fact, never folded into `files`:
   * an Admin who consented to prompts must see that process documents were written too.
   */
  prerequisites: string[];
  /** Prerequisite paths left alone because the repository already had them. */
  prerequisitesAlreadyPresent: string[];
}

export interface MissingPrompt {
  saveAs: string;
  resolvedPath: string | null;
}

export interface WorkflowSetupReport {
  directory: string;
  created: string[];
  skipped: SkippedStep[];
  foundNotWired: string[];
  installed: InstalledStarters | null;
  missingPrompts: MissingPrompt[];
  /** Steps the Admin excluded. Its own fact: "skipped" means the project already had it. */
  excluded: string[];
}

/**
 * #229 — what pipeline does this repository already have? A read, run on an explicit press rather
 * than on mount: it costs several vendor listings, and the answer is only interesting when
 * somebody is about to act on it.
 */
export function usePipelineDiscovery(projectId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["pipeline-discovery", projectId] as const,
    queryFn: () =>
      api.get<PipelineDiscovery>(`/api/projects/${projectId}/automations/discover-pipeline`),
    enabled,
  });
}

export interface WorkflowSetupInput {
  /** The directory the human confirmed. Omitted keeps whatever the Connector already says. */
  promptDirectory?: string;
  /** The second consent: writing to somebody's repository is its own decision. */
  installMissing: boolean;
  /**
   * The triggers still selected (#262). Omitted means every step — the API reads absent and empty
   * as different answers, so never send `[]` to mean "no preference".
   */
  steps?: string[];
  /**
   * The tiers consented to (#269). Omitted means **no tier** — the opposite default from `steps`
   * above, and deliberately so: a selection narrows a plan already on screen, while a consent
   * authorises writing files into the repository. Sending nothing must authorise nothing.
   */
  tiers?: string[];
}

/**
 * The confirmation half. Automations and starter presence both change, so both are refetched —
 * an install that opened a pull request has not changed presence yet, and the refetch is what
 * keeps the offer honest about that.
 */
export function useSetUpWorkflow(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: WorkflowSetupInput) =>
      api.post<WorkflowSetupReport>(
        `/api/projects/${projectId}/automations/set-up-defaults`,
        input,
      ),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ["automations", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["starter-prompts", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["pipeline-discovery", projectId] });
    },
  });
}
