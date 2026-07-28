import { useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Checkbox } from "@/shared/ui/checkbox";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import { WorkflowCanvas } from "./WorkflowCanvas";
import { AUTOMATION_ACTIONS, AGENT_RUNTIMES, EXECUTABLE_ACTIONS } from "./types";
import type { AgentRuntime, AutomationAction } from "./types";
import {
  useApplyAutomationDefaults,
  useAutomations,
  useCreateAutomation,
  useDeleteAutomation,
  useSetAutomationEnabled,
} from "./useAutomations";

const CANVAS_VIEW = "aio:automations-view";

function storedCanvasView(): "list" | "canvas" {
  try {
    return window.localStorage.getItem(CANVAS_VIEW) === "canvas" ? "canvas" : "list";
  } catch {
    return "list";
  }
}

/**
 * UC-005 on its own tab. Creation lives behind an explicit button (dashboard-tabs): a form that
 * is always open tells the reader this page is for configuring, which is false on every day but
 * the first.
 */
export function AutomationsSection({ projectId }: { projectId: string }) {
  const automations = useAutomations(projectId);
  // Enabling can be refused by BR-003's re-check; disabling never is (design D2).
  const setEnabled = useSetAutomationEnabled(projectId);
  const create = useCreateAutomation(projectId);
  const defaults = useApplyAutomationDefaults(projectId);
  const remove = useDeleteAutomation(projectId);

  const [creating, setCreating] = useState(false);
  // A genuine preference like the board's, remembered the same way and for the same reason:
  // nothing about the project decides whether a reader wants rows or a shape.
  const [view, setView] = useState<"list" | "canvas">(storedCanvasView);
  const [triggerLabel, setTriggerLabel] = useState("");
  const [triggerState, setTriggerState] = useState("");
  const [action, setAction] = useState<AutomationAction>("ImplementToPullRequest");
  const [runtime, setRuntime] = useState<AgentRuntime>("ClaudeCodeHeadless");
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [rubricPath, setRubricPath] = useState("");
  const [outputLabel, setOutputLabel] = useState("");

  // Only the grill converses with a rubric; that field would be noise on every other action.
  const isGrill = action === "GrillToReady";

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!triggerLabel.trim()) return;

    create.mutate(
      {
        triggerLabel: triggerLabel.trim(),
        // Empty means "any state" — an unconstrained trigger, not an empty string to match.
        triggerState: triggerState.trim() === "" ? null : triggerState.trim(),
        action,
        runtime,
        requiresApproval,
        timeoutMinutes: null,
        rubricPath: isGrill && rubricPath.trim() ? rubricPath.trim() : null,
        outputLabel: outputLabel.trim() ? outputLabel.trim() : null,
      },
      {
        onSuccess: () => {
          setTriggerLabel("");
          setCreating(false);
        },
      },
    );
  }

  const rows = automations.data ?? [];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-base font-semibold">{t("automations.heading")}</h2>
          {automations.data ? (
            <Badge variant="secondary">
              {tCount(rows.length, "automations.count.one", "automations.count.other")}
            </Badge>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            type="button"
            onClick={() => defaults.mutate()}
            disabled={defaults.isPending}
            title={t("automations.defaults.hint")}
          >
            {defaults.isPending ? t("automations.defaults.applying") : t("automations.defaults")}
          </Button>
          <Button
            variant="outline"
            type="button"
            aria-pressed={view === "canvas"}
            onClick={() => {
              const next = view === "canvas" ? "list" : "canvas";
              setView(next);
              try {
                window.localStorage.setItem(CANVAS_VIEW, next);
              } catch {
                // A refused write costs the preference, never the interaction.
              }
            }}
          >
            {view === "canvas" ? t("canvas.showList") : t("canvas.show")}
          </Button>
          <Button type="button" onClick={() => setCreating((open) => !open)}>
            {creating ? t("automations.new.close") : t("automations.new")}
          </Button>
        </div>
      </div>

      {/* The refusal carries the rule, so it gets its own line rather than a generic error. */}
      {remove.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.delete.refused")}
        </p>
      )}
      {/* Enabling's refusal is its own outcome — previously nested inside the create error, so
          it only ever appeared when creation had failed too. */}
      {setEnabled.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.enableFailed")}
        </p>
      )}
      {defaults.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.defaults.failed")}
        </p>
      )}
      {/* Partial success is the normal outcome, so the result is reported rather than reduced
          to success or failure (design D2). */}
      {defaults.data ? (
        <p className="text-xs text-muted-foreground">
          {defaults.data.created.length > 0
            ? `${defaults.data.created.length} ${t("automations.defaults.created")}`
            : t("automations.defaults.nothingNew")}
          {defaults.data.skipped.length > 0
            ? ` · ${defaults.data.skipped.length} ${t("automations.defaults.skipped")}`
            : ""}
          {/* A label that never reached the vendor is not selectable there, which is the whole
              point of the action — so it is said, not implied. */}
          {defaults.data.labelNote ? ` · ${t("automations.defaults.labels")}` : ""}
        </p>
      ) : null}

      {creating && (
        <Card>
          <CardContent>
            <form className="flex flex-col gap-4" onSubmit={submit}>
              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="trigger-label">{t("automations.trigger")}</Label>
                  <Input
                    id="trigger-label"
                    value={triggerLabel}
                    onChange={(event) => setTriggerLabel(event.target.value)}
                    placeholder={t("automations.triggerPlaceholder")}
                  />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="trigger-state">{t("automations.state")}</Label>
                  <Input
                    id="trigger-state"
                    value={triggerState}
                    onChange={(event) => setTriggerState(event.target.value)}
                    placeholder={t("automations.statePlaceholder")}
                  />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="action">{t("automations.action")}</Label>
                  <NativeSelect
                    id="action"
                    value={action}
                    onChange={(event) => setAction(event.target.value as AutomationAction)}
                  >
                    {AUTOMATION_ACTIONS.map((candidate) => (
                      <option key={candidate} value={candidate}>
                        {candidate}
                        {EXECUTABLE_ACTIONS.includes(candidate)
                          ? ""
                          : ` — ${t("automations.actionNotExecutable")}`}
                      </option>
                    ))}
                  </NativeSelect>
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="runtime">{t("automations.runtime")}</Label>
                  <NativeSelect
                    id="runtime"
                    value={runtime}
                    onChange={(event) => setRuntime(event.target.value as AgentRuntime)}
                  >
                    {AGENT_RUNTIMES.map((candidate) => (
                      <option key={candidate} value={candidate}>
                        {candidate}
                      </option>
                    ))}
                  </NativeSelect>
                </div>
                {/* Only the grill converses with a rubric; the field would be noise elsewhere.
                    The output label is every action's, since #115 — chaining is a property of
                    the model now, not of the grill. */}
                {isGrill ? (
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="rubric-path">{t("automations.rubricPath")}</Label>
                    <Input
                      id="rubric-path"
                      value={rubricPath}
                      onChange={(event) => setRubricPath(event.target.value)}
                      placeholder={t("automations.rubricPathPlaceholder")}
                    />
                  </div>
                ) : null}
                <div className="flex flex-col gap-2">
                  <Label htmlFor="output-label">{t("automations.outputLabel")}</Label>
                  <Input
                    id="output-label"
                    value={outputLabel}
                    onChange={(event) => setOutputLabel(event.target.value)}
                    placeholder={t("automations.outputLabelPlaceholder")}
                  />
                </div>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <Checkbox
                    id="requires-approval"
                    checked={requiresApproval}
                    onCheckedChange={(checked) => setRequiresApproval(checked === true)}
                  />
                  <Label htmlFor="requires-approval">{t("automations.approval")}</Label>
                </div>
                <Button type="submit" disabled={create.isPending}>
                  {create.isPending ? t("automations.adding") : t("automations.add")}
                </Button>
              </div>

              <p className="text-xs text-muted-foreground">{t("automations.catalogueHint")}</p>

              {create.isError && (
                <p className="text-sm text-destructive" role="alert">
                  {t("automations.saveFailed")}
                </p>
              )}
            </form>
          </CardContent>
        </Card>
      )}

      {automations.isPending && (
        <p className="text-sm text-muted-foreground">{t("automations.loading")}</p>
      )}
      {automations.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.error")}
        </p>
      )}
      {automations.data && rows.length === 0 && (
        <p className="text-sm text-muted-foreground">{t("automations.empty")}</p>
      )}

      {rows.length > 0 && view === "canvas" && (
        <WorkflowCanvas projectId={projectId} automations={rows} />
      )}

      {rows.length > 0 && view === "list" && (
        <Card>
          <CardContent>
            <ul className="divide-y">
              {rows.map((automation) => (
                <li
                  key={automation.id}
                  className="flex flex-col gap-2 py-3 first:pt-0 last:pb-0 md:flex-row md:items-center md:justify-between"
                >
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <Badge variant="secondary">{automation.triggerLabel}</Badge>
                    {automation.triggerState ? (
                      <Badge className="bg-info text-info-foreground">
                        {automation.triggerState}
                      </Badge>
                    ) : (
                      <span className="text-xs text-muted-foreground">
                        {t("automations.anyState")}
                      </span>
                    )}
                    <span className="truncate text-sm font-medium">{automation.action}</span>
                    {EXECUTABLE_ACTIONS.includes(automation.action) ? null : (
                      <Badge className="bg-warning text-warning-foreground">
                        {t("automations.actionNotExecutable")}
                      </Badge>
                    )}
                    {automation.requiresApproval ? (
                      <Badge className="bg-info text-info-foreground">
                        {t("automations.approvalRequired")}
                      </Badge>
                    ) : null}
                    {automation.enabled ? null : (
                      <Badge variant="outline">{t("automations.disabled")}</Badge>
                    )}
                  </div>

                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <span className="text-xs text-muted-foreground">
                      {automation.timeoutMinutes} {t("automations.minutes")}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      type="button"
                      disabled={setEnabled.isPending}
                      onClick={() =>
                        setEnabled.mutate({ id: automation.id, enabled: !automation.enabled })
                      }
                    >
                      {automation.enabled ? t("automations.disable") : t("automations.enable")}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      type="button"
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(automation.id)}
                      title={t("automations.delete.hint")}
                    >
                      {t("automations.delete")}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
