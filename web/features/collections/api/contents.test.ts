import { beforeEach, expect, it, vi } from "vitest";
import { getItems, createItem, updateItem, deleteItem } from "./contents";
import { resetCsrfState } from "@/shared/api/csrf";
const mock = vi.fn<typeof fetch>(); const id = "a1111111-1111-1111-1111-111111111111";
const item = { id, mediaItemId: id, media: { id, type: "custom", title: "Flower", releaseDate: null, imageReference: null }, position: 0, createdAt: "d", updatedAt: "d" };
beforeEach(() => { resetCsrfState(); mock.mockReset(); vi.stubGlobal("fetch", mock); mock.mockImplementation(async (url, opts) => String(url).endsWith("csrf") ? Response.json({ requestToken: "token" }) : opts?.method === "DELETE" ? new Response(null, { status: 204 }) : Response.json(item)); });
it("sends catalog identity and ordering only through existing CSRF handling", async () => {
 const signal = new AbortController().signal; await createItem(id, { expectedRevision: 2, mediaItemId: id, position: 0 }, signal); await updateItem(id, id, { expectedRevision: 3, position: 4 }, signal);
 const writes = mock.mock.calls.filter(([, o]) => o?.method !== "GET"); expect(JSON.parse(String(writes[0][1]?.body))).toEqual({ expectedRevision: 2, mediaItemId: id, position: 0 }); expect(JSON.parse(String(writes[1][1]?.body))).toEqual({ expectedRevision: 3, position: 4 }); expect(writes[1][1]?.signal).toBe(signal);
});
it("uses bounded paging and deletes membership without deleting the catalog item", async () => {
 mock.mockResolvedValueOnce(Response.json({ items: [], page: 2, pageSize: 20, totalCount: 0 })); const signal = new AbortController().signal; await getItems(id.toUpperCase(), 2, signal); await deleteItem(id, id, 8, signal);
 expect(mock.mock.calls[0][0]).toBe(`/api/v1/collections/${id}/items?page=2&pageSize=20`); expect(mock.mock.calls.filter(([, o]) => o?.method === "DELETE").map(([url]) => url)).toEqual([`/api/v1/collections/${id}/items/${id}?expectedRevision=8`]);
});
