import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ApiError, api } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/button";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import { ResponsiveDialog } from "@/shared/ui/responsive-dialog";
import { Textarea } from "@/shared/ui/textarea";
import type { InboxChange } from "./types";

const RUNTIMES = ["ClaudeCodeHeadless", "OpenCode"] as const;

/**
 * run-on-a-pr — the launch. An instruction typed on the spot becomes a Run on the change's own
 * head branch; the server resolves what the number means (BR-008), so this dialog sends only the
 * text and the runtime choice. Refusals render in place — the concurrency rule and an unreadable
 * repository are answers, not blanks.
 */
export function RunOnChangeDialog({
  change,
  onClose,
}: {
  change: InboxChange | null;
  onClose: () => void;
}) {
  const [instruction, setInstruction] = useState("");
  const [runtime, setRuntime] = useState<(typeof RUNTIMES)[number]>("ClaudeCodeHeadless");
  const queryClient = useQueryClient();

  const launch = useMutation({
    mutationFn: (target: InboxChange) =>
      api.post<{ id: string }>(`/api/projects/${target.projectId}/changes/${target.number}/runs`, {
        instruction: instruction.trim(),
        runtime,
      }),
    onSuccess: () => {
      // The run lists are what changed; the change list itself is the vendor's and stays put.
      void queryClient.invalidateQueries({ queryKey: ["runs"] });
      close();
    },
  });

  function close() {
    setInstruction("");
    launch.reset();
    onClose();
  }

  return (
    <ResponsiveDialog
      open={change !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
      title={
        <>
          {t("inbox.changes.runTitle")}{" "}
          <span className="font-mono text-primary">#{change?.number}</span>
        </>
      }
      footer={
        <>
          <span />
          <span className="flex items-center gap-2">
            <Button variant="outline" type="button" onClick={close}>
              {t("common.cancel")}
            </Button>
            <Button
              type="button"
              disabled={!instruction.trim() || launch.isPending || change === null}
              onClick={() => change && launch.mutate(change)}
            >
              {launch.isPending ? t("inbox.changes.launching") : t("inbox.changes.launch")}
            </Button>
          </span>
        </>
      }
    >
      <div className="flex flex-col gap-4 px-5 py-4">
        <p className="text-sm text-muted-foreground">{t("inbox.changes.runExplainer")}</p>

        <div className="flex flex-col gap-2">
          <Label htmlFor="change-instruction">{t("inbox.changes.instruction")}</Label>
          <Textarea
            id="change-instruction"
            value={instruction}
            onChange={(event) => setInstruction(event.target.value)}
            placeholder={t("inbox.changes.instructionPlaceholder")}
            rows={5}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label htmlFor="change-runtime">{t("automations.runtime")}</Label>
          <NativeSelect
            id="change-runtime"
            value={runtime}
            onChange={(event) => setRuntime(event.target.value as (typeof RUNTIMES)[number])}
          >
            {RUNTIMES.map((candidate) => (
              <option key={candidate} value={candidate}>
                {candidate}
              </option>
            ))}
          </NativeSelect>
        </div>

        {launch.isError ? (
          <p className="text-sm text-destructive" role="alert">
            {(launch.error instanceof ApiError && launch.error.detail) ||
              t("inbox.changes.launchFailed")}
          </p>
        ) : null}
      </div>
    </ResponsiveDialog>
  );
}
