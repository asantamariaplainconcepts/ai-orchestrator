/** One Run waiting on a human (UC-026). `waitingFor` is the reason vocabulary, not the enum. */
export interface InboxEntry {
  runId: string;
  projectId: string;
  /** The list is cross-project, so every row names its project; null when it no longer exists. */
  projectName: string | null;
  /** Null for a change-targeted Run (run-on-a-pr): its identity is the change fields below. */
  vendorStoryId: string | null;
  storyTitle: string | null;
  waitingFor: "approval" | "input" | "failure";
  waitingSince: string;
  /** The change a change-targeted Run updates; null for story Runs. */
  changeNumber: number | null;
  changeTitle: string | null;
}

/** One open change (pull request) awaiting review — a different kind of wait from a Run's. */
export interface InboxChange {
  projectId: string;
  projectName: string | null;
  number: number;
  title: string;
  url: string;
  createdAt: string;
  /** Set when the change is the product's own — its URL matches a Run's recorded output link. */
  runId: string | null;
}

/** Refusals arrive apart from entries, so one bad Connector never blanks the group. */
export interface InboxChanges {
  changes: InboxChange[];
  refusals: { projectId: string; projectName: string | null; reason: string }[];
}
