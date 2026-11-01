import type { CatalogFilters, MediaType } from "../interfaces/catalog-filters";
export const mediaTypes = { movie: "Movies", tv_show: "TV shows", album: "Albums", video_game: "Video games" } as const;
export const mediaLabels = { movie: "Movie", tv_show: "TV show", album: "Album", video_game: "Video game" } as const;
export const isMediaId = (id: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
export function submittedFilters(q: string, type: string): CatalogFilters {
  return { q: q.trim(), type: type as MediaType | "", page: 1, pageSize: 20 };
}
export function parseFilters(params: URLSearchParams): CatalogFilters | null {
  for (const key of params.keys()) if (!["q", "type", "page", "pageSize"].includes(key) || params.getAll(key).length !== 1) return null;
  const q = params.get("q") ?? "";
  const type = params.get("type") ?? "";
  const page = params.get("page") ?? "1";
  if (q.length > 200 || q.includes("\0") || (type && !Object.hasOwn(mediaTypes, type)) || !/^\d+$/.test(page) || +page < 1 || +page > 10000 || (params.has("pageSize") && params.get("pageSize") !== "20")) return null;
  return { ...submittedFilters(q, type), page: +page };
}
export function filterParams(filters: CatalogFilters, api = false): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.q) params.set("q", filters.q);
  if (filters.type) params.set("type", filters.type);
  if (api || filters.page > 1) params.set("page", String(filters.page));
  if (api) params.set("pageSize", String(filters.pageSize));
  return params;
}
export function catalogHref(filters: CatalogFilters): string {
  const query = filterParams(filters).toString();
  return `/catalog${query ? `?${query}` : ""}`;
}
