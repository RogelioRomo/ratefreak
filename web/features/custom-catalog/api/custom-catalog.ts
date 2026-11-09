import { apiRequest } from "@/shared/api/request";
import { getCsrfToken } from "@/shared/api/csrf";
import { parseApiError } from "@/shared/api/parse-api-error";
import type { Category, CategoryPage, CustomItem, ItemInput } from "../interfaces/custom-catalog";
function decodeItem(text: string): CustomItem {
  const item = JSON.parse(text) as CustomItem;
  if (!Number.isSafeInteger(item.revision) || item.revision < 1) throw new Error("Invalid item revision. Reload before editing.");
  return item;
}
export const getCategories = (q: string, signal?: AbortSignal) => apiRequest<CategoryPage>(`categories?${new URLSearchParams({ q, pageSize: "100" })}`, { signal });
export const createCategory = (name: string, scope: "public" | "private", signal?: AbortSignal) => apiRequest<Category>("categories", { method: "POST", body: { name, scope }, signal });
export const getManagedItem = (id: string, signal?: AbortSignal) => apiRequest<CustomItem>(`media/${id}/manage`, { signal, decode: decodeItem });
export const createItem = (body: ItemInput, signal?: AbortSignal) => apiRequest<CustomItem>("media", { method: "POST", body, signal, decode: decodeItem });
export const updateItem = (id: string, expectedRevision: number, input: ItemInput, signal?: AbortSignal) => apiRequest<CustomItem>(`media/${id}`, { method: "PATCH", body: { title: input.title, description: input.description, categoryId: input.categoryId, expectedRevision }, signal, decode: decodeItem });
export const changeItemState = (item: CustomItem, action: "publish" | "unpublish" | "archive" | "restore", signal?: AbortSignal) => apiRequest<CustomItem>(`media/${item.id}/${action}`, { method: "POST", body: { expectedRevision: item.revision }, signal, decode: decodeItem });
export async function uploadImage(item: CustomItem, file: File, signal?: AbortSignal): Promise<CustomItem> {
  const body = new FormData(); body.set("expectedRevision", String(item.revision)); body.set("file", file);
  const token = await getCsrfToken(signal); signal?.throwIfAborted();
  const response = await fetch(`/api/v1/media/${item.id}/artwork`, { method: "PUT", headers: { "X-CSRF-TOKEN": token }, body, credentials: "same-origin", cache: "no-store", signal });
  if (!response.ok) throw await parseApiError(response); signal?.throwIfAborted(); return decodeItem(await response.text());
}
export const removeImage = (item: CustomItem, signal?: AbortSignal) => apiRequest<CustomItem>(`media/${item.id}/artwork`, { method: "DELETE", body: { expectedRevision: item.revision }, signal, decode: decodeItem });
