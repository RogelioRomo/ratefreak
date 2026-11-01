import type { MediaSummary } from "./media-summary";
export interface ExternalIdentity { provider: string; namespace: string; externalId: string; externalUrl: string | null; lastSyncedAt: string | null; }
export interface MediaDetails extends MediaSummary { description: string | null; metadata: unknown; createdAt: string; updatedAt: string; externalIds: ExternalIdentity[]; }
