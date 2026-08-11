import type { Automation } from "./types";

/**
 * What is left of the graph derivation (#310, design D6).
 *
 * There was a `buildChains` here: it walked output labels against trigger labels, laid the result out
 * as rows, opened a row per branch, and named the node each branch left. Nothing about the shape was
 * stored, so the picture was recomputed at every read site — six of them, and they disagreed.
 *
 * A project's lifecycle is now stored and served in order (`useLifecycle`), and each Automation stores
 * the one transition it claims. So there is no graph to derive: what a surface needs is the stage list
 * it was given, plus the answer to "who claims the transition into this stage?" — which is a lookup,
 * not a walk. `WorkflowChain`, `branches`, `branchedFrom` and `hasBranches` are gone with the
 * derivation, because branching is unrepresentable (AC 13): an Automation names at most one to-stage,
 * so nothing can hand on to two places and no view can draw a second edge.
 *
 * Every comparison here folds case. The vendor treats `AI:Implement` and `ai:implement` as one label
 * (DEC-056), BR-003's identity already does, and the `Map` this file used to compare through did not —
 * a latent bug that would have dropped edges the old canvas drew.
 */
export function fold(label: string | null | undefined): string {
  return (label ?? "").trim().toLowerCase();
}

/**
 * The Automation claiming the transition **into** each stage, keyed by the folded stage name.
 *
 * Keyed by the to-stage rather than the from-stage, because that is the boundary a reader points at:
 * the transition into `s2` is the boundary drawn between `s1` and `s2`, and the Automation claiming it
 * is the one whose `toStage` is `s2`. Its from-stage is its own trigger label, so the pair needs no
 * second field and this map needs no second key.
 *
 * Only enabled Automations. A disabled one claims nothing that will fire, and drawing it on a boundary
 * would say work moves where it does not; BR-003 permits two Automations to share a trigger only when
 * one is off, so the enabled one is always the real claimant.
 */
export function claimantsByToStage(automations: Automation[]): ReadonlyMap<string, Automation> {
  const claimants = new Map<string, Automation>();

  for (const automation of automations) {
    if (!automation.enabled || !automation.toStage) continue;
    const key = fold(automation.toStage);
    // First wins. BR-003 makes a second enabled claimant of the same from-stage a refused save, so
    // two here means a to-stage reached from two different from-stages — which the adjacency guard
    // refuses, and which a board must not silently pick a winner for without saying so.
    if (!claimants.has(key)) claimants.set(key, automation);
  }

  return claimants;
}

/**
 * The Automations that are on the flow: those whose trigger label is a stage of the lifecycle.
 *
 * One sentence, and it is the same sentence the board draws — a stage is a place a Story can be, and
 * an Automation whose trigger is a stage is a step of the flow. Everything else is a trigger somebody
 * applies on its own (DEC-053's `ai:estimate`), which belongs to the catalogue and to no boundary.
 *
 * Read from the stored list rather than derived from the edges, which is the whole of ADR-0022: a
 * membership computed here could disagree with the order stored there, and then the rail would claim a
 * flow the board does not draw.
 */
export function workflowMembers(
  stages: readonly string[],
  automations: Automation[],
): ReadonlySet<string> {
  const onFlow = new Set(stages.map(fold));

  return new Set(
    automations
      .filter((automation) => onFlow.has(fold(automation.triggerLabel)))
      .map((automation) => automation.id),
  );
}

export interface WorkflowSummary {
  /** Stages in the lifecycle — places a Story can be, which is a different number from Automations. */
  stages: number;
  /** How many times the flow waits for a person: an unclaimed boundary, or a gate on a claimed one. */
  humanStops: number;
}

/**
 * How big the flow is, and how often it stops for somebody (design D4, restated over the stored list).
 * "6 Automations" is a fact about the catalogue and says nothing about the flow; these two are what
 * somebody wants before reading it.
 */
export function summarise(stages: readonly string[], automations: Automation[]): WorkflowSummary {
  const claimants = claimantsByToStage(automations);

  // A boundary per stage: the transition *into* it, including the one into the first stage, which is
  // how a step gets placed first (AC 4). An unclaimed boundary is a person's turn (BR-006); a claimed
  // one that gates its plan is the other wait, and both are stops.
  const humanStops = stages.filter((stage) => {
    const claimant = claimants.get(fold(stage));
    return claimant === undefined || claimant.requiresApproval;
  }).length;

  return { stages: stages.length, humanStops };
}
