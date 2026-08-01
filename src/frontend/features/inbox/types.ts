/** One Run waiting on a human (UC-026). `waitingFor` is the reason vocabulary, not the enum. */
export interface InboxEntry {
  runId: string;
  projectId: string;
  /** The list is cross-project, so every row names its project; null when it no longer exists. */
  projectName: string | null;
  vendorStoryId: string;
  storyTitle: string | null;
  waitingFor: "approval" | "input" | "failure";
  waitingSince: string;
}
