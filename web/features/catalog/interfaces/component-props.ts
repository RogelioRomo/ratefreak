import type { CatalogFilters } from "./catalog-filters";
import type { CatalogPage } from "./catalog-page";
export interface CatalogFiltersProps { filters: CatalogFilters; }
export interface CatalogResultsProps { data: CatalogPage; filters: CatalogFilters; }
export interface MediaArtworkProps { reference: string | null; title: string; type: string; }
export interface MediaDetailScreenProps { id: string; }
export interface MediaPageProps { params: Promise<{ id: string }>; }
