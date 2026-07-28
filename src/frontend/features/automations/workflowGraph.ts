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
