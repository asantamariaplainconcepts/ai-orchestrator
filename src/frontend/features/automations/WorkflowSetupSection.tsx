import { useMemo, useState } from "react";
import { ApiError } from "@/shared/http/client";
import { t } from "@/shared/i18n";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Switch } from "@/shared/ui/switch";
import { handoffsBrokenBy } from "./planHandoff";
import {
  usePipelineDiscovery,
  useSetUpWorkflow,
  type PipelineCandidate,
  type PlannedStep,
  type StarterTier,
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
  // Exclusions rather than selections (#262), so a row this card has not seen before is selected by
  // definition: a plan that grows never quietly leaves the new step out. A different candidate is a
  // different list, so choosing one clears them.
  const [excluded, setExcluded] = useState<ReadonlySet<string>>(() => new Set());
  // Consent is opt-in, so this is a set of what was granted rather than of what was withheld — the
  // mirror image of `excluded` above, and for the mirror-image reason: a tier this card has not seen
  // before must arrive off, never on.
  const [consented, setConsented] = useState<ReadonlySet<string>>(() => new Set());

  const discovery = usePipelineDiscovery(projectId, looking);
  const setUp = useSetUpWorkflow(projectId);

  // A tier with no prerequisite needs no consent and gets no control; only a gated one is offered.
  const gatedTiers = useMemo(
    () => (discovery.data?.tiers ?? []).filter((tier) => tier.requires !== null),
    [discovery.data?.tiers],
  );

  const plan = useMemo(
    () => planFor(discovery.data?.candidates ?? [], chosen, consented),
    [discovery.data?.candidates, chosen, consented],
  );

  const selected = useMemo(
    () => new Set(plan.filter((step) => !excluded.has(step.trigger)).map((step) => step.trigger)),
    [plan, excluded],
  );

  const broken = useMemo(() => handoffsBrokenBy(plan, selected), [plan, selected]);

  const chooseCandidate = (directory: string) => {
    setChosen(directory);
    setExcluded(new Set());
  };

  const toggleConsent = (tierId: string) =>
    setConsented((current) => {
      const next = new Set(current);
      if (!next.delete(tierId)) next.add(tierId);
      return next;
    });

  const toggle = (trigger: string) =>
    setExcluded((current) => {
      const next = new Set(current);
      if (!next.delete(trigger)) next.add(trigger);
      return next;
    });

  // A discovered pipeline is a list to choose from; an empty repository is not. With no rows there
  // is nothing to select, so the selection stays out of it entirely and the press means what it
  // meant before this feature existed — which is the whole reason the API reads an absent selection
  // and an empty one as different answers.
  const selecting = plan.length > 0;
  const nothingSelected = selecting && selected.size === 0;
  // With no rows at all there is nothing a press could do: no file to wire and no consent given.
  // Distinct from `nothingSelected`, which is rows that exist and were all turned off.
  const nothingToBuild = plan.length === 0;

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
            onChoose={chooseCandidate}
          />
        ) : null}

        {/* Outside the plan on purpose: an empty repository has no rows, and that is exactly the
            case a consent exists for — a control living inside the row list would be unreachable
            precisely when it matters. Offered even with no Connector, because what a consent writes
            is catalogue content rather than something read from the repository. */}
        {gatedTiers.length > 0 ? (
          <Consent tiers={gatedTiers} consented={consented} onToggle={toggleConsent} />
        ) : null}

        {discovery.data && !discovery.data.reason ? (
          <div className="flex flex-col gap-3">
            {/* The plan, before the button (#233), and since #262 a checklist rather than a
                notice: a preview a reader cannot change leaves them accepting steps they do not
                want and deleting the Automations afterwards. */}
            <Plan steps={plan} excluded={excluded} broken={broken} onToggle={toggle} />

            <div className="flex flex-wrap items-center gap-3">
              <Button
                type="button"
                disabled={setUp.isPending || nothingSelected || nothingToBuild}
                onClick={() =>
                  setUp.mutate({
                    promptDirectory: chosen ?? undefined,
                    // The rows already said which files would be written, so the decision is the
                    // press. A second consent for a preview somebody just read is a confirmation
                    // of a confirmation — the consent above asks a different question, about paths
                    // no row names.
                    installMissing: true,
                    // Absent where there is no plan to select from — never `[]`, which the API
                    // reads as "no step at all".
                    steps: selecting ? [...selected] : undefined,
                    // Sent as given: an empty set means no tier, which is what the API defaults to
                    // anyway. Unlike `steps`, there is no absent-versus-empty distinction to honour.
                    tiers: [...consented],
                  })
                }
              >
                {setUp.isPending ? t("workflowSetup.building") : t("workflowSetup.build")}
              </Button>
              {/* Beside the button, where the decision is taken — not in a paragraph above it. */}
              <p className="text-xs text-muted-foreground">{t("workflowSetup.draftSafety")}</p>
            </div>

            {nothingSelected ? (
              <p className="text-xs text-muted-foreground" role="status">
                {t("workflowSetup.nothingSelected")}
              </p>
            ) : null}

            {nothingToBuild ? (
              <p className="text-xs text-muted-foreground" role="status">
                {t("workflowSetup.nothingToBuild")}
              </p>
            ) : null}
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
      {/* Beside "Skipped" and never inside it: one means the project already had it, the other
          means you said no. Folding them together would make one count mean two things. */}
      <Fact label={t("workflowSetup.excluded")} values={report.excluded} />
      <Fact label={t("workflowSetup.found")} values={report.foundNotWired} />
      <Fact
        label={t("workflowSetup.missing")}
        values={report.missingPrompts.map((prompt) => prompt.resolvedPath ?? prompt.saveAs)}
      />
      {/* Its own fact, never folded into the prompts: these are writes outside the prompt directory,
          and an Admin who consented to prompts has to see them without opening the diff. */}
      <Fact
        label={t("workflowSetup.prerequisites")}
        values={report.installed?.prerequisites ?? []}
      />
      {/* "We wrote four of seven" only means something beside which three were already yours. */}
      <Fact
        label={t("workflowSetup.prerequisitesKept")}
        values={report.installed?.prerequisitesAlreadyPresent ?? []}
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
 *
 * Filtered by consent (#269). A step whose file is already there is always a row — wiring it reads
 * the repository and needs no permission. A step with no file is a row only once its tier is
 * consented to, because until then nothing would happen for it, and a row offering a choice that
 * changes nothing is the noise #233 kept out of this list.
 */
function planFor(
  candidates: PipelineCandidate[],
  chosen: string | null,
  consented: ReadonlySet<string>,
): PlannedStep[] {
  const candidate = chosen ? candidates.find((entry) => entry.directory === chosen) : candidates[0];

  return (candidate?.plan ?? []).filter(
    (step) => step.exists || step.installable || consented.has(step.tierId),
  );
}

/**
 * The consent (#269): off by default, with what it would write stated beside it rather than
 * discovered in the diff afterwards.
 *
 * This is not the control #262 deleted. That one asked whether to install the starters the plan rows
 * already named — a confirmation of a confirmation. This one authorises writing files *outside* the
 * prompt directory, at paths no row names, on the terms of a methodology the plan does not describe.
 */
function Consent({
  tiers,
  consented,
  onToggle,
}: {
  tiers: StarterTier[];
  consented: ReadonlySet<string>;
  onToggle: (tierId: string) => void;
}) {
  return (
    <ul className="flex flex-col gap-2">
      {tiers.map((tier) => {
        const on = consented.has(tier.id);

        return (
          <li key={tier.id} className="flex flex-col gap-2 rounded-md border border-border p-3">
            <div className="flex items-start justify-between gap-3">
              <div className="flex flex-col gap-1">
                <span className="text-sm font-semibold">{tier.title}</span>
                <p className="text-sm text-muted-foreground">{tier.summary}</p>
              </div>
              <Switch
                checked={on}
                onCheckedChange={() => onToggle(tier.id)}
                aria-label={`${t("workflowSetup.adopt")}: ${tier.title}`}
              />
            </div>

            {/* Shown whether the switch is on or off: a prerequisite an Admin cannot read before
                consenting is a prerequisite they learn from a failed Run, which is the failure the
                tiering was introduced to prevent. */}
            {tier.requires ? (
              <p className="text-xs text-muted-foreground">
                <span className="font-medium">{t("workflowSetup.adoptNeeds")}</span> {tier.requires}
              </p>
            ) : null}

            {tier.prerequisites.length > 0 ? (
              <>
                <ul className="flex flex-col gap-0.5">
                  {tier.prerequisites.map((path) => (
                    <li key={path} className="font-mono text-[11.5px] text-muted-foreground">
                      {path}
                    </li>
                  ))}
                </ul>
                {/* The precise claim, not the optimistic one: nothing was read to produce this list,
                    so it says where the files go and on what condition — and the report afterwards
                    says which ones were actually written. */}
                <p className="text-xs text-muted-foreground">
                  {t("workflowSetup.adoptWritesWhereAbsent")}
                </p>
              </>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * One row per step the build would create (#233). It replaced prose and a checkbox: what the click
 * would do was a surprise, and the per-step detail existed only in the report afterwards — which is
 * the wrong side of an action that writes to somebody's repository.
 */
function Plan({
  steps,
  excluded,
  broken,
  onToggle,
}: {
  steps: PlannedStep[];
  excluded: ReadonlySet<string>;
  broken: ReadonlySet<string>;
  onToggle: (trigger: string) => void;
}) {
  const [expanded, setExpanded] = useState(false);

  if (steps.length === 0) return null;

  // Long pipelines collapse: a plan that fills the screen stops being read, which defeats it.
  const visible = expanded ? steps : steps.slice(0, 3);
  const hidden = steps.length - visible.length;

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold">{t("workflowSetup.planTitle")}</h3>
      <ul className="divide-y divide-border rounded-lg border border-border">
        {visible.map((step) => {
          const off = excluded.has(step.trigger);

          return (
            <li key={step.trigger} className="flex flex-wrap items-center gap-2.5 px-3.5 py-2">
              {/* An excluded row stays legible — it is excluded, not gone, and a reader has to be
                  able to see what they turned off in order to turn it back on. */}
              <input
                type="checkbox"
                className="size-3.5 shrink-0 accent-primary"
                checked={!off}
                onChange={() => onToggle(step.trigger)}
                aria-label={`${t("workflowSetup.includeStep")} ${step.trigger}`}
              />
              <span
                className={`w-28 shrink-0 font-mono text-[11.5px] font-semibold ${
                  off ? "text-muted-foreground line-through" : "text-primary"
                }`}
              >
                {step.trigger}
              </span>
              <span className="min-w-0 flex-1 text-xs text-muted-foreground">
                {t("workflowSetup.wireTo")} <span className="font-mono">{step.promptFile}</span>
                {/* What this step will claim (#310). The plan said "hands on to a label" while the
                    model still had one; it says which transition now, because that is what installing
                    the tier creates — and the stages come into existence as a consequence of it. */}
                {step.toStage ? (
                  <>
                    {" · "}
                    {t("workflowSetup.movesTo")} <span className="font-mono">{step.toStage}</span>
                  </>
                ) : (
                  <>
                    {" · "}
                    {t("workflowSetup.flowEnds")}
                  </>
                )}
                {step.holds ? (
                  <>
                    {" · "}
                    <b className="text-warning-foreground">{t("workflowSetup.gate")}</b>
                  </>
                ) : null}
                {/* Information, never a blocker: a workflow where a person hands on is a workflow
                    this product already supports. */}
                {broken.has(step.trigger) ? (
                  <>
                    {" · "}
                    <b className="text-warning-foreground">{t("workflowSetup.handoffBroken")}</b>
                  </>
                ) : null}
              </span>
              <Badge variant={step.exists ? "secondary" : "outline"}>
                {step.exists ? t("workflowSetup.exists") : t("workflowSetup.installStarter")}
              </Badge>
            </li>
          );
        })}
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
