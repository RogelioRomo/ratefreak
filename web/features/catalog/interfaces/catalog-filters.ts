export type MediaType = "movie" | "tv_show" | "album" | "video_game" | "custom";
export interface CatalogFilters { q: string; type: MediaType | ""; page: number; pageSize: number; categoryId?: string; }
