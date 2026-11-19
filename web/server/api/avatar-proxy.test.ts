import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const id = "a1111111-1111-1111-1111-111111111111";
const upstream = vi.fn<typeof fetch>();
beforeEach(() => {
 vi.stubEnv("NODE_ENV", "development"); vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080"); vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test"); vi.stubEnv("RATEFREAK_PROXY_SECRET", "b".repeat(64)); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
 upstream.mockReset(); upstream.mockResolvedValue(Response.json({ revision: 1, avatarUrl: null })); vi.stubGlobal("fetch", upstream);
});
it("forwards avatar multipart, preserving its boundary and CSRF beyond the JSON size limit", async () => {
 const body = new FormData(); body.set("expectedRevision", "0"); body.set("file", new Blob([new Uint8Array(70000)], {type:"image/png"}), "avatar.png");
 const response = await proxyRequest(new Request("http://web.test/api/v1/users/me/avatar", {method:"PUT", headers:{Origin:"http://web.test","X-CSRF-TOKEN":"token","X-RateFreak-Account-Id":id}, body}), ["users","me","avatar"]);
 expect(response.status).toBe(200); expect(new Headers(upstream.mock.calls[0][1]?.headers).get("X-RateFreak-Account-Id")).toBe(id); expect(new Headers(upstream.mock.calls[0][1]?.headers).get("Content-Type")).toContain("multipart/form-data; boundary=");
});
it("supports only canonical public, owner and report image paths with no-store", async () => {
 for (const path of [["users",id,"avatar",id], ["users","me","avatar",id], ["moderation","reports",id,"avatar",id]]) {
  upstream.mockResolvedValueOnce(new Response(new Uint8Array([1]),{headers:{"Content-Type":"image/webp","X-Content-Type-Options":"nosniff"}}));
  const response = await proxyRequest(new Request("http://web.test/api/v1/"+path.join("/")),path);
  expect(response.status).toBe(200); expect(response.headers.get("Cache-Control")).toBe("no-store"); expect(response.headers.get("X-Content-Type-Options")).toBe("nosniff");
 }
 expect((await proxyRequest(new Request("http://web.test/api/v1/users/me/avatar/invalid"),["users","me","avatar","invalid"])).status).toBe(404);
});
it("requires same-origin CSRF and rejects oversized avatar uploads before upstream", async () => {
 const path=["users","me","avatar"];
 const make=(headers:Record<string,string>,body:BodyInit) => new Request("http://web.test/api/v1/users/me/avatar",{method:"PUT",headers,body});
 expect((await proxyRequest(make({Origin:"http://foreign.test"},new FormData()),path)).status).toBe(403);
 expect((await proxyRequest(make({Origin:"http://web.test","X-CSRF-TOKEN":"token","Content-Type":"multipart/form-data; boundary=x","Content-Length":String(6*1024*1024)},"x"),path)).status).toBe(413);
 expect(upstream).not.toHaveBeenCalled();
});
