export type MediaType = "movie" | "tv_show" | "album" | "video_game";
export interface CatalogFilters { q: string; type: MediaType | ""; page: number; pageSize: number; }
