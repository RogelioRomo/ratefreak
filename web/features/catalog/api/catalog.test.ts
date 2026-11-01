import { beforeEach, expect, it, vi } from "vitest";
import { getCatalog, getMedia } from "./catalog";
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => { vi.stubGlobal("fetch", fetchMock); fetchMock.mockReset(); });
it("requests encoded filters through the real client with cancellation", async () => {
  fetchMock.mockResolvedValue(Response.json({ items: [], page: 2, pageSize: 20, totalCount: 0 }));
  const signal = new AbortController().signal;
  const data = await getCatalog({ q: "A & B", type: "album", page: 2, pageSize: 20 }, signal);
  expect(data.items).toEqual([]);
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/media?q=A+%26+B&type=album&page=2&pageSize=20", expect.objectContaining({ signal, cache: "no-store" }));
});
it("canonicalizes GUIDs and propagates cancellation", async () => {
  fetchMock.mockResolvedValue(Response.json({ title: "Example" }));
  const signal = new AbortController().signal;
  await getMedia("A1111111-1111-1111-1111-111111111111", signal);
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/media/a1111111-1111-1111-1111-111111111111", expect.objectContaining({ signal }));
});
it("rejects invalid IDs without requesting", async () => {
  await expect(getMedia("../users/me")).rejects.toThrow();
  expect(fetchMock).not.toHaveBeenCalled();
});
