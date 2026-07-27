import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { ProjectCost, RunView } from "./types";

const runsKey = (projectId: string, vendorStoryId: string | null) =>
  ["runs", projectId, vendorStoryId] as const;

export function useRuns(projectId: string, vendorStoryId: string | null) {
  return useQuery({
    queryKey: runsKey(projectId, vendorStoryId),
    queryFn: () =>
      api.get<RunView[]>(
        vendorStoryId
          ? `/api/projects/${projectId}/runs?vendorStoryId=${encodeURIComponent(vendorStoryId)}`
          : `/api/projects/${projectId}/runs`,
      ),
    // Runs are born asynchronously (poll → event → match), so a static snapshot would miss the
    // one thing this page exists to show. A slow poll is not streaming (DEC-031 — logs are
    // fetched, not streamed); it just keeps the list honest while the page is open.
    refetchInterval: 10_000,
  });
}

/**
 * UC-013 — the decision. Approve re-enqueues the Run for execution; reject ends it. Both
 * re-read the list, because either way the Run's state just changed.
 */
export function useDecideOnPlan(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ runId, approve }: { runId: string; approve: boolean }) =>
      api.post<unknown>(
        `/api/projects/${projectId}/runs/${runId}/${approve ? "approve" : "reject"}`,
        {},
      ),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}

export interface RunChangedFile {
  path: string;
  status: string;
  additions: number;
  deletions: number;
  patch: string | null;
  patchOmittedReason: "Binary" | "TooLarge" | null;
}

export interface RunChangesView {
  change: { number: number; url: string; files: RunChangedFile[] } | null;
}

/** UC-024 — read live at the change (BR-008), so there is nothing to invalidate. */
export function useRunChanges(projectId: string, runId: string) {
  return useQuery({
    queryKey: ["run-changes", projectId, runId] as const,
    queryFn: () => api.get<RunChangesView>(`/api/projects/${projectId}/runs/${runId}/changes`),
  });
}

/** UC-014 — stop a Run. Terminal Runs refuse, so the button hides once one finishes. */
export function useCancelRun(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (runId: string) =>
      api.post<unknown>(`/api/projects/${projectId}/runs/${runId}/cancel`, {}),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}

export function useProjectCost(projectId: string) {
  return useQuery({
    queryKey: ["project-cost", projectId] as const,
    queryFn: () => api.get<ProjectCost>(`/api/projects/${projectId}/runs/cost`),
    refetchInterval: 15_000,
  });
}

/**
 * Money as money, and unknown as unknown. Since #30's free models a cost of zero is a real
 * reported value, so null must never fall through to "0.00" (design D1).
 */
export function formatCost(cost: number | null): string | null {
  return cost === null
    ? null
    : new Intl.NumberFormat("en", {
        style: "currency",
        currency: "USD",
        minimumFractionDigits: 2,
        maximumFractionDigits: 4,
      }).format(cost);
}
