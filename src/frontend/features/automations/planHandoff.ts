import type { PlannedStep } from "./useWorkflowSetup";

/**
 * #262, restated for #310 — which still-selected steps stopped having anybody move work into them,
 * because the step that used to do so was excluded.
 *
 * <p>
 * The question is the same; what answers it changed. It used to be "which selected steps lost the
 * output label that fed them", walked over `outputLabels`; it is now "which selected steps' from-stage
 * nobody claims a transition into", read straight off the plan's own claims. That is a lookup rather
 * than a walk, and it is the only place in the setup card the flow is asked about at all.
 * </p>
 *
 * <p>
 * It keeps operating on <b>uncreated</b> plan rows, which have no ids and no enabled flag — that is why
 * this is not the board's `claimantsByToStage` with a different argument, and any design needing an id
 * here would be wrong. What is shared is the rule, restated: <b>A moves work into B when A's claimed
 * to-stage is B's trigger.</b>
 * </p>
 *
 * <p>
 * Its deliberate case folding is now the norm rather than the exception. `buildChains` used to compare
 * through a plain `Map`, which is case-sensitive, while product identity is case-insensitive (BR-003,
 * DEC-056); this path folded case to avoid inheriting that latent bug. Every comparison in the new code
 * folds case, so the exception has become the rule and this file no longer stands apart.
 * </p>
 *
 * <p>
 * A step is reported only when it <i>had</i> a provider and has none left: a step nobody ever moved work
 * into is not orphaned, it is a start. Under a linear lifecycle there is at most one provider, so
 * "losing one of two" can no longer arise — the clause that handled it is gone with branching.
 * </p>
 */
export function handoffsBrokenBy(
  plan: PlannedStep[],
  selected: ReadonlySet<string>,
): ReadonlySet<string> {
  const fold = (label: string) => label.toLowerCase();

  /** The step claiming the transition into each trigger, whether or not it survived the selection. */
  const providerOf = new Map<string, PlannedStep>();
  for (const step of plan) {
    if (!step.toStage) continue;
    const target = fold(step.toStage);
    // A to-stage naming no step in the plan is not a hand-off inside this plan — it is a stage this
    // installation does not fill, which is not this function's warning to give.
    if (!plan.some((candidate) => fold(candidate.trigger) === target)) continue;
    if (fold(step.trigger) === target) continue;

    if (!providerOf.has(target)) providerOf.set(target, step);
  }

  const orphaned = new Set<string>();
  for (const step of plan) {
    if (!selected.has(step.trigger)) continue;

    const provider = providerOf.get(fold(step.trigger));
    if (provider === undefined) continue;
    if (selected.has(provider.trigger)) continue;

    orphaned.add(step.trigger);
  }

  return orphaned;
}
