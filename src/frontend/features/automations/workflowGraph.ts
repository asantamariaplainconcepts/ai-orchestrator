import type { Automation } from "./types";

export interface WorkflowNode {
  automation: Automation;
  /** The Automation this one hands work to, or null when the chain stops here. */
  next: Automation | null;
}

/**
 * The pipeline, derived (design D1): an edge exists exactly where one Automation's output label
 * equals another's trigger label. Nothing about the shape is stored, so the picture cannot claim
 * a chain that would not fire.
 * <p>
 * Chains, not a general graph: #115 allows one output label per Automation, so out-degree is at
 * most one and every component is a path. Roots come first — an Automation nobody hands to is
 * where a reader starts — and each chain is walked from there.
 * </p>
 */
export function buildChains(automations: Automation[]): WorkflowNode[][] {
  const byTrigger = new Map<string, Automation>();
  for (const automation of automations) {
    // Two Automations can share a trigger label only if one is disabled (BR-003), so the first
    // enabled one is the edge's real destination.
    const held = byTrigger.get(automation.triggerLabel);
    if (!held || (!held.enabled && automation.enabled)) {
      byTrigger.set(automation.triggerLabel, automation);
    }
  }

  const nextOf = (automation: Automation): Automation | null => {
    const target = automation.outputLabel ? byTrigger.get(automation.outputLabel) : undefined;
    // An output label pointing at no Automation is not an edge — it is a label the vendor will
    // carry and nobody will answer, which the node states rather than the graph hiding.
    return target && target.id !== automation.id ? target : null;
  };

  const handedTo = new Set<string>();
  for (const automation of automations) {
    const target = nextOf(automation);
    if (target) handedTo.add(target.id);
  }

  const chains: WorkflowNode[][] = [];
  const placed = new Set<string>();

  // Roots first: nobody hands to them, so they are where a chain begins.
  for (const root of automations.filter((candidate) => !handedTo.has(candidate.id))) {
    chains.push(walk(root));
  }

  // Whatever is left is part of a cycle (#115 refuses self-triggers, not longer loops), so it
  // has no root. Shown as its own chain rather than silently omitted.
  for (const orphan of automations) {
    if (!placed.has(orphan.id)) chains.push(walk(orphan));
  }

  return chains;

  function walk(start: Automation): WorkflowNode[] {
    const chain: WorkflowNode[] = [];
    let current: Automation | null = start;

    while (current && !placed.has(current.id)) {
      placed.add(current.id);
      const next: Automation | null = nextOf(current);
      chain.push({ automation: current, next });
      current = next;
    }

    return chain;
  }
}

/**
 * The chains that are actually a workflow (#136, design D2).
 *
 * Membership is one sentence: an Automation is in the workflow exactly when it has an edge — it
 * hands work to another, or another hands to it. Expressed through the chains, that is simply a
 * chain with more than one node, because `buildChains` walks a root plus everything reachable from
 * it, so a length of one means nothing arrives and nothing leaves.
 *
 * This is what removes the special case #122 was reaching for. `estimate` is not at the end of the
 * pipeline; it is not in the pipeline, and its absence is not an omission to explain.
 *
 * Deliberately derived, never stored: a flag saying "in the workflow" could disagree with the
 * edges, and then the picture would claim a chain that would not fire.
 */
export function workflowChains(automations: Automation[]): WorkflowNode[][] {
  return buildChains(automations).filter((chain) => chain.length > 1);
}

export interface WorkflowSummary {
  /** Nodes across every chain — steps, not Automations, which is a different number. */
  steps: number;
  /** How many times the flow stops for a person: a gate on a step, or a chain that breaks. */
  humanStops: number;
}

/**
 * How big the flow is (design D4). "6 Automations" is a fact about the catalogue and says nothing
 * about the pipeline; these two are what somebody wants before reading the diagram.
 */
export function summarise(chains: WorkflowNode[][]): WorkflowSummary {
  const nodes = chains.flat();

  return {
    steps: nodes.length,
    humanStops: nodes.filter(
      // Two shapes of the same wait: a step that asks for approval, and a step that hands to
      // nobody while sitting inside a chain — somebody has to carry the work onward.
      (node) => node.automation.requiresApproval || node.next === null,
    ).length,
  };
}
