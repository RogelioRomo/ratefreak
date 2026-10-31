import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";

const key = "b".repeat(64);
const fetchMock = vi.fn<typeof fetch>();
function request(path = "auth/login", changes: Record<string, string> = {}) {
  const headers: Record<string, string> = { Origin: "http://web.test", "Content-Type": "application/json", "X-CSRF-TOKEN": "csrf", ...changes };
  for (const name of Object.keys(headers)) if (headers[name] === "") delete headers[name];
  return new Request(`http://web.test/api/v1/${path}`, { method: "POST", headers, body: '{"email":"reader@example.test","password":"example"}' });
}
beforeEach(() => {
  vi.stubEnv("NODE_ENV", "development");
  vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", key);
  vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
  fetchMock.mockReset();
  fetchMock.mockResolvedValue(Response.json({ id: "account" }));
  vi.stubGlobal("fetch", fetchMock);
});

it.each(["register", "login", "logout"])("forwards only same-origin auth/%s with bounded JSON and CSRF", async (action) => {
  const response = await proxyRequest(request(`auth/${action}`), ["auth", action]);
  expect(response.status).toBe(200);
  const [url, init] = fetchMock.mock.calls[0];
  expect(url).toBe(`http://localhost:5080/api/v1/auth/${action}`);
  expect(init?.method).toBe("POST");
  expect(new TextDecoder().decode(init?.body as Uint8Array)).toContain('"email":"reader@example.test"');
  const headers = new Headers(init?.headers);
  expect(headers.get("X-CSRF-TOKEN")).toBe("csrf");
  expect(headers.get("X-RateFreak-Proxy-Key")).toBe(key);
  expect(headers.get("X-RateFreak-Client-IP")).toBe("127.0.0.1");
  expect(response.headers.get("cache-control")).toBe("no-store");
});

it.each(["", "https://evil.test", "http://web.test.evil.test"])("rejects missing/foreign Origin %j without forwarding", async (Origin) => {
  expect((await proxyRequest(request("auth/login", { Origin }), ["auth", "login"])).status).toBe(403);
  expect(fetchMock).not.toHaveBeenCalled();
});

it("requires JSON, CSRF and complete write configuration", async () => {
  expect((await proxyRequest(request("auth/login", { "Content-Type": "text/plain" }), ["auth", "login"])).status).toBe(415);
  expect((await proxyRequest(request("auth/login", { "X-CSRF-TOKEN": "" }), ["auth", "login"])).status).toBe(400);
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", undefined);
  expect((await proxyRequest(request(), ["auth", "login"])).status).toBe(503);
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test");
  vi.stubEnv("RATEFREAK_PROXY_SECRET", undefined); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", undefined);
  expect((await proxyRequest(request(), ["auth", "login"])).status).toBe(503);
  expect(fetchMock).not.toHaveBeenCalled();
});

it("rejects oversized writes locally", async () => {
  const large = new Request("http://web.test/api/v1/auth/login", { method: "POST", headers: {
    Origin: "http://web.test", "Content-Type": "application/json", "X-CSRF-TOKEN": "csrf",
  }, body: "x".repeat(65537) });
  expect((await proxyRequest(large, ["auth", "login"])).status).toBe(413);
  expect(fetchMock).not.toHaveBeenCalled();
});
