import { apiRequest } from "@/shared/api/request";
import type { ItemCreateInput, ItemUpdateInput } from "../interfaces/contents";
import { decodeItem, decodeItems } from "./decode-contents";
const root = (id: string) => `collections/${id.toLowerCase()}`;
export const getItems = (id: string, page: number, signal: AbortSignal) => apiRequest(`${root(id)}/items?page=${page}&pageSize=20`, { signal, decode: decodeItems });
export const getItem = (id: string, itemId: string, signal: AbortSignal) => apiRequest(`${root(id)}/items/${itemId.toLowerCase()}`, { signal, decode: decodeItem });
export const createItem = (id: string, body: ItemCreateInput, signal: AbortSignal) => apiRequest(`${root(id)}/items`, { method: "POST", body, signal, decode: decodeItem });
export const updateItem = (id: string, itemId: string, body: ItemUpdateInput, signal: AbortSignal) => apiRequest(`${root(id)}/items/${itemId.toLowerCase()}`, { method: "PATCH", body, signal, decode: decodeItem });
export const deleteItem = (id: string, itemId: string, revision: number, signal: AbortSignal) => apiRequest<void>(`${root(id)}/items/${itemId.toLowerCase()}?expectedRevision=${revision}`, { method: "DELETE", signal });

export const clearUnavailableItems = (id: string, revision: number, signal: AbortSignal) => apiRequest<void>(`${root(id)}/unavailable-items?expectedRevision=${revision}`, { method: "DELETE", signal });
