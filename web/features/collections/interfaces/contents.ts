import type { MediaSummary } from "@/features/catalog/interfaces/media-summary";
export interface CollectionItem { id: string; mediaItemId: string; media: MediaSummary; position: number; createdAt: string; updatedAt: string; }
export interface CollectionItemPage { items: CollectionItem[]; page: number; pageSize: number; totalCount: number; }
export interface ItemCreateInput { expectedRevision: number; mediaItemId: string; position: number; }
export interface ItemUpdateInput { expectedRevision: number; position: number; }
