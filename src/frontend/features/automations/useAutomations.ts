import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { Automation, CreateAutomationRequest } from "./types";

const automationsKey = (projectId: string) => ["automations", projectId] as const;

export function useAutomations(projectId: string) {
  return useQuery({
    queryKey: automationsKey(projectId),
    queryFn: () => api.get<Automation[]>(`/api/projects/${projectId}/automations`),
  });
}

export function useCreateAutomation(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateAutomationRequest) =>
      api.post<Automation>(`/api/projects/${projectId}/automations`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: automationsKey(projectId) }),
  });
}

export function useUpdateAutomation(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: CreateAutomationRequest }) =>
      api.put<Automation>(`/api/projects/${projectId}/automations/${id}`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: automationsKey(projectId) }),
  });
}

/** Enabling can be refused (BR-003 re-check); disabling never is. */
export function useSetAutomationEnabled(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      api.post<Automation>(
        `/api/projects/${projectId}/automations/${id}/${enabled ? "enable" : "disable"}`,
        {},
      ),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: automationsKey(projectId) }),
  });
}
