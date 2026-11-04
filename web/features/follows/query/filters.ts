import type { ConnectionFilters } from "../interfaces/follows";
export function connectionFilters(params: URLSearchParams): ConnectionFilters | null {
 for (const key of params.keys()) if (!["connections", "page"].includes(key) || params.getAll(key).length !== 1) return null;
 const direction = params.get("connections") ?? "followers", page = params.get("page") ?? "1";
 if (!["followers", "following"].includes(direction) || !/^\d+$/.test(page) || +page < 1 || +page > 10000) return null;
 return { direction: direction as ConnectionFilters["direction"], page: +page };
}
export function connectionHref(handle: string, filters: ConnectionFilters): string {
 const params = new URLSearchParams(); if (filters.direction !== "followers") params.set("connections", filters.direction); if (filters.page !== 1) params.set("page", String(filters.page));
 return `/users/${handle}${params.size ? `?${params}` : ""}`;
}
