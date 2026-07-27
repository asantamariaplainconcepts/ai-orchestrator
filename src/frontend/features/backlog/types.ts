export interface ConnectorView {
  vendor: string;
  owner: string;
  repository: string;
  secretName: string;
  lastSyncedAt: string | null;
  /** Non-null means the last poll failed — which is a different fact from "no Stories". */
  lastFailure: string | null;
  lastFailureAt: string | null;
}

export interface StoryView {
  vendorId: string;
  title: string;
  /** The vendor's own state value; not normalised until OPN-003 closes (design D9). */
  state: string;
  labels: string[];
}

export interface BacklogView {
  connector: ConnectorView | null;
  stories: StoryView[];
}

export interface ConfigureConnectorRequest {
  owner: string;
  repository: string;
  secretName: string;
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
