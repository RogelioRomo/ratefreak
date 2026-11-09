import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const id = "a1111111-1111-1111-1111-111111111111";
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  vi.stubEnv("NODE_ENV", "development"); vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080"); vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test"); vi.stubEnv("RATEFREAK_PROXY_SECRET", "b".repeat(64)); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
  fetchMock.mockReset(); fetchMock.mockResolvedValue(Response.json({ id })); vi.stubGlobal("fetch", fetchMock);
});
it("forwards bounded multipart only on artwork PUT while preserving its boundary", async () => {
  const body = new FormData(); body.set("expectedRevision", "1"); body.set("file", new Blob([new Uint8Array(70_000)], { type: "image/png" }), "flower.png");
  const request = new Request(`http://web.test/api/v1/media/${id}/artwork`, { method: "PUT", headers: { Origin: "http://web.test", "X-CSRF-TOKEN": "csrf" }, body });
  expect((await proxyRequest(request, ["media", id, "artwork"])).status).toBe(200);
  const headers = new Headers(fetchMock.mock.calls[0][1]?.headers); expect(headers.get("Content-Type")).toContain("multipart/form-data; boundary="); expect(headers.get("X-CSRF-TOKEN")).toBe("csrf");
});
it("keeps ordinary mutations JSON-only and bounded", async () => {
  const request = new Request("http://web.test/api/v1/media", { method: "POST", headers: { Origin: "http://web.test", "X-CSRF-TOKEN": "csrf" }, body: new FormData() });
  expect((await proxyRequest(request, ["media"])).status).toBe(415); expect(fetchMock).not.toHaveBeenCalled();
});
it("does not cache binary artwork and preserves nosniff", async () => {
  fetchMock.mockResolvedValue(new Response(new Uint8Array([1, 2, 3]), { headers: { "Content-Type": "image/webp", "X-Content-Type-Options": "nosniff" } }));
  const response = await proxyRequest(new Request(`http://web.test/api/v1/media/${id}/artwork/${id}`), ["media", id, "artwork", id]);
  expect(response.status).toBe(200); expect(response.headers.get("cache-control")).toBe("no-store"); expect(response.headers.get("x-content-type-options")).toBe("nosniff"); expect(new Uint8Array(await response.arrayBuffer())).toEqual(new Uint8Array([1, 2, 3]));
});
