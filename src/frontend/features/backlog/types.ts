export interface ConnectorView {
  vendor: string;
  owner: string;
  repository: string;
  secretName: string;
  /**
   * When this product stored the token itself (#124). Null means the secret is managed outside
   * the product — a different fact from "never", which is why it is not a boolean.
   */
  secretSetAt: string | null;
  /** Only Azure DevOps needs this: on GitHub the backlog and the code are the same repository. */
  codeRepository: string | null;
  promptDirectory: string | null;
  lastSyncedAt: string | null;
  /** Non-null means the last poll failed — which is a different fact from "no Stories". */
  lastFailure: string | null;
  lastFailureAt: string | null;
  /** Repository | LocalFolder (#210); the path travels only with the latter. */
  codeSource: string;
  localPath: string | null;
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

/** #132 — what the stored credential can actually do, one entry per capability. */
export interface CapabilityView {
  capability: string;
  succeeded: boolean;
  /** The vendor's own reason when it refused; null when it did not. */
  reason: string | null;
}

export interface ConnectorTestView {
  satisfied: boolean;
  capabilities: CapabilityView[];
}

export interface ConfigureConnectorRequest {
  owner: string;
  repository: string;
  /** Exactly one of these two: name a secret somebody else manages, or paste the token (#124). */
  secretName: string | null;
  accessToken: string | null;
  vendor: BacklogVendor;
  codeRepository: string | null;
  promptDirectory: string | null;
  /** Repository | LocalFolder (#210); null keeps the API's default (Repository). */
  codeSource: string | null;
  localPath: string | null;
}

/** #210 — four facts about one path, enough to name the failing check (mock 3a). */
export interface PathValidation {
  isDirectory: boolean;
  isGitRepository: boolean;
  branch: string | null;
  isClean: boolean | null;
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
