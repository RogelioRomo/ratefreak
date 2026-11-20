"use client";
import { useQuery } from "@tanstack/react-query";
import type { HistoryFilters } from "../interfaces/history";
import { personalKeys } from "./use-personal";
import { getHistory } from "../api/history";
export const historyKey = (accountId: string, filters: HistoryFilters | null) => [...personalKeys.library(accountId), "history", filters] as const;
export function useHistory(accountId: string, filters: HistoryFilters | null) {
  return useQuery({ queryKey: historyKey(accountId, filters), queryFn: ({ signal }) => getHistory(filters!, signal), enabled: !!accountId && filters !== null, retry: false });
}
