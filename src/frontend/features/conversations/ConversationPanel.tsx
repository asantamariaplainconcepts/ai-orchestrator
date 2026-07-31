import { useState } from "react";
import { formatCost } from "@/features/runs/useRuns";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import {
  useConversation,
  useSendMessage,
  useStartConversation,
  type ConversationMessage,
} from "./useConversations";

/**
 * UC-027's reading surface, borrowed for a conversation (#166): ask an agent about this project, or
 * about one of its Stories.
 *
 * Deliberately not a Run surface. There is no state badge, no cancel, no queue position — a
 * conversation occupies nothing and blocks nothing, and showing it beside Runs with the same
 * furniture would teach the opposite.
 */
export function ConversationPanel({ projectId }: { projectId: string }) {
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [draft, setDraft] = useState("");

  const start = useStartConversation(projectId);
  const conversation = useConversation(projectId, conversationId);
  const send = useSendMessage(projectId, conversationId);

  const exchange = send.data ?? conversation.data;

  // A refusal carries its own sentence, and here the useful one is "this project has no Connector,
  // so there is no repository to ground an answer in" — replacing that with "something went wrong"
  // would throw away the only thing the reader can act on.
  const failure = [start.error, send.error, conversation.error].find(
    (error): error is ApiError => error instanceof ApiError,
  );

  if (!conversationId) {
    return (
      <Card>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <h2 className="text-base font-semibold">{t("conversation.title")}</h2>
            <p className="text-sm text-muted-foreground">{t("conversation.explainer")}</p>
          </div>

          <form
            className="flex flex-wrap items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              start.mutate(subject.trim() || null, {
                onSuccess: (started) => setConversationId(started.id),
              });
            }}
          >
            <div className="flex min-w-48 flex-1 flex-col gap-1">
              {/* Optional, and the label says so rather than the placeholder: a subject is an
                  ordinary absence, not a field somebody forgot. */}
              <Label htmlFor="conversation-subject">{t("conversation.subject")}</Label>
              <Input
                id="conversation-subject"
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                placeholder={t("conversation.subjectPlaceholder")}
              />
            </div>
            <Button type="submit" disabled={start.isPending}>
              {t("conversation.start")}
            </Button>
          </form>

          {failure ? (
            <p className="text-sm text-destructive">{failure.detail ?? t("conversation.failed")}</p>
          ) : null}
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="flex flex-col gap-1">
            <h2 className="text-base font-semibold">{t("conversation.title")}</h2>
            {exchange?.vendorStoryId ? (
              <p className="text-sm text-muted-foreground">
                {t("conversation.about")}{" "}
                <span className="font-mono">{exchange.vendorStoryId}</span>
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">{t("conversation.aboutProject")}</p>
            )}
          </div>
          <Spend spendUsd={exchange?.spendUsd ?? 0} complete={exchange?.spendIsComplete ?? true} />
        </div>

        <ul className="flex flex-col gap-3">
          {exchange?.messages.map((message) => (
            <Turn key={message.id} message={message} />
          ))}
        </ul>

        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (!draft.trim()) return;
            send.mutate(draft.trim(), { onSuccess: () => setDraft("") });
          }}
        >
          <div className="flex min-w-48 flex-1 flex-col gap-1">
            <Label htmlFor="conversation-message">{t("conversation.message")}</Label>
            <Input
              id="conversation-message"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder={t("conversation.messagePlaceholder")}
            />
          </div>
          <Button type="submit" disabled={!draft.trim() || send.isPending}>
            {send.isPending ? t("conversation.thinking") : t("conversation.send")}
          </Button>
        </form>

        {failure ? (
          <p className="text-sm text-destructive">{failure.detail ?? t("conversation.failed")}</p>
        ) : null}
      </CardContent>
    </Card>
  );
}

/**
 * What it has cost. When any pass went unmeasured the number is a floor and says so — a total that
 * looked exact and was not is precisely what BR-011 exists to prevent.
 */
function Spend({ spendUsd, complete }: { spendUsd: number; complete: boolean }) {
  const amount = formatCost(spendUsd) ?? "—";

  return (
    <span className="text-xs text-muted-foreground">
      {complete ? amount : `${t("conversation.atLeast")} ${amount}`}
    </span>
  );
}

function Turn({ message }: { message: ConversationMessage }) {
  const person = message.role === "Person";

  return (
    <li className={cn("flex flex-col gap-1", person ? "items-start" : "items-stretch")}>
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold text-muted-foreground">
          {person ? t("conversation.you") : t("conversation.agent")}
        </span>
        {/* A failure is shown on the message that caused it, not on the conversation: the
            conversation is still open and the next question still works. */}
        {message.failed ? (
          <Badge variant="destructive">{t("conversation.passFailed")}</Badge>
        ) : null}
        {message.costUsd !== null ? (
          <span className="text-xs text-muted-foreground">{formatCost(message.costUsd)}</span>
        ) : message.role === "Agent" ? (
          <span className="text-xs text-muted-foreground">{t("conversation.costUnknown")}</span>
        ) : null}
      </div>
      <p
        className={cn(
          "text-sm whitespace-pre-wrap",
          message.failed ? "text-destructive" : undefined,
        )}
      >
        {message.body}
      </p>
    </li>
  );
}
