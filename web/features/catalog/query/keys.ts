import type { CatalogFilters } from "../interfaces/catalog-filters";
export const catalogKeys = {
  list: (filters: CatalogFilters | null) => ["catalog", "list", filters] as const,
  detail: (id: string) => ["catalog", "detail", id.toLowerCase()] as const,
};
