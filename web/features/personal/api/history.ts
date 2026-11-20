import { apiRequest } from "@/shared/api/request";
import type { HistoryFilters, HistoryPage } from "../interfaces/history";
import { historyParams } from "../query/history";
export const getHistory = (filters: HistoryFilters, signal?: AbortSignal) => apiRequest<HistoryPage>(`users/me/library/history?${historyParams(filters, true)}`, { signal });
