"use client";
import { useQuery } from "@tanstack/react-query";
import type { ProviderFilters, ProviderIdentity } from "../interfaces/provider";
import { getProviders, getExternalSearch, getExternalMedia } from "../api/providers";
export const providerKeys = {
  capabilities: ["providers", "capabilities"] as const,
  search: (filters: ProviderFilters | null) => ["providers", "search", filters] as const,
  preview: (identity: ProviderIdentity | null) => ["providers", "preview", identity] as const,
};
export const useProviders = () => useQuery({ queryKey: providerKeys.capabilities, queryFn: ({ signal }) => getProviders(signal), retry: false });
export const useExternalSearch = (filters: ProviderFilters | null) => useQuery({ queryKey: providerKeys.search(filters), queryFn: ({ signal }) => getExternalSearch(filters!, signal), enabled: !!filters, retry: false });
export const useExternalMedia = (identity: ProviderIdentity | null, enabled = true) => useQuery({ queryKey: providerKeys.preview(identity), queryFn: ({ signal }) => getExternalMedia(identity!, signal), enabled: !!identity && enabled, retry: false });
