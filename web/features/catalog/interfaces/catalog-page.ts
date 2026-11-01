import type { MediaSummary } from "./media-summary";
export interface CatalogPage { items: MediaSummary[]; page: number; pageSize: number; totalCount: number; }
