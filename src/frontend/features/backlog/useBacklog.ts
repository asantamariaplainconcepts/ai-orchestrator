import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
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

export interface DeploymentCapabilities {
  /** Whether a folder on this machine can be named as a code source (#210). */
  hasCodeSource: boolean;
  /** Whether this deployment composes a store that accepts writes — pasting needs one (#222). */
  canStoreSecret: boolean;
  /** Non-null exactly when it cannot: how to gain the ability, in the store's own words. */
  storeRemedy: string | null;
  /** Whether a folder on this machine is reachable from the executing process (#247). */
  canUseLocalFolder: boolean;
  /** The habitat's declared reason where it is not — shown where the choice would be offered. */
  localFolderReason: string | null;
}

/**
 * What this deployment can offer (#222). It replaces #211's inference — a deliberately invalid
 * validate-path whose 404 meant "no surface" — with an answer derived from the same habitat
 * question the modules ask.
 *
 * Never stale: a deployment's capabilities are fixed at its startup, so one read serves the
 * session.
 */
export function useDeploymentCapabilities() {
  return useQuery({
    queryKey: ["deployment-capabilities"] as const,
    staleTime: Infinity,
    queryFn: () => api.get<DeploymentCapabilities>("/api/capabilities"),
  });
}

/**
 * What a credential must be granted for the shape currently in the form (#226). Asked for the
 * *proposed* configuration rather than the stored one, because the question is answered while
 * somebody is still changing the fields that decide it — a local code source needs no push.
 */
export function useRequiredPermissions(projectId: string, codeSource: string) {
  return useQuery({
    queryKey: ["required-permissions", projectId, codeSource] as const,
    queryFn: () =>
      api.get<{ scopes: string[] }>(
        `/api/projects/${projectId}/connector/required-permissions?codeSource=${encodeURIComponent(codeSource)}`,
      ),
  });
}

/**
 * Whether a secret name resolves on this deployment (design review 5d) — existence only, the
 * value never travels. Asked through the same seam every real resolution uses, so this answer
 * and the poller's first read cannot disagree. A query, unlike the path check: the environment
 * changes on a restart, not under a keystroke, so a cached verdict per typed name is honest.
 */
export function useSecretResolves(projectId: string, name: string) {
  return useQuery({
    queryKey: ["secret-resolves", projectId, name] as const,
    enabled: name.trim().length > 0,
    queryFn: () =>
      api.get<{ resolves: boolean }>(
        `/api/projects/${projectId}/connector/secret-resolves?name=${encodeURIComponent(name.trim())}`,
      ),
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
