import { apiRequest } from "@/shared/api/request";
import type { ConnectionFilters, FollowPage } from "../interfaces/follows";
export const getConnections = (id: string, filters: ConnectionFilters, signal?: AbortSignal) => apiRequest<FollowPage>(`users/${id}/${filters.direction}?page=${filters.page}&pageSize=20`, { signal });
export const setFollow = (id: string, following: boolean, signal?: AbortSignal) => apiRequest<void>(`users/${id}/follow`, { method: following ? "PUT" : "DELETE", body: {}, signal });
