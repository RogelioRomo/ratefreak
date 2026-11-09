"use client";
import { useCatalogViewer } from "@/features/custom-catalog/hooks/use-catalog-viewer";
import { useQuery } from "@tanstack/react-query";
import { getMedia } from "../api/catalog";
import { catalogKeys } from "../query/keys";
import { isMediaId } from "../query/filters";
export function useMedia(id: string) {
  const { viewer, active } = useCatalogViewer();
  return useQuery({ queryKey: [...catalogKeys.detail(id), viewer], queryFn: ({ signal }) => getMedia(id, signal), enabled: active && isMediaId(id), retry: false });
}
