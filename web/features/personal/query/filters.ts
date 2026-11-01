import { mediaTypes } from "@/features/catalog/query/filters";
import type { LibraryFilters } from "../interfaces/personal";
export const trackingStatuses = { planned: "Planned", in_progress: "In progress", completed: "Completed", paused: "Paused", dropped: "Dropped" } as const;
export function libraryFilters(params: URLSearchParams): LibraryFilters | null {
  for (const key of params.keys()) if (!["status", "type", "page", "pageSize"].includes(key) || params.getAll(key).length !== 1) return null;
  const status = params.get("status") ?? "", type = params.get("type") ?? "", page = params.get("page") ?? "1";
  if ((status && !Object.hasOwn(trackingStatuses, status)) || (type && !Object.hasOwn(mediaTypes, type)) || !/^\d+$/.test(page) || +page < 1 || +page > 10000 || (params.has("pageSize") && params.get("pageSize") !== "20")) return null;
  return { status, type, page: +page, pageSize: 20 } as LibraryFilters;
}
export function libraryParams(filters: LibraryFilters, api = false): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.type) params.set("type", filters.type);
  if (api || filters.page > 1) params.set("page", String(filters.page));
  if (api) params.set("pageSize", String(filters.pageSize));
  return params;
}
export function libraryHref(filters: LibraryFilters): string { const query = libraryParams(filters).toString(); return `/library${query ? `?${query}` : ""}`; }
export function reviewPage(params: URLSearchParams): number | null {
  if (params.getAll("reviewsPage").length > 1) return null;
  const value = params.get("reviewsPage") ?? "1";
  return /^\d+$/.test(value) && +value >= 1 && +value <= 10000 ? +value : null;
}
