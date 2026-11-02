"use client";
import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getCollection, getCollections } from "../api/collections";
import type { CollectionFilters } from "../interfaces/collections";
import { collectionKeys } from "../query/keys";
import { isMediaId } from "@/features/catalog/query/filters";
function usePausedReads(viewer: string, active: boolean) {
  const client = useQueryClient();
  useEffect(() => { if (!active) void client.cancelQueries({queryKey: collectionKeys.viewer(viewer)}); }, [client, viewer, active]);
}
export function useCollections(viewer: string, filters: CollectionFilters | null, active: boolean) {
  usePausedReads(viewer, active);
  return useQuery({queryKey: collectionKeys.list(viewer, filters), queryFn: ({signal}) => getCollections(filters!, signal), enabled: active && !!filters && !(viewer === "anonymous" && filters.scope === "mine"), retry: false});
}
export function useCollection(viewer: string, id: string, active: boolean) {
  usePausedReads(viewer, active);
  return useQuery({queryKey: collectionKeys.detail(viewer, id), queryFn: ({signal}) => getCollection(id, signal), enabled: active && isMediaId(id), retry: false});
}
