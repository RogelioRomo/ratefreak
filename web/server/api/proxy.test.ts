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
    for (const method of ["POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"]) {
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
