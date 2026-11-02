import type { CollectionFilters } from "../interfaces/collections";
export const collectionKeys = {
  viewer: (viewer: string) => ["collections", viewer] as const,
  list: (viewer: string, filters: CollectionFilters | null) => ["collections", viewer, "list", filters] as const,
  detail: (viewer: string, id: string) => ["collections", viewer, "detail", id.toLowerCase()] as const,
};
