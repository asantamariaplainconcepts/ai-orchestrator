import { MoreHorizontal, X } from "lucide-react";
import { useState } from "react";
import { t, tCount } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";
import { ResponsiveDialog } from "@/shared/ui/responsive-dialog";
import { AutomationSentence } from "./AutomationSentence";
import { RadioGroup, RadioGroupItem } from "@/shared/ui/radio-group";
import { Switch } from "@/shared/ui/switch";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { NativeSelect } from "@/shared/ui/native-select";
import { PromptScratchpad } from "./PromptScratchpad";
import { WorkflowSetupSection } from "./WorkflowSetupSection";
import { BoardPreview } from "./BoardPreview";
import { ApiError } from "@/shared/http/client";
import { AUTOMATION_ACTIONS, AGENT_RUNTIMES, EXECUTABLE_ACTIONS } from "./types";
import type { AgentRuntime, Automation, AutomationAction } from "./types";
import { summarise, workflowMembers } from "./workflowGraph";
import { useLifecycle } from "./useLifecycle";
import {
  useAgentModels,
  useAutomations,
  useCreateAutomation,
  useDeleteAutomation,
  useProjectPrompts,
  useSetAutomationEnabled,
  useUpdateAutomation,
} from "./useAutomations";

/**
 * The form's own id, so the panel's footer can submit it from outside the `<form>` element. The
 * alternative is a footer inside the scrolling body, where Save scrolls away from the reader.
 */
const FORM_ID = "automation-form";

/**
 * The Automations tab: what this project has, and a read-only picture of the flow it produces.
 *
 * The canvas that used to be the top half of this tab is gone (#310, AC 11). It was the second drawing
 * of one arrangement, and the arrangement is now stored and authored on the Backlog board — where an
 * Admin already reads the flow — so a picture here that could also be edited would be exactly the
 * duplicate description ADR-0022 records. What is left is the catalogue, which is a reference, and the
 * preview under it, which is the consequence: creating an Automation still shows what it does to the
 * board without changing tab, and that was the preview's whole reason for existing.
 *
 * Setup and the scratchpad are tools you reach for on a first day or an odd afternoon, so they live in
 * the toolbar and open over the tab rather than standing between the reader and the catalogue.
 *
 * Creating and editing live in a panel rather than inline (design review 6b). Mounting the form
 * above the catalogue moved the page under the reader: opening an edit scrolled the tab to the top
 * and pushed everything down, and after Save you had to find your place again.
 */
export function AutomationsSection({ projectId }: { projectId: string }) {
  const automations = useAutomations(projectId);
  // The stored lifecycle, read once for this tab (#310, design D6). The rail's "in workflow" tag and
  // the preview's columns both come from it, so they cannot disagree.
  const lifecycle = useLifecycle(projectId);
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
  // Which tool the toolbar has opened, if any. One value rather than two flags: they are alternatives
  // and a state that can hold both would let two panels open at once.
  const [tool, setTool] = useState<"scratchpad" | "setup" | null>(null);
  // Delete asks twice (design review 6b). It used to be one un-confirmed click per catalogue row —
  // beside Edit, at the width of a mis-aim — and the refusal it can raise is about runs, not intent.
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [triggerLabel, setTriggerLabel] = useState("");
  const [triggerState, setTriggerState] = useState("");
  const [action, setAction] = useState<AutomationAction>("RepositoryPrompt");
  // "" means the Project default (#244): stored as null, resolved at execution time.
  const [runtime, setRuntime] = useState<AgentRuntime | "">("");
  const [model, setModel] = useState("");
  const [requiresApproval, setRequiresApproval] = useState(false);
  const [promptPath, setPromptPath] = useState("");
  // A set since #165, plus the text currently being typed into the picker. Two pieces of state
  // because they are two things: what is chosen, and what is half-written.
  const [outputLabels, setOutputLabels] = useState<string[]>([]);
  // An answer, not an absence (#231, design D4). Stored as a null claim, which is what "stop" has
  // always meant — what changes is that the Admin said which they meant.
  const [handsOn, setHandsOn] = useState(false);
  // The to-stage of the one transition this Automation claims (#310). Single-valued, so no form on
  // this surface can express a second one (AC 13) — and carried on every save, because the endpoint
  // replaces the whole Automation and a field this form omitted would be a claim every edit cleared.
  const [toStage, setToStage] = useState("");
  const [outputDraft, setOutputDraft] = useState("");
  // Kept as text so blank can mean "BR-005's default" — a number input cannot hold that.
  const [timeoutMinutes, setTimeoutMinutes] = useState("");
  // The prompt picker's listing (#215) is fetched only once the form is open — a form keystroke,
  // not a page load, is what justifies a vendor read. Degradation arrives as data (`reason`) and
  // the field falls back to the plain input it always was.
  const prompts = useProjectPrompts(projectId, creating);

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
    setRuntime("");
    setModel("");
    setRequiresApproval(false);
    setPromptPath("");
    setOutputLabels([]);
    setHandsOn(false);
    setToStage("");
    setOutputDraft("");
    setTimeoutMinutes("");
  }

  /** A blank panel. Separate from `closeForm` so "start over" is not spelled as "close then open". */
  function startCreate() {
    reset();
    setEditing(null);
    setConfirmingDelete(false);
    setCreating(true);
  }

  /**
   * Seeds the form from what is stored. Every field, including the timeout — the endpoint is a full
   * replace, so a field this form did not carry would be replaced by the default for absent, and the
   * timeout is the one create never carried (design D2).
   */
  function openEdit(automation: Automation) {
    setEditing(automation);
    setCreating(true);
    setConfirmingDelete(false);
    setTriggerLabel(automation.triggerLabel);
    setTriggerState(automation.triggerState ?? "");
    setAction(automation.action);
    setRuntime(automation.runtime ?? "");
    setModel(automation.model ?? "");
    setRequiresApproval(automation.requiresApproval);
    setPromptPath(automation.promptPath ?? "");
    setOutputLabels([...automation.outputLabels]);
    // The claim, not the marks: since #310 an Automation that only marks the Story claims no
    // transition, so "hands on" is the presence of a to-stage and nothing else.
    setHandsOn(automation.toStage !== null);
    setToStage(automation.toStage ?? "");
    setOutputDraft("");
    setTimeoutMinutes(String(automation.timeoutMinutes));
  }

  function closeForm() {
    setCreating(false);
    setEditing(null);
    setConfirmingDelete(false);
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
      // "" is the Project default — sent as null (#244).
      runtime: runtime === "" ? null : runtime,
      requiresApproval,
      // Blank is the default, not zero. Sending 0 would be a timeout of no time at all.
      timeoutMinutes: timeoutMinutes.trim() === "" ? null : Number(timeoutMinutes),
      // Required since #162: with one action, an Automation that names no prompt could never run,
      // and the server refuses it at save. Trimmed only — the empty case is the server's refusal to
      // give, not this form's to silently swallow.
      promptPath: promptPath.trim() ? promptPath.trim() : null,
      // Marks only since #310, and therefore independent of the radio: an Automation that hands
      // work on may also mark the Story, and one that stops may mark it too. The half-typed value
      // counts — an Admin who typed a label and pressed Save meant it, and silently dropping it is
      // the kind of loss a form should never inflict.
      outputLabels: withDraft(),
      // The one transition this Automation claims (#310). Typed-then-stopped resolves to stopped:
      // the radio is the later and more explicit answer, and honouring a stage the Admin then said
      // not to hand to would be obeying the field over the person.
      toStage: handsOn && toStage.trim() ? toStage.trim() : null,
      // Always sent, never omitted: the endpoint is a full replace, so a field this form did not
      // carry would be cleared on every edit. Blank means inherit, which is what the server
      // normalises whitespace to anyway.
      model: model.trim() ? model.trim() : null,
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
  // Read, not derived (#310, ADR-0022). The rail's "in workflow" tag and the preview's columns are one
  // list served by the aggregate that owns it, so the two cannot disagree the way the canvas and the
  // board once did.
  const stages = lifecycle.data?.stages ?? [];
  const members = workflowMembers(stages, rows);
  const summary = summarise(stages, rows);
  // The first-run exception (design review 6a): with nothing configured, setup is not a tool tucked
  // into a menu — it IS the content of the tab, and the one press that answers "what should this
  // project have?" before the panel asks you to answer it a field at a time (#229).
  const firstRun = automations.data !== undefined && rows.length === 0;

  /**
   * The three questions and the sentence that restates them (#231) — one form, whichever container
   * it arrives in. It lives in a value rather than a component so the fifteen pieces of state above
   * stay where they are read, and the panel stays the only thing that changed about editing.
   */
  const form = (
    <form id={FORM_ID} className="flex flex-col gap-4 px-5 py-4" onSubmit={submit}>
      {/* Pinned: the restatement is what the reader checks each answer against, so it must not
          scroll away from the fields that change it. */}
      <div className="sticky top-0 z-10 -mx-5 -mt-4 bg-background px-5 pt-4 pb-1">
        <AutomationSentence
          triggerLabel={triggerLabel}
          triggerState={triggerState}
          promptPath={promptPath}
          runtime={runtime}
          requiresApproval={requiresApproval}
          handsOn={handsOn}
          toStage={toStage}
          marks={withDraft()}
        />
      </div>

      <section className="flex flex-col gap-3">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <span className="grid size-5 shrink-0 place-content-center rounded-full bg-primary/10 text-[11px] text-primary">
            1
          </span>
          {t("automations.q1")}
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
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
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <span className="grid size-5 shrink-0 place-content-center rounded-full bg-primary/10 text-[11px] text-primary">
            2
          </span>
          {t("automations.q2")}
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
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
            <Label htmlFor="prompt-path">{t("automations.promptFile")}</Label>
            {/* A datalist for the same reason the output labels use one (#165): suggest what
                exists — the repository's own prompts, read live (#215) — while accepting a
                name that is still on its way in a pending PR. */}
            <Input
              id="prompt-path"
              required
              list="prompt-file-suggestions"
              value={promptPath}
              onChange={(event) => setPromptPath(event.target.value)}
              placeholder={t("automations.promptFilePlaceholder")}
            />
            <datalist id="prompt-file-suggestions">
              {(prompts.data?.names ?? []).map((name) => (
                <option key={name} value={name} />
              ))}
            </datalist>
            {/* Degradation is a readable reason, never a blocked form: the field above is
                already the plain input, so discovery failing costs suggestions and nothing
                else. */}
            <p className="text-xs text-muted-foreground">
              {prompts.data?.reason
                ? `${t("automations.promptSuggestionsUnavailable")} — ${prompts.data.reason}`
                : prompts.data && prompts.data.names.length === 0
                  ? `${t("automations.promptSuggestionsEmpty")} ${prompts.data.directory}`
                  : t("automations.promptFileHint")}
            </p>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="runtime">{t("automations.runtime")}</Label>
            <NativeSelect
              id="runtime"
              value={runtime}
              onChange={(event) => setRuntime(event.target.value as AgentRuntime | "")}
            >
              <option value="">{t("automations.runtimeProjectDefault")}</option>
              {AGENT_RUNTIMES.map((candidate) => (
                <option key={candidate} value={candidate}>
                  {candidate}
                </option>
              ))}
            </NativeSelect>
          </div>
          <ModelField
            runtime={runtime}
            value={model}
            onChange={setModel}
            /* Only once the panel is open — asking costs a whole sandbox where agents are
               sandboxed, and a closed form has nobody to offer anything to. */
            enabled={creating}
          />
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

          {/* The consequence beside the execution it gates (design D1). It was a bare checkbox
              next to Save, which read as a submission option rather than a property of the Run. */}
          <label className="flex cursor-pointer items-start gap-2.5 rounded-lg border border-warning/50 bg-warning/10 p-3 md:col-span-2">
            <Switch
              id="requires-approval"
              checked={requiresApproval}
              onCheckedChange={setRequiresApproval}
            />
            <span className="flex flex-col gap-0.5">
              <span className="text-xs font-semibold">{t("automations.approval")}</span>
              <span className="text-[11px] leading-snug text-muted-foreground">
                {t("automations.approvalExplainer")}
              </span>
            </span>
          </label>
        </div>
      </section>

      <section className="flex flex-col gap-3">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <span className="grid size-5 shrink-0 place-content-center rounded-full bg-primary/10 text-[11px] text-primary">
            3
          </span>
          {t("automations.q3")}
        </h3>
        <div className="grid grid-cols-1 gap-4">
          <RadioGroup
            value={handsOn ? "hand-on" : "stop"}
            onValueChange={(value) => setHandsOn(value === "hand-on")}
          >
            {[
              {
                value: "hand-on",
                label: t("automations.after.handOn"),
                hint: t("automations.after.handOnHint"),
              },
              {
                value: "stop",
                label: t("automations.after.stop"),
                hint: t("automations.after.stopHint"),
              },
            ].map((option) => (
              <label
                key={option.value}
                className="flex cursor-pointer items-start gap-2.5 rounded-lg border border-input p-3"
              >
                <RadioGroupItem value={option.value} id={`after-${option.value}`} />
                <span className="flex flex-col gap-0.5">
                  <span className="text-xs font-semibold">{option.label}</span>
                  <span className="text-[11px] leading-snug text-muted-foreground">
                    {option.hint}
                  </span>
                </span>
              </label>
            ))}
          </RadioGroup>
          {/* The claim, single-valued (#310, AC 13). One input rather than a set, so no form on this
              surface can express a second transition — and the board is where an arrangement is
              normally authored, so this field exists for the create that names one from the start. */}
          {handsOn ? (
            <div className="flex flex-col gap-2">
              <Label htmlFor="to-stage">{t("automations.toStage")}</Label>
              <Input
                id="to-stage"
                list="to-stage-suggestions"
                value={toStage}
                onChange={(event) => setToStage(event.target.value)}
                placeholder={t("automations.toStagePlaceholder")}
              />
              <datalist id="to-stage-suggestions">
                {outputSuggestions.map((suggestion) => (
                  <option key={suggestion} value={suggestion} />
                ))}
              </datalist>
              <p className="text-xs text-muted-foreground">{t("automations.toStageHint")}</p>
            </div>
          ) : null}

          {/* Marks, beside the claim and never inside it (#310). A mark that names no stage is the
              normal case now, not a dangling edge, so this field is offered whichever answer the
              radio holds. */}
          <div className="flex flex-col gap-2">
            <Label htmlFor="output-label">{t("automations.marks")}</Label>
            <p className="text-xs text-muted-foreground">{t("automations.marksHint")}</p>
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
                      onClick={() => setOutputLabels(outputLabels.filter((kept) => kept !== label))}
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
      </section>

      <p className="text-xs text-muted-foreground">{t("automations.catalogueHint")}</p>

      {/* Every refusal this panel can raise, answered inside it. They used to sit at the top of the
          tab, which is where the reader is not looking once a dialog has their attention. */}
      {saveFailed && (
        <p className="text-sm text-destructive" role="alert">
          {/* The API's own reason: an overlap refusal names the Automation collided with, and
              a generic line throws that away (design D4). */}
          {(saveError instanceof ApiError && saveError.detail) || t("automations.saveFailed")}
        </p>
      )}
      {remove.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.delete.refused")}
        </p>
      )}
      {setEnabled.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.enableFailed")}
        </p>
      )}
    </form>
  );

  /**
   * Destructive on the left, the way out and the way on to the right (design review 6b).
   *
   * Delete and enable both live here because they belong to *this* Automation and to nothing on the
   * tab: as a button per catalogue row, Delete was one mis-aimed click from gone, and Enable was a
   * third button competing with it for the width of a row.
   */
  const footer = (
    <>
      {editing ? (
        <span className="flex flex-wrap items-center gap-2">
          {confirmingDelete ? (
            <Button
              variant="destructive"
              size="sm"
              type="button"
              disabled={remove.isPending}
              onClick={() => remove.mutate(editing.id, { onSuccess: closeForm })}
            >
              {t("automations.delete.confirm")}
            </Button>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              type="button"
              className="text-destructive hover:text-destructive"
              title={t("automations.delete.hint")}
              onClick={() => setConfirmingDelete(true)}
            >
              {t("automations.delete.start")}
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            type="button"
            disabled={setEnabled.isPending}
            onClick={() => setEnabled.mutate({ id: editing.id, enabled: !editing.enabled })}
          >
            {editing.enabled ? t("automations.disable") : t("automations.enable")}
          </Button>
        </span>
      ) : (
        <span />
      )}
      <span className="flex items-center gap-2">
        <Button variant="outline" type="button" onClick={closeForm}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" form={FORM_ID} disabled={saving}>
          {saving
            ? editing
              ? t("automations.saving")
              : t("automations.adding")
            : editing
              ? t("automations.save")
              : t("automations.add")}
        </Button>
      </span>
    </>
  );

  /**
   * The catalogue (design review 6a): one row per Automation, its relation to the flow instead of a
   * repeat of its fields, and the whole row the way into the edit panel.
   *
   * It is the tab's content now rather than a rail beside the flow, because the flow moved (#310,
   * design D7). The arrangement is authored on the board, where it is read; this tab is what exists.
   * The read-only preview below says what the catalogue makes of that board, so somebody creating an
   * Automation still sees the consequence without changing tab.
   */
  const catalogue = (
    <aside className="flex min-w-0 flex-col gap-2">
      <span className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
        {t("automations.catalogue")}
      </span>
      <p className="text-xs text-muted-foreground">{t("automations.catalogue.hint")}</p>
      <Card className="gap-0 overflow-hidden py-0">
        <ul className="divide-y divide-border">
          {rows.map((automation) => {
            const inWorkflow = members.has(automation.id);

            return (
              <li key={automation.id}>
                {/* The row IS the control (design review 6a). It replaced three buttons — Edit,
                    Enable, Delete — which is what made a row wider than the information in it, and
                    put an un-confirmed Delete a mis-aim away from Edit. */}
                <button
                  type="button"
                  onClick={() => openEdit(automation)}
                  aria-label={`${t("automations.edit")} ${automation.triggerLabel}`}
                  className="flex min-h-11 w-full items-center justify-between gap-2 px-3 py-2 text-left outline-none hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/50"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <span className="truncate font-mono text-xs font-semibold text-primary">
                      {automation.triggerLabel}
                    </span>
                    {automation.enabled ? null : (
                      <Badge variant="outline" className="shrink-0 text-[10px]">
                        {t("automations.disabled")}
                      </Badge>
                    )}
                  </span>
                  <span
                    className={cn(
                      "shrink-0 text-[10px] font-semibold",
                      inWorkflow ? "text-success" : "text-muted-foreground",
                    )}
                  >
                    {inWorkflow ? t("automations.inWorkflow") : t("automations.standalone")}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </Card>
    </aside>
  );

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-baseline gap-2">
          <h2 className="text-base font-semibold">{t("automations.heading")}</h2>
          {automations.data ? (
            <Badge variant="secondary">
              {tCount(rows.length, "automations.count.one", "automations.count.other")}
            </Badge>
          ) : null}
          {/* Stages and human stops (#310): "6 Automations" is a fact about the catalogue and says
              nothing about the flow. Beside the count, where the three numbers can be read as the
              three different things they are. */}
          {summary.stages > 0 ? (
            <span className="text-xs text-muted-foreground">
              {tCount(
                summary.stages,
                "automations.workflow.stages.one",
                "automations.workflow.stages.other",
              )}
              {" · "}
              {tCount(
                summary.humanStops,
                "automations.workflow.stops.one",
                "automations.workflow.stops.other",
              )}
            </span>
          ) : null}
        </div>

        <div className="flex items-center gap-2">
          {/* Tools at pointer widths, folded under ⋯ below (design review 6c) — the same two
              actions either way, so nothing is reachable on one width only. */}
          <span className="hidden items-center gap-2 md:flex">
            <Button variant="outline" type="button" onClick={() => setTool("scratchpad")}>
              {t("automations.tools.tryPrompt")}
            </Button>
            {firstRun ? null : (
              <Button variant="outline" type="button" onClick={() => setTool("setup")}>
                {t("automations.tools.setup")}
              </Button>
            )}
          </span>
          <Button type="button" onClick={startCreate}>
            {t("automations.new")}
          </Button>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="outline"
                size="icon"
                type="button"
                aria-label={t("automations.tools.more")}
                className="md:hidden"
              >
                <MoreHorizontal className="size-4" aria-hidden="true" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => setTool("scratchpad")}>
                {t("automations.tools.tryPrompt")}
              </DropdownMenuItem>
              {firstRun ? null : (
                <DropdownMenuItem onSelect={() => setTool("setup")}>
                  {t("automations.tools.setup")}
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {automations.isPending && (
        <p className="text-sm text-muted-foreground">{t("automations.loading")}</p>
      )}
      {automations.isError && (
        <p className="text-sm text-destructive" role="alert">
          {t("automations.error")}
        </p>
      )}

      {firstRun && (
        <>
          <p className="text-sm text-muted-foreground">{t("automations.empty")}</p>
          <WorkflowSetupSection projectId={projectId} />
        </>
      )}

      {/* The catalogue, and under it the flow that catalogue produces — read-only, because the flow is
          arranged on the board now (#310, design D7, AC 11). There is no canvas here any more: two
          drawings of one arrangement is what ADR-0022 was written about, and the board is the one that
          an Admin already reads. The preview stays so that creating an Automation still shows its
          consequence without changing tab, which is the whole reason it existed. */}
      {rows.length > 0 && (
        <div className="flex flex-col gap-4">
          {catalogue}
          <section className="flex min-w-0 flex-col gap-2">
            <h3 className="text-sm font-semibold">{t("automations.workflow")}</h3>
            {stages.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("automations.workflow.empty")}</p>
            ) : (
              <BoardPreview stages={stages} automations={rows} />
            )}
          </section>
        </div>
      )}

      <ResponsiveDialog
        open={creating}
        // Esc and the overlay cancel, exactly as the close button does — one way out, three
        // gestures, and none of them a half-open form left behind.
        onOpenChange={(open) => {
          if (!open) closeForm();
        }}
        title={
          editing ? (
            <>
              {t("automations.editTitle")} —{" "}
              <span className="font-mono text-primary">{editing.triggerLabel}</span>
            </>
          ) : (
            t("automations.new")
          )
        }
        footer={footer}
      >
        {form}
      </ResponsiveDialog>

      {/* Each tool keeps its own component and its own heading; only the container is new. */}
      <ResponsiveDialog
        open={tool !== null}
        onOpenChange={(open) => {
          if (!open) setTool(null);
        }}
        title={tool === "setup" ? t("automations.tools.setup") : t("automations.tools.tryPrompt")}
        hideTitle
        className="sm:max-w-2xl"
      >
        {/* The panel is the card now, so the component's own card chrome would be a second box
            inside the first. Neutralised here rather than in the component: what changed is where
            these two live, not what they are. */}
        <div className="p-1 [&_[data-slot=card]]:border-0 [&_[data-slot=card]]:bg-transparent [&_[data-slot=card]]:shadow-none">
          {tool === "setup" ? <WorkflowSetupSection projectId={projectId} /> : null}
          {tool === "scratchpad" ? <PromptScratchpad projectId={projectId} /> : null}
        </div>
      </ResponsiveDialog>
    </div>
  );
}

/**
 * The model chooser (#291). Its whole job is telling three states apart, because they send a
 * reader to different places and only one of them is a list:
 *
 * - **enumerated** — the runtime listed them itself, on the machine that will run it;
 * - **declared** — this runtime has no listing command, so an operator's configuration decides,
 *   and an empty one means nobody has declared any;
 * - **couldNotAsk** — the machine could not be reached, which says nothing at all about the
 *   runtime's models and must never be rendered as though it did.
 *
 * A written value is accepted in every one of them, and blank always means inherit — so a machine
 * that is down never blocks somebody from editing an Automation.
 */
function ModelField({
  runtime,
  value,
  onChange,
  enabled,
}: {
  runtime: string;
  value: string;
  onChange: (model: string) => void;
  enabled: boolean;
}) {
  const models = useAgentModels(runtime, enabled);
  const offered = models.data?.models ?? [];
  const source = models.data?.source;

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="model">{t("automations.model")}</Label>
      {offered.length > 0 ? (
        <NativeSelect id="model" value={value} onChange={(event) => onChange(event.target.value)}>
          <option value="">{t("automations.modelDeploymentDefault")}</option>
          {/* A stored model the runtime no longer offers must stay selectable, or opening the
              form would silently change what this Automation runs. */}
          {(offered.includes(value) || !value ? offered : [value, ...offered]).map((candidate) => (
            <option key={candidate} value={candidate}>
              {candidate}
            </option>
          ))}
        </NativeSelect>
      ) : (
        <Input
          id="model"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          placeholder={t("automations.modelPlaceholder")}
        />
      )}
      <p className="text-xs text-muted-foreground">
        {!runtime
          ? t("automations.modelPickRuntimeFirst")
          : models.isPending
            ? t("automations.modelAsking")
            : source === "couldNotAsk"
              ? t("automations.modelCouldNotAsk")
              : source === "declared" && offered.length === 0
                ? t("automations.modelNoneDeclared")
                : source === "declared"
                  ? t("automations.modelDeclared")
                  : t("automations.modelEnumerated")}
      </p>
    </div>
  );
}
