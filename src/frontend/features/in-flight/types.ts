import type { RunStateName } from "@/shared/ui/state-chip";

/** One live Run. `state` is the API's enum name; the copy for it resolves in the shared chip. */
export interface InFlightRun {
  runId: string;
  state: RunStateName;
  createdAt: string;
}

/**
 * One node under a project — the subject its Runs belong to (UC-033).
 *
 * A Run targets exactly one of a Story or an open change, never both and never neither, so exactly
 * one of `vendorStoryId` and `changeNumber` is set. `held` is a fact about the Story rather than
 * about any Run: it is true for a held Story with no Run at all, which is most of what this surface
 * adds over the per-project Runs list.
 */
export interface InFlightWork {
  vendorStoryId: string | null;
  title: string | null;
  held: boolean;
  changeNumber: number | null;
  runs: InFlightRun[];
}

/** Only projects with live work appear here — a quiet one is absent, not empty. */
export interface InFlightProject {
  projectId: string;
  projectName: string | null;
  work: InFlightWork[];
}

export interface InFlightView {
  projects: InFlightProject[];
}
