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
      locus,
      runtime,
      model,
    }: {
      vendorStoryId: string;
      automationId: string;
      /** #211: absent means the project's default — Local for a local-folder project. */
      locus?: "Local" | "Sandbox";
      /** #244: the human's choice for this Run only; absent means "as resolved". */
      runtime?: string;
      /** #291: the human's model for this Run only; absent means "as resolved". */
      model?: string;
    }) =>
      api.post<RunNowResult>(`/api/projects/${projectId}/runs`, {
        vendorStoryId,
        automationId,
        locus: locus ?? null,
        runtime: runtime ?? null,
        model: model ?? null,
      }),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: ["runs", projectId] }),
  });
}
