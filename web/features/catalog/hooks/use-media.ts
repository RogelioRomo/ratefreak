"use client";
import { useQuery } from "@tanstack/react-query";
import { getMedia } from "../api/catalog";
import { catalogKeys } from "../query/keys";
import { isMediaId } from "../query/filters";
export function useMedia(id: string) {
  return useQuery({ queryKey: catalogKeys.detail(id), queryFn: ({ signal }) => getMedia(id, signal), enabled: isMediaId(id), retry: false });
}
