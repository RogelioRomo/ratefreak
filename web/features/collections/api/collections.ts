import { decodeCollectionPage, decodeDetails } from "./decode-contents";
import { apiRequest } from "@/shared/api/request";
import type { CollectionCreate, CollectionDetails, CollectionFilters, CollectionPage, CollectionUpdate } from "../interfaces/collections";
export const getCollections = (filters: CollectionFilters, signal?: AbortSignal) => apiRequest<CollectionPage>(`collections?${new URLSearchParams({ ownOnly: String(filters.scope === "mine"), page: String(filters.page), pageSize: "20" })}`, { signal, decode: decodeCollectionPage });
export const getCollection = (id: string, signal?: AbortSignal) => apiRequest<CollectionDetails>(`collections/${id.toLowerCase()}`, { signal, decode: decodeDetails });
export const createCollection = (input: CollectionCreate, signal: AbortSignal) => apiRequest<CollectionDetails>("collections", { method: "POST", body: input, signal, decode: decodeDetails });
export const updateCollection = (id: string, input: CollectionUpdate, signal: AbortSignal) => apiRequest<CollectionDetails>(`collections/${id.toLowerCase()}`, { method: "PATCH", body: input, signal, decode: decodeDetails });
