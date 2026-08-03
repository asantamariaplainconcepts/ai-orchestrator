import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import {
  usePipelineDiscovery,
  useSetUpWorkflow,
  type PipelineCandidate,
  type PlannedStep,
  type WorkflowSetupReport,
} from "./useWorkflowSetup";

/**
 * #229 — the whole workflow in one press, in three moves: look, confirm, report.
 *
 * The middle move is the point. Discovery **proposes** and never picks (design D1): a heuristic
 * that reconfigured a project the first time somebody pressed a button would be wrong for
 * somebody, and the only thing worse than not finding a pipeline is adopting the wrong one. So
 * the candidates are shown with what each holds, and nothing is written until one is chosen.
 */
export function WorkflowSetupSection({ projectId }: { projectId: string }) {
  const [looking, setLooking] = useState(false);
  const [chosen, setChosen] = useState<string | null>(null);

  const discovery = usePipelineDiscovery(projectId, looking);
  const setUp = useSetUpWorkflow(projectId);

  return (
    <Card>
      <CardContent className="flex flex-col gap-5">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold">{t("workflowSetup.title")}</h2>
          <p className="text-sm text-muted-foreground">{t("workflowSetup.explainer")}</p>
        </div>

        {!looking ? (
          <div>
            <Button type="button" onClick={() => setLooking(true)}>
              {t("workflowSetup.look")}
            </Button>
          </div>
        ) : null}

        {looking && discovery.isPending ? (
          <p className="text-sm text-muted-foreground" role="status">
            {t("workflowSetup.looking")}
          </p>
        ) : null}

        {looking && discovery.isError ? (
          <p className="text-sm text-destructive" role="alert">
            {t("workflowSetup.lookFailed")}
          </p>
        ) : null}

        {discovery.data ? (
          <Proposal
            candidates={discovery.data.candidates}
            searchedIn={discovery.data.searchedIn}
            reason={discovery.data.reason}
            chosen={chosen}
            onChoose={setChosen}
          />
        ) : null}

        {discovery.data && !discovery.data.reason ? (
          <div className="flex flex-col gap-3">
            {/* The plan, before the button (#233). It replaces a checkbox that was doing a
                preview's job: the rows say which steps install a starter, so a toggle asking
                whether to install them had nothing left to communicate that the list does not. */}
            <Plan steps={planFor(discovery.data.candidates, chosen)} />

            <div className="flex flex-wrap items-center gap-3">
              <Button
                type="button"
                disabled={setUp.isPending}
                onClick={() =>
                  setUp.mutate({
                    promptDirectory: chosen ?? undefined,
                    // The rows already said which files would be written, so the decision is the
                    // press. A second consent for a preview somebody just read is a confirmation
                    // of a confirmation.
                    installMissing: true,
                  })
                }
              >
                {setUp.isPending ? t("workflowSetup.building") : t("workflowSetup.build")}
              </Button>
              {/* Beside the button, where the decision is taken — not in a paragraph above it. */}
              <p className="text-xs text-muted-foreground">{t("workflowSetup.draftSafety")}</p>
            </div>
          </div>
        ) : null}

        {setUp.isError ? (
          <p className="text-sm text-destructive" role="alert">
            {setUp.error instanceof ApiError && setUp.error.detail
              ? setUp.error.detail
              : t("workflowSetup.buildFailed")}
          </p>
        ) : null}

        {setUp.data ? <Report report={setUp.data} /> : null}
      </CardContent>
    </Card>
  );
}

function Proposal({
  candidates,
  searchedIn,
  reason,
  chosen,
  onChoose,
}: {
  candidates: PipelineCandidate[];
  searchedIn: string[];
  reason: string | null;
  chosen: string | null;
  onChoose: (directory: string) => void;
}) {
  if (reason) {
    return (
      <p className="text-sm text-muted-foreground" role="status">
        {reason}
      </p>
    );
  }

  if (candidates.length === 0) {
    // The empty state names where it looked: "no pipeline here" and "this button does not work"
    // look identical otherwise.
    return (
      <p className="text-sm text-muted-foreground" role="status">
        {t("workflowSetup.foundNothing")} <span className="font-mono">{searchedIn.join(", ")}</span>
      </p>
    );
  }

  return (
    <ul className="flex flex-col gap-2">
      {candidates.map((candidate) => (
        <li
          key={candidate.directory}
          className="flex flex-col gap-2 rounded-md border border-border p-3"
        >
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="flex flex-col gap-1">
              <span className="font-mono text-sm">{candidate.directory}</span>
              <p className="text-sm text-muted-foreground">
                {t("workflowSetup.wires")} {candidate.steps.join(", ")}
              </p>
              {candidate.unmatched.length > 0 ? (
                <p className="text-xs text-muted-foreground">
                  {t("workflowSetup.notWired")} {candidate.unmatched.join(", ")}
                </p>
              ) : null}
            </div>

            <Button
              type="button"
              size="sm"
              variant={chosen === candidate.directory ? "default" : "secondary"}
              onClick={() => onChoose(candidate.directory)}
            >
              {chosen === candidate.directory
                ? t("workflowSetup.chosen")
                : t("workflowSetup.choose")}
            </Button>
          </div>
        </li>
      ))}
    </ul>
  );
}

/**
 * The five facts design D5 asks for. An outcome of "done" teaches nobody what happened to a
 * repository they did not read first.
 */
function Report({ report }: { report: WorkflowSetupReport }) {
  return (
    <div className="flex flex-col gap-2 rounded-md border border-border p-3" role="status">
      <p className="text-sm">
        {t("workflowSetup.readsFrom")} <span className="font-mono">{report.directory}</span>
      </p>

      <Fact label={t("workflowSetup.created")} values={report.created} />
      <Fact
        label={t("workflowSetup.skipped")}
        values={report.skipped.map((step) => `${step.trigger} — ${step.reason}`)}
      />
      <Fact label={t("workflowSetup.found")} values={report.foundNotWired} />
      <Fact
        label={t("workflowSetup.missing")}
        values={report.missingPrompts.map((prompt) => prompt.resolvedPath ?? prompt.saveAs)}
      />

      {report.installed?.pullRequestUrl ? (
        <p className="text-sm">
          {t("workflowSetup.installed")}{" "}
          <a
            className="underline underline-offset-2"
            href={report.installed.pullRequestUrl}
            target="_blank"
            rel="noreferrer"
          >
            {report.installed.pullRequestUrl}
          </a>
        </p>
      ) : null}

      {/* A refusal to install is reported beside what did happen: "created five Automations"
          must never stand for "and the prompts they name exist". */}
      {report.installed?.failure ? (
        <p className="text-sm text-destructive">
          {t("workflowSetup.installFailed")} {report.installed.failure}
        </p>
      ) : null}
    </div>
  );
}

function Fact({ label, values }: { label: string; values: string[] }) {
  if (values.length === 0) return null;

  return (
    <p className="text-sm text-muted-foreground">
      <Badge variant="secondary">{label}</Badge> {values.join(", ")}
    </p>
  );
}

/**
 * Which candidate's plan to show: the chosen directory, or the first offered when nobody has
 * chosen yet — the same directory the button would use, so the preview and the press agree.
 */
function planFor(candidates: PipelineCandidate[], chosen: string | null): PlannedStep[] {
  const candidate = chosen ? candidates.find((entry) => entry.directory === chosen) : candidates[0];

  return candidate?.plan ?? [];
}

/**
 * One row per step the build would create (#233). It replaced prose and a checkbox: what the click
 * would do was a surprise, and the per-step detail existed only in the report afterwards — which is
 * the wrong side of an action that writes to somebody's repository.
 */
function Plan({ steps }: { steps: PlannedStep[] }) {
  const [expanded, setExpanded] = useState(false);

  if (steps.length === 0) return null;

  // Long pipelines collapse: a plan that fills the screen stops being read, which defeats it.
  const visible = expanded ? steps : steps.slice(0, 3);
  const hidden = steps.length - visible.length;

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold">{t("workflowSetup.planTitle")}</h3>
      <ul className="divide-y divide-border rounded-lg border border-border">
        {visible.map((step) => (
          <li key={step.trigger} className="flex flex-wrap items-center gap-2.5 px-3.5 py-2">
            <span className="w-28 shrink-0 font-mono text-[11.5px] font-semibold text-primary">
              {step.trigger}
            </span>
            <span className="min-w-0 flex-1 text-xs text-muted-foreground">
              {t("workflowSetup.wireTo")} <span className="font-mono">{step.promptFile}</span>
              {step.gated ? (
                <>
                  {" · "}
                  <b className="text-warning-foreground">{t("workflowSetup.gate")}</b>
                </>
              ) : null}
            </span>
            <Badge variant={step.exists ? "secondary" : "outline"}>
              {step.exists ? t("workflowSetup.exists") : t("workflowSetup.installStarter")}
            </Badge>
          </li>
        ))}
      </ul>
      {hidden > 0 || expanded ? (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="self-start"
          onClick={() => setExpanded(!expanded)}
        >
          {expanded ? t("workflowSetup.planFewer") : `+ ${hidden} ${t("workflowSetup.planMore")}`}
        </Button>
      ) : null}
    </div>
  );
}
