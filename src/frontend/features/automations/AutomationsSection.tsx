import { X } from "lucide-react";
import { useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Checkbox } from "@/shared/ui/checkbox";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import { HumanStepBlock, WorkflowCanvas } from "./WorkflowCanvas";
import { ApiError } from "@/shared/http/client";
import { AUTOMATION_ACTIONS, AGENT_RUNTIMES, EXECUTABLE_ACTIONS } from "./types";
import type { AgentRuntime, Automation, AutomationAction } from "./types";
import {
  useAutomations,
  useCreateAutomation,
  useDeleteAutomation,
  useSetAutomationEnabled,
  useUpdateAutomation,
} from "./useAutomations";

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
  const remove = useDeleteAutomation(projectId);
  const update = useUpdateAutomation(projectId);

  const [creating, setCreating] = useState(false);
  // The form's mode (#151, design D1): null creates, an Automation edits. One form rather than two,
  // because "the edit form mirrors create's rules" is a property two components satisfy the day they
  // are written and stop satisfying without anyone noticing.
  const [editing, setEditing] = useState<Automation | null>(null);
  const [triggerLabel, setTriggerLabel] = useState("");
  const [triggerState, setTriggerState] = useState("");
  const [action, setAction] = useState<AutomationAction>("RepositoryPrompt");
  const [runtime, setRuntime] = useState<AgentRuntime>("ClaudeCodeHeadless");
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [promptPath, setPromptPath] = useState("");
  // A set since #165, plus the text currently being typed into the picker. Two pieces of state
  // because they are two things: what is chosen, and what is half-written.
  const [outputLabels, setOutputLabels] = useState<string[]>([]);
  const [outputDraft, setOutputDraft] = useState("");
  // Kept as text so blank can mean "BR-005's default" — a number input cannot hold that.
  const [timeoutMinutes, setTimeoutMinutes] = useState("");

  /** The chosen set plus whatever is still in the input, deduped the way the vendor compares. */
  function withDraft() {
    const draft = outputDraft.trim();
    if (!draft) return outputLabels;
    return outputLabels.some((label) => label.toLowerCase() === draft.toLowerCase())
      ? outputLabels
      : [...outputLabels, draft];
  }

  function addOutputLabel() {
    setOutputLabels(withDraft());
    setOutputDraft("");
  }

  /**
   * What the picker offers (#165, design D5): the trigger labels of this project's other **enabled**
   * Automations, because wiring the next step is what this field is most often for.
   *
   * Not this Automation's own trigger (#115 would refuse it), and not a disabled one — wiring an
   * edge into something switched off produces a hand-off that goes nowhere, which is exactly the
   * dangling state the canvas warns about. Free text stays possible: a label may be a mark that
   * triggers nothing, or a trigger that does not exist yet.
   */
  const outputSuggestions = (automations.data ?? [])
    .filter(
      (candidate) =>
        candidate.enabled &&
        candidate.id !== editing?.id &&
        candidate.triggerLabel.toLowerCase() !== triggerLabel.trim().toLowerCase() &&
        !outputLabels.some((label) => label.toLowerCase() === candidate.triggerLabel.toLowerCase()),
    )
    .map((candidate) => candidate.triggerLabel);

  /** Every field, blank, for a create that starts from nothing. */
  function reset() {
    setTriggerLabel("");
    setTriggerState("");
    setAction("RepositoryPrompt");
    setRuntime("ClaudeCodeHeadless");
    setRequiresApproval(false);
    setPromptPath("");
    setOutputLabels([]);
    setOutputDraft("");
    setTimeoutMinutes("");
  }

  /**
   * Seeds the form from what is stored. Every field, including the timeout — the endpoint is a full
   * replace, so a field this form did not carry would be replaced by the default for absent, and the
   * timeout is the one create never carried (design D2).
   */
  function openEdit(automation: Automation) {
    setEditing(automation);
    setCreating(true);
    setTriggerLabel(automation.triggerLabel);
    setTriggerState(automation.triggerState ?? "");
    setAction(automation.action);
    setRuntime(automation.runtime);
    setRequiresApproval(automation.requiresApproval);
    setPromptPath(automation.promptPath ?? "");
    setOutputLabels([...automation.outputLabels]);
    setOutputDraft("");
    setTimeoutMinutes(String(automation.timeoutMinutes));
  }

  function closeForm() {
    setCreating(false);
    setEditing(null);
    reset();
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!triggerLabel.trim()) return;

    const request = {
      triggerLabel: triggerLabel.trim(),
      // Empty means "any state" — an unconstrained trigger, not an empty string to match.
      triggerState: triggerState.trim() === "" ? null : triggerState.trim(),
      action,
      runtime,
      requiresApproval,
      // Blank is the default, not zero. Sending 0 would be a timeout of no time at all.
      timeoutMinutes: timeoutMinutes.trim() === "" ? null : Number(timeoutMinutes),
      // Required since #162: with one action, an Automation that names no prompt could never run,
      // and the server refuses it at save. Trimmed only — the empty case is the server's refusal to
      // give, not this form's to silently swallow.
      promptPath: promptPath.trim() ? promptPath.trim() : null,
      // The half-typed value counts: an Admin who typed a label and pressed Save meant it, and
      // silently dropping it is the kind of loss a form should never inflict.
      outputLabels: withDraft(),
    };

    if (editing) {
      update.mutate({ id: editing.id, request }, { onSuccess: closeForm });
      return;
    }

    create.mutate(request, { onSuccess: closeForm });
  }

  const saving = editing ? update.isPending : create.isPending;
  const saveError = editing ? update.error : create.error;
  const saveFailed = editing ? update.isError : create.isError;

  const rows = automations.data ?? [];

  // The catalogue: every Automation the project has, chained or not (#136, design D2). Held in a
  // value so the two sections below read as two things rather than as two branches of one
  // conditional — which is what they were, and what made them feel like one list.
  const catalogue = (
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
                  <Badge className="bg-info text-info-foreground">{automation.triggerState}</Badge>
                ) : (
                  <span className="text-xs text-muted-foreground">{t("automations.anyState")}</span>
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
                  onClick={() => openEdit(automation)}
                >
                  {t("automations.edit")}
                </Button>
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
  );

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
          <Button type="button" onClick={() => (creating ? closeForm() : setCreating(true))}>
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
                {/* The prompt is the Automation now (#162): what it does is this file's
                    business, so the field is required and always visible. A name, not a path —
                    the directory is the project's, on the Settings tab. */}
                <div className="flex flex-col gap-2">
                  <Label htmlFor="prompt-path">{t("automations.promptFile")}</Label>
                  <Input
                    id="prompt-path"
                    required
                    value={promptPath}
                    onChange={(event) => setPromptPath(event.target.value)}
                    placeholder={t("automations.promptFilePlaceholder")}
                  />
                  <p className="text-xs text-muted-foreground">{t("automations.promptFileHint")}</p>
                </div>
                {/* Visible in both modes (design D2): an edit resends this value, and a value
                    resent on somebody's behalf is one they are entitled to see. Blank is BR-005's
                    default, which is also why create never needed it until now. */}
                <div className="flex flex-col gap-2">
                  <Label htmlFor="timeout-minutes">{t("automations.timeout")}</Label>
                  <Input
                    id="timeout-minutes"
                    type="number"
                    min={1}
                    max={60}
                    value={timeoutMinutes}
                    onChange={(event) => setTimeoutMinutes(event.target.value)}
                    placeholder={t("automations.timeoutPlaceholder")}
                  />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="output-label">{t("automations.outputLabel")}</Label>
                  {/* Chosen first, each removable: the set is the answer, and the input below is how
                      it grows. Chips rather than a comma-separated string, because a string makes
                      the reader parse what the product already knows. */}
                  {outputLabels.length > 0 ? (
                    <div className="flex flex-wrap gap-1.5">
                      {outputLabels.map((label) => (
                        <Badge key={label} variant="secondary" className="gap-1 font-mono">
                          {label}
                          <button
                            type="button"
                            aria-label={`${t("automations.outputLabelRemove")} ${label}`}
                            onClick={() =>
                              setOutputLabels(outputLabels.filter((kept) => kept !== label))
                            }
                          >
                            <X className="size-3" aria-hidden="true" />
                          </button>
                        </Badge>
                      ))}
                    </div>
                  ) : null}
                  <div className="flex gap-2">
                    {/* A datalist, so one control both suggests and accepts anything — a select
                        would refuse the label that does not exist yet, which is a legitimate way
                        to build a workflow forwards. */}
                    <Input
                      id="output-label"
                      list="output-label-suggestions"
                      value={outputDraft}
                      onChange={(event) => setOutputDraft(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter") {
                          // Enter adds a label; without this it would submit the whole form, which
                          // is the wrong thing to do while somebody is still listing destinations.
                          event.preventDefault();
                          addOutputLabel();
                        }
                      }}
                      placeholder={t("automations.outputLabelPlaceholder")}
                    />
                    <datalist id="output-label-suggestions">
                      {outputSuggestions.map((suggestion) => (
                        <option key={suggestion} value={suggestion} />
                      ))}
                    </datalist>
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={addOutputLabel}
                      disabled={!outputDraft.trim()}
                    >
                      {t("automations.outputLabelAdd")}
                    </Button>
                  </div>
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
                <Button type="submit" disabled={saving}>
                  {saving
                    ? editing
                      ? t("automations.saving")
                      : t("automations.adding")
                    : editing
                      ? t("automations.save")
                      : t("automations.add")}
                </Button>
              </div>

              <p className="text-xs text-muted-foreground">{t("automations.catalogueHint")}</p>

              {saveFailed && (
                <p className="text-sm text-destructive" role="alert">
                  {/* The API's own reason: an overlap refusal names the Automation collided with, and
                      a generic line throws that away (design D4). */}
                  {(saveError instanceof ApiError && saveError.detail) ||
                    t("automations.saveFailed")}
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

      {/* Two sections, not two views of one list (design D1). They share a tab because the
          relationship is the point — the workflow is built out of the catalogue, and a reader who
          cannot see both at once cannot see that. Stacked below the wide breakpoint. */}
      {rows.length > 0 && (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,24rem)_minmax(0,1fr)]">
          <section className="flex min-w-0 flex-col gap-2">
            <h3 className="text-sm font-semibold">{t("automations.catalogue")}</h3>
            <p className="text-xs text-muted-foreground">{t("automations.catalogue.hint")}</p>
            {/* The block the workflow's gaps accept (#137). It lives here because the catalogue is
                what the flow is built out of — that is the whole point of DEC-053's separation. */}
            <HumanStepBlock />
            <p className="hidden text-xs text-muted-foreground xl:block">
              {t("canvas.block.hint")}
            </p>
            {catalogue}
          </section>

          <section className="flex min-w-0 flex-col gap-2">
            <h3 className="text-sm font-semibold">{t("automations.workflow")}</h3>
            <WorkflowCanvas projectId={projectId} automations={rows} />
          </section>
        </div>
      )}
    </div>
  );
}
