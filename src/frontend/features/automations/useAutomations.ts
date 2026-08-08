import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { Automation, CreateAutomationRequest } from "./types";

const automationsKey = (projectId: string) => ["automations", projectId] as const;

export interface ProjectPrompts {
  directory: string;
  names: string[];
  /** Null when the listing worked; a sentence when discovery degraded (#215). */
  reason: string | null;
}

/**
 * The prompts that actually exist, for the picker (#215). Fetched only once the field is used
 * (`enabled`), read live server-side — degradation arrives as data (`reason`), never as an error,
 * so the form can fall back to the plain textbox without treating discovery as load-bearing.
 */
export function useProjectPrompts(projectId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["project-prompts", projectId] as const,
    queryFn: () => api.get<ProjectPrompts>(`/api/projects/${projectId}/prompts`),
    enabled,
  });
}

/**
 * What models a runtime offers, for the chooser (#291). Three states arrive as DATA, never as an
 * error, because they mean different things to the reader: enumerated from the machine that will
 * run it, declared by an operator, or "the machine could not be asked" — and rendering the last
 * as an empty list would say a runtime has no models when nobody managed to look.
 *
 * Fetched only once the field is in use, like the prompt picker: asking costs a whole sandbox
 * where agents are sandboxed.
 */
export interface AgentModels {
  runtimeName: string;
  models: string[];
  source: "enumerated" | "declared" | "couldNotAsk";
}

export function useAgentModels(runtime: string, enabled: boolean) {
  return useQuery({
    // Keyed by runtime, so switching it re-asks rather than showing the previous one's answer.
    queryKey: ["agent-models", runtime] as const,
    queryFn: () => api.get<AgentModels>(`/api/agent-runtimes/${runtime}/models`),
    enabled: enabled && runtime.length > 0,
  });
}

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

/** Safe to repeat: BR-003 refuses the overlaps, so a second press creates nothing. */

/** Refused when any Run used it — the message tells the Admin to disable instead (#84). */
export function useDeleteAutomation(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/projects/${projectId}/automations/${id}`),
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
