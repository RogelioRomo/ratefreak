import type { HistoryFilters } from "../interfaces/history";
export const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"] as const;
export function parseHistoryFilters(params: URLSearchParams, api = false): HistoryFilters | null {
  const allowed = ["year", "month", "page", "pageSize"];
  for (const key of params.keys()) if (!allowed.includes(key) || params.getAll(key).length !== 1 || !/^\d+$/.test(params.get(key)!)) return null;
  const year = params.has("year") ? Number(params.get("year")) : undefined;
  const month = params.has("month") ? Number(params.get("month")) : undefined;
  const page = Number(params.get("page") ?? 1), pageSize = Number(params.get("pageSize") ?? 20);
  if ((year !== undefined && (year < 1 || year > 9999)) || (month !== undefined && (year === undefined || month < 1 || month > 12)) ||
      page < 1 || page > 10000 || pageSize < 1 || pageSize > 100 || (!api && pageSize !== 20)) return null;
  return { ...(year !== undefined ? { year } : {}), ...(month !== undefined ? { month } : {}), page, pageSize };
}
export function historyParams(filters: HistoryFilters, api = false): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.year !== undefined) params.set("year", String(filters.year));
  if (filters.month !== undefined) params.set("month", String(filters.month));
  if (api || filters.page > 1) params.set("page", String(filters.page));
  if (api) params.set("pageSize", String(filters.pageSize));
  return params;
}
export function historyHref(filters: HistoryFilters): string { const query = historyParams(filters).toString(); return `/library/history${query ? `?${query}` : ""}`; }
export function completionLabel(date: string): string { const [year, month, day] = date.split("-").map(Number); return `${monthNames[month - 1]} ${day}, ${year}`; }
