import { useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Checkbox } from "@/shared/ui/checkbox";
import { Label } from "@/shared/ui/label";
import {
  usePipelineDiscovery,
  useSetUpWorkflow,
  type PipelineCandidate,
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
  const [installMissing, setInstallMissing] = useState(true);

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
            <div className="flex items-center gap-2">
              <Checkbox
                id="workflow-install-missing"
                checked={installMissing}
                onCheckedChange={(checked) => setInstallMissing(checked === true)}
              />
              {/* A second consent on purpose: creating Automations here and writing files into
                  somebody's repository are different decisions (design D4). */}
              <Label htmlFor="workflow-install-missing">{t("workflowSetup.installMissing")}</Label>
            </div>

            <div>
              <Button
                type="button"
                disabled={setUp.isPending}
                onClick={() =>
                  setUp.mutate({
                    promptDirectory: chosen ?? undefined,
                    installMissing,
                  })
                }
              >
                {setUp.isPending ? t("workflowSetup.building") : t("workflowSetup.build")}
              </Button>
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
