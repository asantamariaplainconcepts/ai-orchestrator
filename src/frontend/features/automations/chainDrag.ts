import type { Automation } from "./types";
import { fold } from "./workflowGraph";

/**
 * What a drag carries when an Automation is being moved to a boundary of the lifecycle (#310).
 *
 * A custom type rather than `text/plain` so a boundary can tell this gesture from any other drag
 * crossing it — the board also drags Story cards, and a column must not confuse the two.
 */
export const AUTOMATION_BLOCK = "application/x-aio-automation";

/**
 * Why a drop cannot happen, or null when it can. Computed at the boundary the pointer is over and
 * rendered there, because a refusal that arrives as a toast after the drop teaches the rule one
 * gesture too late.
 *
 * <p>
 * Two of the four this used to compute are gone. The loop refusal went to nothing — a lifecycle is a
 * linear ordered list, so no arrangement leads back to where it started — and `already` became "this
 * boundary is where it already is", which is what it says below.
 * </p>
 * <p>
 * <b>Where this differs from design D6, deliberately.</b> D6 expected `self` to become impossible by
 * construction too. It does not: the board can express it. Dropping an Automation onto the boundary
 * <i>into its own trigger label</i> asks for a to-stage equal to the from-stage, which is the
 * self-trigger loop #115 refuses — a Run that succeeds, writes its own trigger, and is then declined
 * by BR-003 because a Run is already active, leaving a labelled Story and no work. So the explanation
 * stays, and the server's refusal stays the enforcement.
 * </p>
 */
export type DropRefusal = "self" | "already" | "shared";

/**
 * The one boundary of a lifecycle: the transition into <c>To</c>.
 *
 * <c>From</c> is null for the boundary before the <b>first</b> stage — there is nothing before it, so a
 * claim there does not name an existing from-stage; the Automation's own trigger label becomes one, and
 * that is how a step gets placed first (AC 4).
 */
export interface Boundary {
  from: string | null;
  to: string;
}

/**
 * What assigning any Automation to <c>boundary</c> would store.
 *
 * The interesting part, and the reason this is a function rather than a line at each call site. A
 * claim's from-stage <b>is</b> the Automation's trigger label (design D2), so moving a step to a
 * different boundary is not only a change of to-stage: the step now fires at the stage it was moved to.
 * Both fields travel, which is what makes AC 5's reorder expressible at all — and what makes AC 6's
 * refusal fire from BR-003 rather than from a rule invented for this screen, since two enabled
 * Automations cannot share the from-stage they would both now trigger on.
 *
 * At the leading boundary the trigger is left alone: there is no from-stage to adopt, and the
 * Automation's own trigger is what becomes the new first stage.
 */
export function claimPatch(boundary: Boundary): { triggerLabel?: string; toStage: string } {
  return boundary.from === null
    ? { toStage: boundary.to }
    : { triggerLabel: boundary.from, toStage: boundary.to };
}

/**
 * The rule that stops assigning <c>dragged</c> to <c>boundary</c>, or null when nothing does.
 *
 * This is the explanation, never the enforcement (design D5). Every one of these is also refused by
 * the update endpoint — the self-trigger validator, BR-003's overlap check in memory, and the
 * expression index underneath both — and that is where the guarantee lives. Saying it here is so the
 * refusal arrives before the gesture rather than after it.
 */
export function refusalFor(
  dragged: Automation,
  boundary: Boundary,
  automations: Automation[],
): DropRefusal | null {
  const patch = claimPatch(boundary);
  const from = patch.triggerLabel ?? dragged.triggerLabel;

  // A step cannot hand work to itself: its from-stage *is* its trigger label, so a to-stage equal to
  // it is a loop of one. Reachable at the leading boundary, where the trigger is not rewritten.
  if (fold(from) === fold(patch.toStage)) {
    return "self";
  }

  // The claim is already here. Not harmful, but the boundary would claim to do something and do
  // nothing, which is worse than saying so.
  if (fold(dragged.triggerLabel) === fold(from) && fold(dragged.toStage) === fold(patch.toStage)) {
    return "already";
  }

  // BR-003: at most one enabled Automation per from-stage. After this assignment `dragged` would fire
  // on `from`, so anybody else already firing there is the refusal — and it is the same refusal the
  // endpoint gives, which is what names the Automation already claiming the transition (AC 6).
  const sharesTrigger = automations.some(
    (candidate) =>
      candidate.id !== dragged.id &&
      candidate.enabled &&
      dragged.enabled &&
      fold(candidate.triggerLabel) === fold(from),
  );

  return sharesTrigger ? "shared" : null;
}
