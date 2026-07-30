import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { ProjectRole } from "@/shared/identity/useCurrentPrincipal";

export interface RoleHolder {
  identityId: string;
  displayName: string;
  role: ProjectRole;
  grantedAt: string;
}

export interface RoleCandidate {
  identityId: string;
  displayName: string;
}

export interface ProjectRolesView {
  holders: RoleHolder[];
  /** People this deployment has seen who hold nothing here yet (UC-002, design D6). */
  candidates: RoleCandidate[];
  /** The bundles, from the server's enum — DEC-034 fixes them at two and this is not the copy of it. */
  bundles: ProjectRole[];
}

const rolesKey = (projectId: string) => ["project-roles", projectId] as const;

export function useProjectRoles(projectId: string, enabled = true) {
  return useQuery({
    queryKey: rolesKey(projectId),
    queryFn: () => api.get<ProjectRolesView>(`/api/projects/${projectId}/roles`),
    enabled,
  });
}

/** Grant or change — one intent, so one call (UC-002). */
export function useAssignProjectRole(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ identityId, role }: { identityId: string; role: ProjectRole }) =>
      api.put<{ identityId: string; role: ProjectRole }>(
        `/api/projects/${projectId}/roles/${encodeURIComponent(identityId)}`,
        { role },
      ),
    // "me" too: changing your own role changes what every screen may offer you, and the shell's
    // project count is read from it.
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: rolesKey(projectId) });
      void queryClient.invalidateQueries({ queryKey: ["me"] });
    },
  });
}

export function useRevokeProjectRole(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (identityId: string) =>
      api.delete<void>(`/api/projects/${projectId}/roles/${encodeURIComponent(identityId)}`),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: rolesKey(projectId) });
      void queryClient.invalidateQueries({ queryKey: ["me"] });
    },
  });
}
