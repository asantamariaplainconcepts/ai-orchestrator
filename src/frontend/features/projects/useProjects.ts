import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { projectsApi } from "./api";
import type { CreateProjectRequest } from "./types";

const projectsKey = ["projects"] as const;

export function useProjects(includeArchived = false) {
  return useQuery({
    queryKey: [...projectsKey, includeArchived] as const,
    queryFn: () => projectsApi.list(includeArchived),
  });
}

/** Retiring a project (#121): stops its work, keeps its history. */
export function useArchiveProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, confirmName }: { projectId: string; confirmName: string }) =>
      projectsApi.archive(projectId, confirmName),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: projectsKey }),
  });
}

export function useRestoreProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (projectId: string) => projectsApi.restore(projectId),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: projectsKey }),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateProjectRequest) => projectsApi.create(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectsKey }),
  });
}
