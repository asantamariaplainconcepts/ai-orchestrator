import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

/** One directory that holds prompt files, with the steps its names were recognised as. */
export interface PipelineCandidate {
  directory: string;
  files: string[];
  /** The triggers this directory can wire — the mapping is the file name (#229). */
  steps: string[];
  /** Files matching no step. Reported, never interpreted into a trigger nobody applies. */
  unmatched: string[];
}

export interface PipelineDiscovery {
  candidates: PipelineCandidate[];
  /** Where it looked. Present even when nothing was found — a bare empty list reads as broken. */
  searchedIn: string[];
  /** Why there is nothing to show, when that is the answer: no Connector, or a vendor refusal. */
  reason: string | null;
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
