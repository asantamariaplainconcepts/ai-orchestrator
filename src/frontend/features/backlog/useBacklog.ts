import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { BacklogView, ConfigureConnectorRequest } from "./types";

const backlogKey = (projectId: string) => ["backlog", projectId] as const;

export function useBacklog(projectId: string) {
  return useQuery({
    queryKey: backlogKey(projectId),
    queryFn: () => api.get<BacklogView>(`/api/projects/${projectId}/backlog`),
  });
}

export function useConfigureConnector(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ConfigureConnectorRequest) =>
      api.put<unknown>(`/api/projects/${projectId}/connector`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: backlogKey(projectId) }),
  });
}

export function useWriteStoryLabel(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      vendorStoryId,
      label,
      apply,
    }: {
      vendorStoryId: string;
      label: string;
      apply: boolean;
    }) => {
      const url = `/api/projects/${projectId}/backlog/stories/${encodeURIComponent(
        vendorStoryId,
      )}/labels/${encodeURIComponent(label)}`;
      return apply ? api.put<unknown>(url, {}) : api.delete<unknown>(url);
    },
    // The write re-synchronises the mirror server-side, and a trigger label may have created a
    // Run by the time the response arrives — both views re-read.
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: backlogKey(projectId) });
      void queryClient.invalidateQueries({ queryKey: ["runs", projectId] });
    },
  });
}

export function useRefreshBacklog(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.post<unknown>(`/api/projects/${projectId}/backlog/refresh`, {}),
    // A failed refresh still changes what we know — the Connector now carries a failure — so the
    // backlog is re-read either way.
    onSettled: () => queryClient.invalidateQueries({ queryKey: backlogKey(projectId) }),
  });
}
