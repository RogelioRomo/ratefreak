import type { CollectionFilters } from "../interfaces/collections";
export function collectionFilters(params: URLSearchParams): CollectionFilters | null {
  for (const key of params.keys()) if (!["scope", "page"].includes(key) || params.getAll(key).length !== 1) return null;
  const scope = params.get("scope") ?? "all", page = params.get("page") ?? "1";
  if (!["all", "mine"].includes(scope) || !/^\d+$/.test(page) || +page < 1 || +page > 10000) return null;
  return { scope: scope as CollectionFilters["scope"], page: +page, pageSize: 20 };
}
export function collectionHref(filters: CollectionFilters): string {
  const params = new URLSearchParams();
  if (filters.scope === "mine") params.set("scope", "mine");
  if (filters.page !== 1) params.set("page", String(filters.page));
  return `/collections${params.size ? `?${params}` : ""}`;
}
