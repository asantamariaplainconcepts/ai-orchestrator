import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/shared/http/client";

export interface ConversationMessage {
  id: string;
  role: "Person" | "Agent";
  body: string;
  createdAt: string;
  /** A pass that failed. The message is still part of the exchange; the conversation is open. */
  failed: boolean;
  inputTokens: number | null;
  outputTokens: number | null;
  /** Null means the runtime reported nothing — unknown, never zero (BR-011). */
  costUsd: number | null;
}

export interface Conversation {
  id: string;
  projectId: string;
  vendorStoryId: string | null;
  startedAt: string;
  lastActivityAt: string;
  spendUsd: number;
  /** False when some pass went unmeasured, so the total is a floor rather than a fact. */
  spendIsComplete: boolean;
  messages: ConversationMessage[];
}

const key = (projectId: string, conversationId: string) =>
  ["conversation", projectId, conversationId] as const;

export function useConversation(projectId: string, conversationId: string | null) {
  return useQuery({
    queryKey: key(projectId, conversationId ?? "none"),
    queryFn: () =>
      api.get<Conversation>(`/api/projects/${projectId}/conversations/${conversationId}`),
    enabled: Boolean(conversationId),
  });
}

export function useStartConversation(projectId: string) {
  return useMutation({
    mutationFn: (vendorStoryId: string | null) =>
      api.post<Conversation>(`/api/projects/${projectId}/conversations`, { vendorStoryId }),
  });
}

/**
 * Sending returns the whole exchange, so the reply lands without a second read. That matters more
 * than it looks: a pass takes seconds, and a refetch racing it would show the question with no
 * answer and then blink.
 */
export function useSendMessage(projectId: string, conversationId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: string) =>
      api.post<Conversation>(
        `/api/projects/${projectId}/conversations/${conversationId}/messages`,
        { body },
      ),
    onSuccess: (conversation) =>
      queryClient.setQueryData(key(projectId, conversation.id), conversation),
  });
}
