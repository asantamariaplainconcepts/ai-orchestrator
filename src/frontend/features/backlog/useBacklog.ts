import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api } from "@/shared/http/client";
import type {
  BacklogView,
  ConfigureConnectorRequest,
  ConnectorTestView,
  PathValidation,
  StoryDetail,
  StoryDocumentContent,
  StoryDocuments,
} from "./types";

const backlogKey = (projectId: string) => ["backlog", projectId] as const;

export function useBacklog(projectId: string) {
  return useQuery({
    queryKey: backlogKey(projectId),
    queryFn: () => api.get<BacklogView>(`/api/projects/${projectId}/backlog`),
  });
}

export function useStory(projectId: string, vendorStoryId: string) {
  return useQuery({
    queryKey: ["story", projectId, vendorStoryId] as const,
    queryFn: () =>
      api.get<StoryDetail>(
        `/api/projects/${projectId}/backlog/stories/${encodeURIComponent(vendorStoryId)}`,
      ),
  });
}

export function useStoryDocuments(projectId: string, vendorStoryId: string) {
  return useQuery({
    queryKey: ["story-documents", projectId, vendorStoryId] as const,
    queryFn: () =>
      api.get<StoryDocuments>(
        `/api/projects/${projectId}/backlog/stories/${encodeURIComponent(vendorStoryId)}/documents`,
      ),
  });
}

export function useStoryDocumentContent(
  projectId: string,
  vendorStoryId: string,
  path: string | null,
) {
  return useQuery({
    queryKey: ["story-document", projectId, vendorStoryId, path] as const,
    // Live reads (design D3): there is no cache to invalidate because there is no cache.
    enabled: path !== null,
    queryFn: () =>
      api.get<StoryDocumentContent>(
        `/api/projects/${projectId}/backlog/stories/${encodeURIComponent(
          vendorStoryId,
        )}/documents/content?path=${encodeURIComponent(path!)}`,
      ),
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

/**
 * #132 — asks what the stored credential can do, on demand. Not a query with a key: it is a
 * question the Admin asks at a moment of their choosing, and caching an answer about a permission
 * that can be revoked at any time would be worse than not asking.
 */
export function useTestConnector(projectId: string) {
  return useMutation({
    mutationFn: () => api.get<ConnectorTestView>(`/api/projects/${projectId}/connector/test`),
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

/**
 * Whether this deployment offers the code-source surface at all (#210/#211, mock 3a). The spec's
 * rule is posture-shaped: a cloud deployment answers **404 for the whole surface**, so the probe
 * asks the surface itself — one deliberately-invalid validate call — and reads only the status.
 * 404 means absent (render nothing); any other answer, the 400 included, means the surface
 * exists. Probed once per project page (staleTime: the posture cannot change under a running
 * deployment).
 */
export function useCodeSourceSurface(projectId: string) {
  return useQuery({
    queryKey: ["code-source-surface", projectId] as const,
    staleTime: Infinity,
    queryFn: async () => {
      try {
        await api.post<unknown>(`/api/projects/${projectId}/connector/validate-path`, {
          path: "",
        });
        return { offered: true };
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return { offered: false };
        }
        return { offered: true };
      }
    },
  });
}

/**
 * #210 — the live path check the form runs on idle (mock 3a). A mutation rather than a query:
 * the answer is about the host's disk at this moment, and caching "clean working tree" would
 * happily contradict the dispatch-time refusal it exists to preview.
 */
export function useValidateLocalPath(projectId: string) {
  return useMutation({
    mutationFn: (path: string) =>
      api.post<PathValidation>(`/api/projects/${projectId}/connector/validate-path`, { path }),
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
