"use client";
import { useQuery } from "@tanstack/react-query";
import type { CatalogFilters } from "../interfaces/catalog-filters";
import { getCatalog } from "../api/catalog";
import { catalogKeys } from "../query/keys";
export function useCatalog(filters: CatalogFilters | null) {
  return useQuery({ queryKey: catalogKeys.list(filters), queryFn: ({ signal }) => getCatalog(filters!, signal), enabled: filters !== null, retry: false });
}
