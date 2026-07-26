import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export interface RunNowResult {
  id: string;
  vendorStoryId: string;
  state: string;
  dispatched: boolean;
  waitingAtCap: boolean;
}

export function useRunNow(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      vendorStoryId,
      automationId,
    }: {
      vendorStoryId: string;
      automationId: string;
    }) =>
      api.post<RunNowResult>(`/api/projects/${projectId}/runs`, { vendorStoryId, automationId }),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}
