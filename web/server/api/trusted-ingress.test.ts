import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const upstream = vi.fn<typeof fetch>();
const id = "a1111111-1111-1111-1111-111111111111";
beforeEach(() => {
  vi.stubEnv("NODE_ENV", "production");
  vi.stubEnv("RATEFREAK_API_URL", "http://api:8080");
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "https://web.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", "a".repeat(64));
  vi.stubEnv("RATEFREAK_INGRESS_SECRET", "b".repeat(64));
  vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "trusted-proxy");
  vi.stubEnv("RATEFREAK_ALLOW_PRIVATE_API_HTTP", "true");
  vi.stubGlobal("fetch", upstream); upstream.mockReset();
});
const routes = [
  ["GET", "users/me"], ["GET", "setup/status"],
  ["POST", "users/me/data/export"], ["PUT", `media/${id}/artwork`],
  ["POST", "auth/external/google/start"], ["GET", "auth/external/google/callback"],
] as const;
function request(method: string, path: string, trusted: boolean, ip = "192.0.2.7") {
  const headers = new Headers({ Origin: "https://web.test", "X-CSRF-TOKEN": "csrf" });
  if (trusted) {headers.set("X-RateFreak-Ingress-Key", "b".repeat(64)); headers.set("X-RateFreak-Ingress-IP", ip);}
  let body: BodyInit | undefined;
  if (method === "PUT") {const form = new FormData(); form.set("file", new Blob(["image"]), "image.png"); body = form;}
  else if (method === "POST") {body = "{}"; headers.set("Content-Type", "application/json");}
  return new Request(`https://web.test/api/v1/${path}`, {method, headers, body});
}
it.each(routes)("rejects unauthenticated ingress before upstream %s %s", async (method, path) => {
  const response = await proxyRequest(request(method, path, false), path.split("/"));
  expect(response.status).toBeGreaterThanOrEqual(400);
  expect(upstream).not.toHaveBeenCalled();
  expect(response.headers.get("Cache-Control")).toBe("no-store");
});
it.each(routes)("forwards only validated API attribution for %s %s", async (method, path) => {
  upstream.mockImplementation(async () => path.endsWith("export")
    ? new Response("zip", {headers:{"Content-Type":"application/zip"}})
    : Response.json({}));
  const response = await proxyRequest(request(method, path, true), path.split("/"));
  expect(upstream).toHaveBeenCalledTimes(1);
  const headers = new Headers(upstream.mock.calls[0][1]?.headers);
  expect(headers.get("X-RateFreak-Client-IP")).toBe("192.0.2.7");
  expect(headers.get("X-RateFreak-Proxy-Key")).toBe("a".repeat(64));
  for (const name of ["X-RateFreak-Ingress-Key", "X-RateFreak-Ingress-IP"]) {
    expect(headers.has(name)).toBe(false); expect(response.headers.has(name)).toBe(false);
  }
  await response.body?.cancel();
});
it("preserves distinct client identities for API budgets", async () => {
  upstream.mockImplementation(async () => Response.json({}));
  for (const ip of ["192.0.2.7", "192.0.2.8"]) await proxyRequest(request("GET", "users/me", true, ip), ["users","me"]);
  expect(upstream.mock.calls.map(call => new Headers(call[1]?.headers).get("X-RateFreak-Client-IP"))).toEqual(["192.0.2.7", "192.0.2.8"]);
});
