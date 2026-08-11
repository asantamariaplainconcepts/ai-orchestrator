import type { Automation } from "./types";

/**
 * What a drag carries when an Automation is being chained (design review turn 8). Distinct from
 * the human block's type so a gap can tell the two gestures apart before either lands — the block
 * removes an edge, this one rewires two.
 */
export const AUTOMATION_BLOCK = "application/x-aio-automation";

/**
 * Why a drop cannot happen, or null when it can (8c). Computed at the slot the pointer is over
 * and rendered there, because a refusal that arrives as a toast after the drop teaches the rule
 * one gesture too late.
 * <p>
 * <b>The loop refusal is gone (#310, design D6).</b> It was not simplified — there is nothing left
 * for it to compute. A cycle is a property of a graph, and a project's lifecycle is a linear ordered
 * list of stages: an Automation claims the transition out of one stage into the next, so no
 * arrangement a person can express can lead back to where it started. Removing a warning is a
 * judgement, so it is this commit's whole subject rather than a line inside a larger one.
 * </p>
 */
export type DropRefusal = "self" | "already" | "shared";

/**
 * The wiring a drop would perform, in the two labels it rewrites — or the reason it will not.
 *
 * Kept apart from the canvas on purpose. The rules are the interesting part and they are pure
 * functions of the automations, so they can be exercised without rendering anything and without
 * an HTML5 drag, which Playwright cannot perform (#110 recorded that, and the human block's
 * gesture is untested for exactly this reason).
 */
export interface ChainDrop {
  /** The step the dropped Automation lands after. */
  preceding: Automation;
  /** The step it lands before, or null when it is being chained onto the end. */
  following: Automation | null;
  dragged: Automation;
}

/**
 * The rule that stops this drop, or null when nothing does.
 *
 * Deliberately narrow: these are the ones a drop can create by itself. Everything else a drop
 * might violate is caught where it already is — the update endpoint applies BR-003's overlap check
 * and #115's self-trigger refusal to whatever this produces (design D4), so this function is the
 * explanation, never the enforcement.
 */
export function refusalFor(drop: ChainDrop, automations: Automation[]): DropRefusal | null {
  const { preceding, following, dragged } = drop;

  // A step cannot hand work to itself, and dropping a step into its own slot is that.
  if (dragged.id === preceding.id || dragged.id === following?.id) {
    return "self";
  }

  // The edge is already there. Not harmful, but the slot would claim to do something and do
  // nothing, which is worse than saying so.
  if (preceding.outputLabels.includes(dragged.triggerLabel)) {
    return "already";
  }

  // Two enabled Automations sharing a trigger is BR-003's refusal, and an edge into an ambiguous
  // label is worse than the refusal: the picture would show one destination while the executor
  // picks another.
  const sharesTrigger = automations.some(
    (candidate) =>
      candidate.id !== dragged.id &&
      candidate.enabled &&
      dragged.enabled &&
      candidate.triggerLabel === dragged.triggerLabel,
  );
  if (sharesTrigger) {
    return "shared";
  }

  return null;
}

/** One Automation's new output labels, as a drop would leave them. */
export interface LabelRewrite {
  automation: Automation;
  outputLabels: string[];
}

/**
 * What a drop rewrites (design D1: the graph stays derived, so a gesture can only ever change
 * labels). Between two steps that is two rewrites; onto the end it is one.
 *
 * Returns the rewrites rather than performing them, so the caller owns the update — which is what
 * keeps every canvas gesture an ordinary Automation update (design D4).
 */
export function rewritesFor(drop: ChainDrop): LabelRewrite[] {
  const { preceding, following, dragged } = drop;

  const rewrites: LabelRewrite[] = [
    {
      automation: preceding,
      outputLabels: [
        // The edge this slot replaces goes; every other hand-off this step has is left alone,
        // because a step handing to three places must not lose two of them to a drop (#165).
        ...preceding.outputLabels.filter((label) => label !== following?.triggerLabel),
        dragged.triggerLabel,
      ],
    },
  ];

  if (following) {
    rewrites.push({
      automation: dragged,
      outputLabels: dragged.outputLabels.includes(following.triggerLabel)
        ? dragged.outputLabels
        : [...dragged.outputLabels, following.triggerLabel],
    });
  }

  return rewrites;
}

/**
 * Taking a step out of the chain (8a): whoever hands to it stops doing so. Nothing is invented in
 * its place — an absence has no two ends (design D2), so the gap is left open with its existing
 * control as the way to close it, exactly as the human block already behaves.
 */
export function removalRewrites(dragged: Automation, automations: Automation[]): LabelRewrite[] {
  return automations
    .filter((candidate) => candidate.outputLabels.includes(dragged.triggerLabel))
    .map((candidate) => ({
      automation: candidate,
      outputLabels: candidate.outputLabels.filter((label) => label !== dragged.triggerLabel),
    }));
}
