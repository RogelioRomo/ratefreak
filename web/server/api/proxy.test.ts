import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";

const fetchMock = vi.fn<typeof fetch>();
const makeRequest = (path = "users/me", init?: RequestInit) => new Request(`http://web.test/api/v1/${path}`, init);

beforeEach(() => {
  vi.stubEnv("RATEFREAK_API_URL", "http://api.test:5080");
  vi.stubEnv("NODE_ENV", "development");
  vi.stubGlobal("fetch", fetchMock);
  fetchMock.mockReset();
});
afterEach(() => vi.useRealTimers());

describe("proxy isolation", () => {
  it.each([200, 401, 429])("relays %i without caching or forwarding identity headers", async (status) => {
    fetchMock.mockResolvedValue(new Response('{"title":"Example"}', {
      status, headers: { "Content-Type": "application/problem+json", "Retry-After": "60", "Content-Length": "19", "Content-Encoding": "gzip" },
    }));
    const response = await proxyRequest(makeRequest("users/me", { headers: {
      Cookie: "ratefreak.session=s; ratefreak.csrf=c; unrelated=secret",
      Authorization: "Bearer spoof", "X-User-Id": "spoof", "X-Forwarded-Host": "evil.test",
    } }), ["users", "me"]);
    expect(response.status).toBe(status);
    expect(await response.json()).toEqual({ title: "Example" });
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(response.headers.get("retry-after")).toBe("60");
    expect(response.headers.has("content-length")).toBe(false);
    expect(response.headers.has("content-encoding")).toBe(false);
    const [url, options] = fetchMock.mock.calls[0];
    expect(String(url)).toBe("http://api.test:5080/api/v1/users/me");
    expect(options).toMatchObject({ cache: "no-store", redirect: "manual" });
    const headers = new Headers(options?.headers);
    expect(headers.get("cookie")).toBe("ratefreak.session=s; ratefreak.csrf=c");
    for (const name of ["authorization", "x-user-id", "x-forwarded-host", "host"]) expect(headers.has(name)).toBe(false);
  });

  it("preserves separate cookie updates and deletions, including Expires commas", async () => {
    const cookies = [
      "ratefreak.session=s; Path=/; HttpOnly; Secure; SameSite=Lax",
      "ratefreak.csrf=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Max-Age=0; Secure; SameSite=Strict",
    ];
    const headers = new Headers();
    cookies.forEach((cookie) => headers.append("Set-Cookie", cookie));
    fetchMock.mockResolvedValue(new Response("{}", { headers }));
    const response = await proxyRequest(makeRequest(), ["users", "me"]);
    expect(response.headers.getSetCookie()).toEqual(cookies);
  });

  it.each(["auth/unknown", "users/me/extra", "users%2fme", "users/me?target=https://evil.test", "users/%6de"])("rejects unapproved URL %s without fetching", async (path) => {
    const response = await proxyRequest(makeRequest(path), path.split("?", 1)[0].split("/"));
    expect(response.status).toBe(404);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects mismatch between request URL and supplied path", async () => {
    expect((await proxyRequest(makeRequest("auth/login"), ["users", "me"])).status).toBe(404);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects every unsupported method locally", async () => {
    for (const method of ["POST", "PUT", "DELETE", "HEAD", "OPTIONS"]) {
      const response = await proxyRequest(makeRequest("users/me", { method, headers: { Origin: "https://evil.test" } }), ["users", "me"]);
      expect(response.status).toBe(405);
      expect(response.headers.get("cache-control")).toBe("no-store");
    }
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not follow upstream redirects", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 302, headers: { Location: "https://evil.test" } }));
    const response = await proxyRequest(makeRequest(), ["users", "me"]);
    expect(response.status).toBe(502);
    expect(response.headers.has("location")).toBe(false);
  });

  it("sanitizes network failures and invalid configuration", async () => {
    fetchMock.mockRejectedValue(new TypeError("secret connection details"));
    const failed = await proxyRequest(makeRequest(), ["users", "me"]);
    expect(failed.status).toBe(502);
    expect(await failed.text()).not.toContain("secret");
    vi.stubEnv("RATEFREAK_API_URL", "https://secret:password@api.test");
    const unconfigured = await proxyRequest(makeRequest(), ["users", "me"]);
    expect(unconfigured.status).toBe(503);
    expect(await unconfigured.text()).not.toContain("password");
  });
});

describe("proxy cancellation", () => {
  it("aborts the upstream after ten seconds and returns 504", async () => {
    vi.useFakeTimers();
    let signal: AbortSignal | undefined;
    fetchMock.mockImplementation((_url, options) => new Promise((_resolve, reject) => {
      signal = options?.signal ?? undefined;
      signal?.addEventListener("abort", () => reject(signal?.reason), { once: true });
    }));
    const result = proxyRequest(makeRequest(), ["users", "me"]);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(signal?.aborted).toBe(true);
    expect((await result).status).toBe(504);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("propagates caller cancellation and does not return successful content", async () => {
    const controller = new AbortController();
    fetchMock.mockImplementation((_url, options) => new Promise((_resolve, reject) => {
      options?.signal?.addEventListener("abort", () => reject(options.signal?.reason), { once: true });
    }));
    const result = proxyRequest(makeRequest("users/me", { signal: controller.signal }), ["users", "me"]);
    const assertion = expect(result).rejects.toMatchObject({ name: "AbortError" });
    controller.abort();
    await assertion;
    expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(true);
  });
});

describe("catalog routes", () => {
  const id = "a1111111-1111-1111-1111-111111111111";
  it.each(["media", `media/${id}`])("allows GET %s", async (path) => {
    fetchMock.mockResolvedValue(new Response("{}"));
    expect((await proxyRequest(makeRequest(path), path.split("/"))).status).toBe(200);
  });
  it("forwards only canonical list parameters", async () => {
    fetchMock.mockResolvedValue(new Response("{}"));
    const response = await proxyRequest(makeRequest("media?q=++A+%26+B++&type=movie&page=2&pageSize=20"), ["media"]);
    expect(response.status).toBe(200);
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test:5080/api/v1/media?q=A+%26+B&type=movie&page=2&pageSize=20");
  });
  it.each(["media?q=a&q=b", "media?unknown=x"])("rejects invalid list query %s", async (path) => {
    expect((await proxyRequest(makeRequest(path), ["media"])).status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it.each(["media/not-a-guid", "media/providers/unknown", `media/${id}?q=x`])("keeps %s closed", async (path) => {
    expect((await proxyRequest(makeRequest(path), path.split("?")[0].split("/"))).status).toBe(404);
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it("rejects POST media", async () => {
    expect((await proxyRequest(makeRequest("media", { method: "POST" }), ["media"])).status).toBe(405);
  });
});

describe("provider proxy boundary", () => {
  it("forwards normalized search to the fixed origin", async () => {
    fetchMock.mockResolvedValue(Response.json({}));
    expect((await proxyRequest(makeRequest("media/external/search?q=++film+%26+music++&provider=tmdb&type=movie&page=2"), ["media", "external", "search"])).status).toBe(200);
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test:5080/api/v1/media/external/search?q=film+%26+music&provider=tmdb&type=movie&page=2");
  });
  it("canonicalizes preview identity", async () => {
    fetchMock.mockResolvedValue(Response.json({}));
    expect((await proxyRequest(makeRequest("media/external/tmdb/movie/00011"), ["media", "external", "tmdb", "movie", "00011"])).status).toBe(200);
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test:5080/api/v1/media/external/tmdb/movie/11");
  });
  it.each(["q=a&q=b&type=movie", "q=x&type=movie&url=https://evil.test", "q=%00&type=movie", "q=x&type=movie&page=501"])("rejects invalid external query %s", async query => {
    expect((await proxyRequest(makeRequest(`media/external/search?${query}`), ["media", "external", "search"])).status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it.each(["media/providers?q=x", "media/import?q=x", "media/external/tmdb/movie/11?q=x", "media/external/tmdb/album/11", "media/external/unknown/movie/11"])("rejects %s", async path => {
    expect((await proxyRequest(makeRequest(path), path.split("?")[0].split("/"))).status).toBe(404);
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it("requires mutation protections for import", async () => {
    vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test");
    vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
    vi.stubEnv("RATEFREAK_PROXY_SECRET", "a".repeat(64));
    vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
    expect((await proxyRequest(makeRequest("media/import", { method: "POST", headers: { Origin: "https://evil.test" } }), ["media", "import"])).status).toBe(403);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("personal-media boundary", () => {
  const id = "a1111111-1111-1111-1111-111111111111";
  it.each(["users/me/library?status=completed&type=movie&page=2&pageSize=20", `media/${id}/reviews?page=2&pageSize=20`])("forwards only approved list query %s", async path => {
    fetchMock.mockResolvedValue(Response.json({}));
    expect((await proxyRequest(makeRequest(path), path.split("?")[0].split("/"))).status).toBe(200);
    expect(fetchMock.mock.calls[0][0]).toBe(`http://api.test:5080/api/v1/${path}`);
  });
  it.each(["users/me/library?status=x", "users/me/library?status=planned&status=completed", `media/${id}/reviews?userId=other`])("rejects invalid list query %s", async path => {
    expect((await proxyRequest(makeRequest(path), path.split("?")[0].split("/"))).status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
  });
  it.each(["PUT", "PATCH", "DELETE"])("requires Origin/JSON/CSRF before %s forwarding", async method => {
    vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080"); vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test"); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback"); vi.stubEnv("RATEFREAK_PROXY_SECRET", "a".repeat(64));
    const path = method === "PATCH" ? `reviews/${id}` : `media/${id}/rating`;
    const call = (headers: Record<string, string>) => proxyRequest(makeRequest(path, { method, headers }), path.split("/"));
    expect((await call({ Origin: "https://evil.test" })).status).toBe(403);
    expect((await call({ Origin: "http://web.test", "Content-Type": "application/json" })).status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
    fetchMock.mockResolvedValue(Response.json({}));
    expect((await call({ Origin: "http://web.test", "Content-Type": "application/json", "X-CSRF-TOKEN": "csrf" })).status).toBe(200);
    expect(fetchMock.mock.calls[0][1]?.method).toBe(method);
  });
});

describe('follow proxy',()=>{
 const id='a1111111-1111-1111-1111-111111111111';
 it('forwards only bounded canonical paging',async()=>{fetchMock.mockResolvedValue(Response.json({}));const path=`users/${id}/followers`;expect((await proxyRequest(makeRequest(path+'?page=02&pageSize=20'),path.split('/'))).status).toBe(200);expect(fetchMock.mock.calls[0][0]).toBe(`http://api.test:5080/api/v1/${path}?page=2&pageSize=20`);});
 it.each(['page=0','page=10001','page=1&page=2','actor=spoof'])('rejects list query %s locally',async q=>{const path=`users/${id}/following`;expect((await proxyRequest(makeRequest(path+'?'+q),path.split('/'))).status).toBe(400);expect(fetchMock).not.toHaveBeenCalled();});
 it.each(['PUT','DELETE'])('preserves Origin and CSRF guards on %s',async method=>{
  vi.stubEnv('RATEFREAK_API_URL','http://localhost:5080');vi.stubEnv('RATEFREAK_WEB_ORIGIN','http://web.test');vi.stubEnv('RATEFREAK_CLIENT_IP_MODE','loopback');vi.stubEnv('RATEFREAK_PROXY_SECRET','a'.repeat(64));const path=`users/${id}/follow`;
  expect((await proxyRequest(makeRequest(path,{method,body:'{}',headers:{Origin:'https://foreign.test','Content-Type':'application/json','X-CSRF-TOKEN':'token'}}),path.split('/'))).status).toBe(403);
  expect((await proxyRequest(makeRequest(path,{method,body:'{}',headers:{Origin:'http://web.test','Content-Type':'application/json'}}),path.split('/'))).status).toBe(400);expect(fetchMock).not.toHaveBeenCalled();
 });
});

describe('review comment proxy',()=>{
 const id='a1111111-1111-1111-1111-111111111111';
 it('forwards exact public discussion and canonical comment paging',async()=>{fetchMock.mockImplementation(async()=>Response.json({}));const path=`reviews/${id}/comments`;expect((await proxyRequest(makeRequest(path+'?page=02&pageSize=20'),path.split('/'))).status).toBe(200);expect(fetchMock.mock.calls[0][0]).toBe(`http://api.test:5080/api/v1/${path}?page=2&pageSize=20`);expect((await proxyRequest(makeRequest(`reviews/${id}`),['reviews',id])).status).toBe(200);});
 it.each(['page=0','page=10001','page=1&page=2','author=spoof'])('rejects invalid comment query %s locally',async q=>{const path=`reviews/${id}/comments`;expect((await proxyRequest(makeRequest(path+'?'+q),path.split('/'))).status).toBe(400);expect(fetchMock).not.toHaveBeenCalled();});
 it.each(['POST','PATCH','DELETE'])('guards comment %s writes',async method=>{vi.stubEnv('RATEFREAK_API_URL','http://localhost:5080');vi.stubEnv('RATEFREAK_WEB_ORIGIN','http://web.test');vi.stubEnv('RATEFREAK_CLIENT_IP_MODE','loopback');vi.stubEnv('RATEFREAK_PROXY_SECRET','a'.repeat(64));const path=method==='POST'?`reviews/${id}/comments`:`comments/${id}`;
 expect((await proxyRequest(makeRequest(path,{method,body:'{}',headers:{Origin:'https://foreign.test','Content-Type':'application/json','X-CSRF-TOKEN':'token'}}),path.split('/'))).status).toBe(403);
 expect((await proxyRequest(makeRequest(path,{method,body:'{}',headers:{Origin:'http://web.test','Content-Type':'application/json'}}),path.split('/'))).status).toBe(400);expect(fetchMock).not.toHaveBeenCalled();});
});
