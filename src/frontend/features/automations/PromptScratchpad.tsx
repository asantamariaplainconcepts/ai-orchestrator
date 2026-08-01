import { useState } from "react";
import { formatCost } from "@/features/runs/useRuns";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { Textarea } from "@/shared/ui/textarea";
import { useSendMessage, useStartConversation, type Conversation } from "./usePromptScratchpad";

/**
 * #189 — try a prompt before committing it.
 *
 * It sits on the Automations tab because that is where prompt writing now happens (#162): the field
 * beside it names a file in the repository, and this is how you find out what that file will do
 * before there is a file.
 *
 * Each attempt is a **new** conversation (design D1). Reusing one would hand the agent the previous
 * draft and its own reply, and a trial contaminated by the draft it replaced predicts nothing.
 */
export function PromptScratchpad({ projectId }: { projectId: string }) {
  const [prompt, setPrompt] = useState("");
  const [subject, setSubject] = useState("");
  const [attempt, setAttempt] = useState<Conversation | null>(null);

  const start = useStartConversation(projectId);
  const send = useSendMessage(projectId);

  const running = start.isPending || send.isPending;

  // The refusal's own sentence, not "something went wrong" — here the useful one is usually "this
  // project has no Connector", which is the only thing the reader can act on.
  const failure = [start.error, send.error].find(
    (error): error is ApiError => error instanceof ApiError,
  );

  const reply = attempt?.messages.find((message) => message.role === "Agent");

  async function run() {
    const started = await start.mutateAsync(subject.trim() || null);
    const finished = await send.mutateAsync({
      conversationId: started.id,
      body: prompt,
    });
    setAttempt(finished);
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("scratchpad.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("scratchpad.explainer")}</p>
        </div>

        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            if (!prompt.trim() || running) return;
            void run();
          }}
        >
          <div className="flex flex-col gap-1">
            <Label htmlFor="scratchpad-prompt">{t("scratchpad.prompt")}</Label>
            <Textarea
              id="scratchpad-prompt"
              value={prompt}
              onChange={(event) => setPrompt(event.target.value)}
              placeholder={t("scratchpad.promptPlaceholder")}
            />
          </div>

          <div className="flex flex-wrap items-end gap-2">
            <div className="flex min-w-48 flex-1 flex-col gap-1">
              {/* Optional, and the label says so: a prompt tried against the project alone is an
                  ordinary case, not a field somebody forgot. */}
              <Label htmlFor="scratchpad-subject">{t("scratchpad.subject")}</Label>
              <Input
                id="scratchpad-subject"
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                placeholder={t("scratchpad.subjectPlaceholder")}
              />
            </div>
            <Button type="submit" disabled={!prompt.trim() || running}>
              {running ? t("scratchpad.running") : t("scratchpad.run")}
            </Button>
          </div>
        </form>

        {/* Said where the text is, not in a tooltip: nothing here is saved, and the only place a
            prompt lives is the repository (#150, #162). */}
        <p className="text-sm text-muted-foreground">{t("scratchpad.notSaved")}</p>

        {/* What a trial cannot reproduce, stated rather than discovered from a divergent result
            (design D4). */}
        <p className="text-xs text-muted-foreground">{t("scratchpad.limits")}</p>

        {failure ? (
          <p className="text-sm text-destructive">{failure.detail ?? t("scratchpad.failed")}</p>
        ) : null}

        {reply ? <Reply reply={reply} /> : null}
      </CardContent>
    </Card>
  );
}

/**
 * The answer and what it cost. A failed pass is shown as the answer it is — the scratchpad stays
 * usable and takes another attempt, which is the conversation's own rule (#166).
 */
function Reply({ reply }: { reply: Conversation["messages"][number] }) {
  return (
    <div className="flex flex-col gap-2 border-t border-border pt-4">
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold text-muted-foreground">
          {t("scratchpad.answer")}
        </span>
        {reply.failed ? <Badge variant="destructive">{t("scratchpad.passFailed")}</Badge> : null}
        {/* Unknown is not zero (BR-011) — a pass the runtime did not measure says so. */}
        <span className="text-xs text-muted-foreground">
          {reply.costUsd !== null ? formatCost(reply.costUsd) : t("scratchpad.costUnknown")}
        </span>
      </div>
      <p
        className={
          reply.failed
            ? "text-sm whitespace-pre-wrap text-destructive"
            : "text-sm whitespace-pre-wrap"
        }
      >
        {reply.body}
      </p>
    </div>
  );
}
