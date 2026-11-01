import type { MediaType } from "@/features/catalog/interfaces/catalog-filters";
export type ProviderName = "tmdb" | "musicbrainz" | "igdb";
export interface ProviderIdentity { provider: ProviderName; type: MediaType; externalId: string; }
export interface ProviderCapability { name: ProviderName; enabled: boolean; supportedTypes: MediaType[]; attribution: string; attributionUrl: string; }
export interface ProviderFilters { q: string; provider: ProviderName; type: MediaType; page: number; }
export interface ExternalMedia { provider: ProviderName; externalId: string; type: MediaType; title: string; description: string | null; releaseDate: string | null; imageReference: string | null; metadata: unknown; }
export interface ExternalSearchPage { provider: ProviderName; page: number; items: ExternalMedia[]; }
export interface ImportMediaRequest { provider: ProviderName; externalId: string; mediaType: MediaType; }
export interface ImportMediaResult { mediaId: string; created: boolean; }
