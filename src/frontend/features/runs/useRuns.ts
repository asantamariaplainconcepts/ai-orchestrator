import { useQuery } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { RunView } from "./types";

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
