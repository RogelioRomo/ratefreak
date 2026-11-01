import { apiRequest } from "@/shared/api/request";
import type { CatalogFilters } from "../interfaces/catalog-filters";
import type { CatalogPage } from "../interfaces/catalog-page";
import type { MediaDetails } from "../interfaces/media-details";
import { filterParams, isMediaId } from "../query/filters";
export function getCatalog(filters: CatalogFilters, signal?: AbortSignal): Promise<CatalogPage> {
  return apiRequest(`media?${filterParams(filters, true)}`, { signal });
}
export async function getMedia(id: string, signal?: AbortSignal): Promise<MediaDetails> {
  if (!isMediaId(id)) throw new Error("Invalid media ID.");
  return apiRequest(`media/${id.toLowerCase()}`, { signal });
}
