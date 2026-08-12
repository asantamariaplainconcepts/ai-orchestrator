import {
  CircleCheck,
  CircleSlash,
  CircleX,
  Clock,
  Hand,
  Loader,
  MessageCircleQuestion,
  NotebookPen,
  ShieldCheck,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { t } from "@/shared/i18n";
import { cn } from "@/shared/lib/utils";
import { Badge } from "@/shared/ui/badge";

/**
 * The one vocabulary for "what this Run is doing" (#335), and the hold beside it.
 *
 * Shared for the reason `gate-chip.tsx` gives for its own existence: two chips that merely look
 * alike drift the first time one is restyled, and the design gate cannot catch it because the tokens
 * are right in both. It replaces two local mappings that had already drifted — the Runs list painted
 * `Succeeded`, `Executing` and `Planning` one green, so "running now" and "finished" were
 * indistinguishable, and both it and the board rendered the raw state enum as user-facing copy in at
 * least one branch (the board's fallback, which is the branch `Queued` took).
 *
 * Glyph beside a word, never colour alone — the rule `locus.tsx` states for the locus vocabulary,
 * and the reason every state here survives greyscale.
 */

/** The Run states the API can report, as the enum names it sends. */
export type RunStateName =
  | "Queued"
  | "Planning"
  | "AwaitingApproval"
  | "Executing"
  | "Succeeded"
  | "Failed"
  | "Cancelled"
  | "AwaitingInput";

type Appearance = {
  icon: LucideIcon;
  label: string;
  /** Semantic classes from the theme; `null` means the badge's own outline variant. */
  className: string | null;
  /** Motion belongs to the one state that is actually moving. */
  animate?: boolean;
};

function appearanceOf(state: RunStateName): Appearance {
  switch (state) {
    case "Queued":
      // Dispatched but not started: a wait on the machine, and the tree's most common row.
      return { icon: Clock, label: t("state.run.queued"), className: null };
    case "Executing":
      return {
        icon: Loader,
        label: t("state.run.executing"),
        className: "bg-info text-info-foreground",
        animate: true,
      };
    case "AwaitingInput":
      return {
        icon: MessageCircleQuestion,
        label: t("state.run.awaitingInput"),
        className: "bg-warning text-warning-foreground",
      };
    case "Succeeded":
      return {
        icon: CircleCheck,
        label: t("state.run.succeeded"),
        className: "bg-success text-success-foreground",
      };
    case "Failed":
      return {
        icon: CircleX,
        label: t("state.run.failed"),
        className: "bg-destructive text-destructive-foreground",
      };
    case "Cancelled":
      return { icon: CircleSlash, label: t("state.run.cancelled"), className: null };
    // The two DEC-067 retired: unreachable for new Runs, still recorded on old ones, so they need
    // an appearance or those Runs would render as nothing. Each keeps its own — lumping a retired
    // state in with a live one is the defect this component was extracted to fix.
    case "Planning":
      return { icon: NotebookPen, label: t("state.run.planning"), className: null };
    case "AwaitingApproval":
      return {
        icon: ShieldCheck,
        label: t("state.run.awaitingApproval"),
        className: "bg-warning text-warning-foreground",
      };
  }
}

function Chip({ appearance }: { appearance: Appearance }) {
  const Icon = appearance.icon;
  return (
    <Badge
      variant={appearance.className === null ? "outline" : "default"}
      className={cn("gap-1", appearance.className)}
    >
      <Icon aria-hidden="true" className={cn("size-3", appearance.animate && "animate-spin")} />
      {appearance.label}
    </Badge>
  );
}

/**
 * A Run's state. Exhaustive over {@link RunStateName}, so a state added to the API cannot render as
 * a blank or as its own enum name — the compiler names the omission instead.
 */
export function RunStateChip({ state }: { state: RunStateName }) {
  return <Chip appearance={appearanceOf(state)} />;
}

/**
 * The hold: a person must act before anything else does (BR-007, DEC-067).
 *
 * Its own export rather than a member of {@link RunStateName}, because the hold is a fact about a
 * **Story** and not a Run state — typing it as one would make the union lie. It renders through the
 * same primitive, which is what "one vocabulary" actually requires.
 *
 * Distinct from `Executing` by glyph and word, not merely by colour: DEC-067 makes the hold a wait
 * on a *person* and execution a wait on a *machine*, and a panel that renders them alike answers
 * "what needs me?" wrongly.
 */
export function HoldChip() {
  return (
    <Chip
      appearance={{
        icon: Hand,
        label: t("state.story.held"),
        className: "bg-warning text-warning-foreground",
      }}
    />
  );
}
