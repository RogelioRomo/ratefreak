export type MediaType = "movie" | "tv_show" | "album" | "song" | "video_game" | "book" | "custom";
export interface CatalogFilters { q: string; type: MediaType | ""; page: number; pageSize: number; categoryId?: string; }
