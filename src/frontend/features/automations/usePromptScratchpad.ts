import { useMutation } from "@tanstack/react-query";
import { api } from "@/shared/http/client";
import type { Conversation } from "@/features/conversations/useConversations";

export type { Conversation } from "@/features/conversations/useConversations";

/**
 * A scratchpad attempt is a conversation (#189, design D1), so it borrows the conversation's
 * endpoints rather than getting its own.
 *
 * Its own hooks, though, and deliberately: the conversation's `useSendMessage` is bound to one
 * conversation id and caches the exchange under it, because a conversation is a thing you come back
 * to. An attempt is not — each one starts a fresh conversation so the agent never sees the previous
 * draft — so caching it under a key nobody will read again would be storage with no reader.
 */
export function useStartConversation(projectId: string) {
  return useMutation({
    mutationFn: (vendorStoryId: string | null) =>
      api.post<Conversation>(`/api/projects/${projectId}/conversations`, { vendorStoryId }),
  });
}

export function useSendMessage(projectId: string) {
  return useMutation({
    mutationFn: ({ conversationId, body }: { conversationId: string; body: string }) =>
      api.post<Conversation>(
        `/api/projects/${projectId}/conversations/${conversationId}/messages`,
        { body },
      ),
  });
}
