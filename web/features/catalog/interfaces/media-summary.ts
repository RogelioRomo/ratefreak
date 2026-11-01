import type { MediaType } from "./catalog-filters";
export interface MediaSummary { id: string; type: MediaType; title: string; releaseDate: string | null; imageReference: string | null; }
