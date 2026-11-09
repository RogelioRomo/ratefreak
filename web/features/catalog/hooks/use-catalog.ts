"use client";
import { useCatalogViewer } from "@/features/custom-catalog/hooks/use-catalog-viewer";
import { useQuery } from "@tanstack/react-query";
import type { CatalogFilters } from "../interfaces/catalog-filters";
import { getCatalog } from "../api/catalog";
import { catalogKeys } from "../query/keys";
export function useCatalog(filters: CatalogFilters | null) {
  const { viewer, active } = useCatalogViewer();
  return useQuery({ queryKey: [...catalogKeys.list(filters), viewer], queryFn: ({ signal }) => getCatalog(filters!, signal), enabled: active && filters !== null, retry: false });
}
