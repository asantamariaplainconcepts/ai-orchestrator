export interface ConnectorView {
  vendor: string;
  owner: string;
  repository: string;
  secretName: string;
  /** Only Azure DevOps needs this: on GitHub the backlog and the code are the same repository. */
  codeRepository: string | null;
  lastSyncedAt: string | null;
  /** Non-null means the last poll failed — which is a different fact from "no Stories". */
  lastFailure: string | null;
  lastFailureAt: string | null;
}

export interface StoryView {
  vendorId: string;
  title: string;
  /** The vendor's own state value, never normalised — OPN-003 closed by keeping it that way. */
  state: string;
  labels: string[];
}

export interface BacklogView {
  connector: ConnectorView | null;
  stories: StoryView[];
}

export const BACKLOG_VENDORS = ["GitHub", "AzureDevOps"] as const;

export type BacklogVendor = (typeof BACKLOG_VENDORS)[number];

export interface ConfigureConnectorRequest {
  owner: string;
  repository: string;
  secretName: string;
  vendor: BacklogVendor;
  codeRepository: string | null;
}

/** UC-022's detail read — the body arrives verbatim and is sanitised at render (design D2). */
export interface StoryDetail {
  vendorId: string;
  title: string;
  state: string;
  labels: string[];
  body: string | null;
  lastSeenAt: string;
}

/** UC-023 — the change written for a Story and the documents it touches. */
export interface StoryDocuments {
  change: { number: number; title: string; url: string; headRef: string } | null;
  documents: string[];
}

export interface StoryDocumentContent {
  path: string;
  headRef: string;
  content: string;
}
