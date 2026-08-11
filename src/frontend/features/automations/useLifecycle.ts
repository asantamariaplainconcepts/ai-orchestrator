import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export interface ProjectLifecycle {
  /** The stages in the stored order — array position is the order (#310, ADR-0022, design D1). */
  stages: string[];
}

/**
 * The project's Story lifecycle, read from the aggregate that owns it (#310).
 *
 * This one query is what replaced six re-implementations of the same label walk. The board, the
 * read-only preview and the setup plan used to each derive the flow from output labels and trigger
 * labels, and they disagreed — the board invented an ordering rule for Automations outside the flow,
 * the canvas opened a row per branch, and neither folded case. Nothing derives an order now: the
 * owner stores it and serves it, so there is one answer and no way for two surfaces to hold two.
 *
 * An empty list is an ordinary answer, not a failure: a project whose Automations have claimed no
 * transition has no lifecycle yet, and seeding one is deliberately out of scope.
 */
export function useLifecycle(projectId: string) {
  return useQuery({
    queryKey: lifecycleKey(projectId),
    queryFn: () => api.get<ProjectLifecycle>(`/api/projects/${projectId}/lifecycle`),
  });
}

/** Shared with the Automation mutations: claiming a transition can create the stages it names. */
export const lifecycleKey = (projectId: string) => ["lifecycle", projectId] as const;
