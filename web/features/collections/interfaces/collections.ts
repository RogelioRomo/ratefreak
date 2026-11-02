import type { FieldConfiguration, FieldType } from "./contents";
export type CollectionVisibility = "private" | "public";
export type CollectionViewer = "anonymous" | string;
export interface CollectionAuthor { id: string; handle: string; displayName: string; }
export interface CollectionSummary {
  id: string; owner: CollectionAuthor; name: string; description: string | null;
  visibility: CollectionVisibility; revision: number; schemaVersion: number;
  createdAt: string; updatedAt: string;
}
export interface CollectionField { id: string; key: string; label: string; fieldType: FieldType; required: boolean; configuration: FieldConfiguration; position: number; }
export interface CollectionDetails { collection: CollectionSummary; fields: CollectionField[]; }
export interface CollectionPage { items: CollectionSummary[]; page: number; pageSize: number; totalCount: number; }
export interface CollectionFilters { scope: "all" | "mine"; page: number; pageSize: 20; }
export interface CollectionCreate { name: string; description: string | null; visibility: CollectionVisibility; }
export interface CollectionUpdate extends CollectionCreate { expectedRevision: number; }
export interface CollectionMetadata { name: string; description: string; visibility: CollectionVisibility; }
