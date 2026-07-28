import { UserRound } from "lucide-react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { NativeSelect } from "@/shared/ui/native-select";
import { EXECUTABLE_ACTIONS } from "./types";
import type { Automation, CreateAutomationRequest } from "./types";
import { useSetAutomationEnabled, useUpdateAutomation } from "./useAutomations";
import { buildChains } from "./workflowGraph";

/**
 * The pipeline as a shape (#116). Edges are label agreements — nothing about the picture is
 * stored, so the canvas cannot claim a chain that would not fire (design D1).
 * <p>
 * The human balloon hangs off a node's <b>output</b> rather than between two nodes, because an
 * absence has no two ends: "no output label" does not name a destination, so the gesture that
 * removes it must also choose one. On a node the same balloon is `requiresApproval` (design D2).
 * Dragging is sugar; every gesture here is an explicit control (design D3).
 * </p>
 */
export function WorkflowCanvas({
  projectId,
  automations,
}: {
  projectId: string;
  automations: Automation[];
}) {
  const update = useUpdateAutomation(projectId);
  const setEnabled = useSetAutomationEnabled(projectId);
  const chains = buildChains(automations);

  /**
   * Every canvas change is an ordinary Automation update (design D4), so BR-003's overlap check
   * and #115's self-trigger refusal apply unchanged. The whole Automation is resent because the
   * endpoint replaces it — including the fields this screen never shows, which is why the API
   * had to start returning them.
   */
  function change(automation: Automation, patch: Partial<CreateAutomationRequest>) {
    update.mutate({
      id: automation.id,
      request: {
        triggerLabel: automation.triggerLabel,
        triggerState: automation.triggerState,
        action: automation.action,
        runtime: automation.runtime,
        requiresApproval: automation.requiresApproval,
        timeoutMinutes: automation.timeoutMinutes,
        rubricPath: automation.rubricPath ?? null,
        outputLabel: automation.outputLabel ?? null,
        ...patch,
      },
    });
  }

  if (automations.length === 0) {
    return <p className="text-sm text-muted-foreground">{t("automations.empty")}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      {update.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("canvas.changeRefused")}
        </p>
      )}

      <p className="text-xs text-muted-foreground">{t("canvas.hint")}</p>

      {/* Chains read downward, so a long pipeline costs scrolling the page rather than a
          sideways scroll nobody finds on a phone. Chains sit beside each other where there is
          room and stack where there is not. */}
      <div className="flex flex-wrap items-start gap-6">
        {chains.map((chain) => (
          <div key={chain[0]?.automation.id} className="flex w-full flex-col md:w-60">
            {chain.map((node) => (
              <div key={node.automation.id} className="flex flex-col">
                <AutomationNode
                  automation={node.automation}
                  onToggleApproval={() =>
                    change(node.automation, {
                      requiresApproval: !node.automation.requiresApproval,
                    })
                  }
                  onToggleEnabled={() =>
                    setEnabled.mutate({
                      id: node.automation.id,
                      enabled: !node.automation.enabled,
                    })
                  }
                />
                <Connector
                  automation={node.automation}
                  connected={node.next !== null}
                  candidates={automations.filter(
                    (candidate) =>
                      candidate.id !== node.automation.id &&
                      candidate.triggerLabel !== node.automation.triggerLabel,
                  )}
                  onConnect={(triggerLabel) =>
                    change(node.automation, { outputLabel: triggerLabel })
                  }
                  onDisconnect={() => change(node.automation, { outputLabel: null })}
                />
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}

function AutomationNode({
  automation,
  onToggleApproval,
  onToggleEnabled,
}: {
  automation: Automation;
  onToggleApproval: () => void;
  onToggleEnabled: () => void;
}) {
  return (
    <Card className={cn("w-60", !automation.enabled && "opacity-60")}>
      <CardContent className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center gap-1.5">
          <Badge variant="secondary">{automation.triggerLabel}</Badge>
          {automation.triggerState ? (
            <Badge variant="outline">{automation.triggerState}</Badge>
          ) : null}
        </div>

        <span className="truncate text-sm font-medium">{automation.action}</span>
        <span className="truncate text-xs text-muted-foreground">
          {automation.runtime}
          {EXECUTABLE_ACTIONS.includes(automation.action)
            ? ""
            : ` · ${t("automations.actionNotExecutable")}`}
        </span>

        {/* The balloon, on a node: approval. Same word and same colour as the one on an edge,
            because to the reader they mean one thing — a person is required here. */}
        <Button
          variant={automation.requiresApproval ? "default" : "outline"}
          size="sm"
          type="button"
          onClick={onToggleApproval}
          className={cn(automation.requiresApproval && "bg-warning text-warning-foreground")}
        >
          <UserRound className="size-3.5" />
          {automation.requiresApproval ? t("canvas.approval.on") : t("canvas.approval.off")}
        </Button>

        <Button variant="ghost" size="sm" type="button" onClick={onToggleEnabled}>
          {automation.enabled ? t("automations.disable") : t("automations.enable")}
        </Button>
      </CardContent>
    </Card>
  );
}

/**
 * What happens after this Automation succeeds: a solid arm to the next one, or a dotted arm
 * ending in the human balloon. The control names the choice, so the picture is never the only
 * way to read it.
 */
function Connector({
  automation,
  connected,
  candidates,
  onConnect,
  onDisconnect,
}: {
  automation: Automation;
  connected: boolean;
  candidates: Automation[];
  onConnect: (triggerLabel: string) => void;
  onDisconnect: () => void;
}) {
  // An output label pointing at no Automation: the vendor will carry the label and nobody will
  // answer it. Said plainly rather than drawn as a chain that does not exist.
  const dangling = !connected && Boolean(automation.outputLabel);

  return (
    <div className="flex flex-col items-center gap-1.5 py-2">
      {/* The arm hangs from the node above: solid when the chain continues, dotted where a
          person has to carry it. Vertical so a chain reads downward and never scrolls sideways. */}
      <div
        aria-hidden="true"
        className={cn(
          "h-6 w-0 border-l-2",
          connected ? "border-solid border-primary" : "border-dashed border-warning",
        )}
      />

      {connected ? (
        <Button variant="ghost" size="sm" type="button" onClick={onDisconnect}>
          {t("canvas.disconnect")}
        </Button>
      ) : (
        <>
          <span className="flex items-center gap-1 text-xs font-medium text-warning">
            <UserRound className="size-3.5" />
            {t("canvas.human")}
          </span>

          <div aria-hidden="true" className="h-6 w-0 border-l-2 border-dashed border-warning" />

          {/* The line runs between one step and another, so the undecided next step is drawn as
              a step: an empty slot that names itself by being filled. Without it the dotted arm
              would hang into nothing, which reads as a fault rather than as a choice. */}
          <div className="flex w-full flex-col items-center gap-1.5 rounded-lg border-2 border-dashed border-warning/60 p-2">
            {dangling ? (
              <span className="text-center text-xs text-destructive">
                {t("canvas.dangling")} <span className="font-mono">{automation.outputLabel}</span>
              </span>
            ) : null}
            <NativeSelect
              className="h-8 text-xs"
              aria-label={t("canvas.handsTo")}
              value=""
              onChange={(event) => {
                if (event.target.value) onConnect(event.target.value);
              }}
            >
              <option value="">{t("canvas.handsTo")}</option>
              {candidates.map((candidate) => (
                <option key={candidate.id} value={candidate.triggerLabel}>
                  {candidate.triggerLabel}
                </option>
              ))}
            </NativeSelect>
          </div>
        </>
      )}
    </div>
  );
}
