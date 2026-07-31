import type { Automation } from "./types";

export interface WorkflowNode {
  automation: Automation;
  /** The Automation this one hands work to along this row, or null when the row stops here. */
  next: Automation | null;
  /**
   * The other Automations this one hands to (#165). They are real edges — each has its own row,
   * beginning at the target — and this is what lets the node say so where the reader is looking.
   */
  branches: Automation[];
}

export interface WorkflowChain {
  nodes: WorkflowNode[];
  /**
   * The Automation this row branched off, or null for a row that begins on its own. A branch row
   * exists because an edge points into it, so it opens by naming where that edge came from —
   * otherwise the second edge would read as an unrelated chain.
   */
  branchedFrom: Automation | null;
}

/**
 * The pipeline, derived (design D1): an edge exists exactly where one Automation's output label
 * equals another's trigger label. Nothing about the shape is stored, so the picture cannot claim
 * a chain that would not fire.
 * <p>
 * Since #165 an Automation can hand on to several, so out-degree is no longer at most one and this
 * is a graph rather than a set of paths. It is still <b>rendered</b> as rows, which is the layout
 * the design contract fixes: the first matching label continues the row, and every further match
 * opens a row of its own that names the node it left. Two edges, two rows, one reading direction —
 * rather than a second arrow crossing a line the contract says must not wrap.
 * </p>
 * <p>
 * What the rows must not imply is that branches run at once. BR-001 allows one active Run per
 * Story, so a second simultaneous match is ignored rather than queued; the canvas says that in
 * words beside the flow, because a picture of two branches cannot say it by itself.
 * </p>
 */
export function buildChains(automations: Automation[]): WorkflowChain[] {
  const byTrigger = new Map<string, Automation>();
  for (const automation of automations) {
    // Two Automations can share a trigger label only if one is disabled (BR-003), so the first
    // enabled one is the edge's real destination.
    const held = byTrigger.get(automation.triggerLabel);
    if (!held || (!held.enabled && automation.enabled)) {
      byTrigger.set(automation.triggerLabel, automation);
    }
  }

  /** Every Automation this one hands to, in the order its labels were named. */
  const targetsOf = (automation: Automation): Automation[] => {
    const seen = new Set<string>();
    return (
      automation.outputLabels
        .map((label) => byTrigger.get(label))
        // An output label pointing at no Automation is not an edge — it is a label the vendor will
        // carry and nobody will answer, which the node states rather than the graph hiding.
        .filter(
          (target): target is Automation =>
            target !== undefined &&
            target.id !== automation.id &&
            seen.add(target.id) !== undefined,
        )
    );
  };

  const handedTo = new Set<string>();
  for (const automation of automations) {
    for (const target of targetsOf(automation)) handedTo.add(target.id);
  }

  const chains: WorkflowChain[] = [];
  const placed = new Set<string>();
  // Branch rows are discovered while walking and drawn after the row that produced them, so a
  // reader meets a branch below the step it leaves rather than before it.
  const pending: { start: Automation; from: Automation }[] = [];

  // Roots first: nobody hands to them, so they are where a chain begins.
  for (const root of automations.filter((candidate) => !handedTo.has(candidate.id))) {
    chains.push({ nodes: walk(root), branchedFrom: null });
    drainBranches();
  }

  // Whatever is left is part of a cycle (#115 refuses self-triggers, not longer loops), so it
  // has no root. Shown as its own chain rather than silently omitted.
  for (const orphan of automations) {
    if (!placed.has(orphan.id)) {
      chains.push({ nodes: walk(orphan), branchedFrom: null });
      drainBranches();
    }
  }

  return chains.filter((chain) => chain.nodes.length > 0);

  function drainBranches() {
    while (pending.length > 0) {
      const branch = pending.shift()!;
      if (placed.has(branch.start.id)) continue;
      chains.push({ nodes: walk(branch.start), branchedFrom: branch.from });
    }
  }

  function walk(start: Automation): WorkflowNode[] {
    const chain: WorkflowNode[] = [];
    let current: Automation | null = start;

    while (current && !placed.has(current.id)) {
      placed.add(current.id);
      const targets = targetsOf(current);
      // The first target keeps the row; the rest become rows of their own. Which one is "first" is
      // the order the Admin named the labels in — display order and nothing more, because the
      // labels come back as vendor deliveries and are matched then (design D3).
      const [next = null, ...branches] = targets;
      for (const branch of branches) pending.push({ start: branch, from: current });
      chain.push({ automation: current, next, branches });
      current = next;
    }

    return chain;
  }
}

/**
 * The chains that are actually a workflow (#136, design D2).
 *
 * Membership is one sentence: an Automation is in the workflow exactly when it has an edge — it
 * hands work to another, or another hands to it. Expressed through the rows, that is a row with
 * more than one node, or a row that exists because something branched into it — a branch row of one
 * node is still an edge, and dropping it would hide the second hand-off (#165).
 *
 * This is what removes the special case #122 was reaching for. `estimate` is not at the end of the
 * pipeline; it is not in the pipeline, and its absence is not an omission to explain.
 *
 * Deliberately derived, never stored: a flag saying "in the workflow" could disagree with the
 * edges, and then the picture would claim a chain that would not fire.
 */
export function workflowChains(automations: Automation[]): WorkflowChain[] {
  return buildChains(automations).filter(
    (chain) => chain.nodes.length > 1 || chain.branchedFrom !== null,
  );
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
export function summarise(chains: WorkflowChain[]): WorkflowSummary {
  const nodes = chains.flatMap((chain) => chain.nodes);

  return {
    steps: nodes.length,
    humanStops: nodes.filter(
      // Two shapes of the same wait: a step that asks for approval, and a step that hands to
      // nobody while sitting inside a chain — somebody has to carry the work onward.
      (node) => node.automation.requiresApproval || node.next === null,
    ).length,
  };
}

/** True when some step hands on to more than one place — what the BR-001 note is for. */
export function hasBranches(chains: WorkflowChain[]): boolean {
  return chains.some((chain) => chain.nodes.some((node) => node.branches.length > 0));
}
