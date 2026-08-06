import type { PlannedStep } from "./useWorkflowSetup";

/**
 * #262 — which still-selected steps stopped being handed work, because the step that used to hand
 * to them was excluded.
 *
 * <p>
 * Deliberately **not** `buildChains`. That function answers a different question over a different
 * type: it walks created Automations, needing an id and an enabled flag, to lay out the canvas.
 * Plan rows have neither, and bending them into that shape to share code would be the larger
 * coupling. What is shared is the rule, restated here: <b>A hands to B when one of A's output
 * labels is B's trigger.</b>
 * </p>
 *
 * <p>
 * One thing is deliberately not carried across. `buildChains` matches labels through a plain `Map`,
 * which is case-sensitive; the product identity is case-insensitive (BR-003, DEC-056). The canvas
 * gets away with it because both sides come from one catalogue. This path should not inherit a
 * latent bug, so it folds case.
 * </p>
 *
 * <p>
 * A step is reported only when it <i>had</i> a hand-off and has none left: losing one of two
 * providers still leaves it fed, and marking it then would be a warning about nothing. A step
 * nobody ever handed to is not orphaned either — it is a start, which is how `ai:triage` reads.
 * </p>
 */
export function handoffsBrokenBy(
  plan: PlannedStep[],
  selected: ReadonlySet<string>,
): ReadonlySet<string> {
  const fold = (label: string) => label.toLowerCase();
  const isSelected = (trigger: string) => selected.has(trigger);

  /** Every step that hands to this trigger, whether or not it survived the selection. */
  const providersOf = new Map<string, PlannedStep[]>();
  for (const step of plan) {
    for (const label of step.outputLabels) {
      const target = fold(label);
      // A label naming no step in the plan is not a hand-off — it is a label the vendor will carry
      // and nobody will answer, which is not this function's warning to give.
      if (!plan.some((candidate) => fold(candidate.trigger) === target)) continue;
      if (fold(step.trigger) === target) continue;

      providersOf.set(target, [...(providersOf.get(target) ?? []), step]);
    }
  }

  const orphaned = new Set<string>();
  for (const step of plan) {
    if (!isSelected(step.trigger)) continue;

    const providers = providersOf.get(fold(step.trigger)) ?? [];
    if (providers.length === 0) continue;
    if (providers.some((provider) => isSelected(provider.trigger))) continue;

    orphaned.add(step.trigger);
  }

  return orphaned;
}
