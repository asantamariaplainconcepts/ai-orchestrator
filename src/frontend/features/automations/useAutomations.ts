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
