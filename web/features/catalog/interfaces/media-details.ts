import type { MediaSummary } from "./media-summary";
export interface ExternalIdentity { provider: string; namespace: string; externalId: string; externalUrl: string | null; lastSyncedAt: string | null; }
export interface MediaDetails extends MediaSummary { description: string | null; metadata: unknown; createdAt: string; updatedAt: string; externalIds: ExternalIdentity[]; category?: import("@/features/custom-catalog/interfaces/custom-catalog").Category; creatorUserId?: string | null; visibility?: "public" | "private"; revision?: number; isArchived?: boolean; isHidden?: boolean; }
