import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const upstream = vi.fn<typeof fetch>();
const google = "https://accounts.google.com/o/oauth2/v2/auth?client_id=client&redirect_uri=http%3A%2F%2Fweb.test%2Fapi%2Fv1%2Fauth%2Fexternal%2Fgoogle%2Fcallback&state=protected&code_challenge=pkce&code_challenge_method=S256";
function request(path: string, options: RequestInit = {}) { return new Request(`http://web.test/api/v1/${path}`, options); }
function post(path: string, extra: Record<string, string> = {}) { return request(path, { method: "POST", headers: { Origin: "http://web.test", "Content-Type": "application/json", "X-CSRF-TOKEN": "csrf", ...extra }, body: "{}" }); }
function call(req: Request) { return proxyRequest(req, new URL(req.url).pathname.slice(8).split("/")); }
function redirect(location: string, cookies: string[] = []) { const headers = new Headers({ Location: location }); cookies.forEach(c => headers.append("Set-Cookie", c)); return new Response(null, { status: 302, headers }); }
beforeEach(() => {
  vi.stubEnv("NODE_ENV", "development"); vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
  vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test"); vi.stubEnv("RATEFREAK_PROXY_SECRET", "a".repeat(64)); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
  upstream.mockReset(); vi.stubGlobal("fetch", upstream);
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); vi.unstubAllEnvs(); });
it("returns a validated authorization navigation and correlation cookie", async () => {
  upstream.mockResolvedValue(redirect(google, [".AspNetCore.Correlation.google.flow=N; Path=/api/v1/auth/external/google/callback; HttpOnly; SameSite=Lax"]));
  const response = await call(post("auth/external/google/start"));
  expect(response.status).toBe(200); expect(await response.json()).toEqual({ authorizationUrl: google });
  expect(response.headers.getSetCookie()).toHaveLength(1); expect(response.headers.get("cache-control")).toBe("no-store");
  const headers = new Headers(upstream.mock.calls[0][1]?.headers);
  expect(headers.get("X-RateFreak-Browser-Origin")).toBe("http://web.test");
  expect(headers.get("X-CSRF-TOKEN")).toBe("csrf"); expect(upstream.mock.calls[0][1]?.redirect).toBe("manual");
});
it.each([
  "https://evil.test/", google.replace("accounts.google.com", "accounts.google.com.evil.test"),
  google.replace("/o/oauth2/v2/auth", "/anything"), google.replace("https:", "http:"),
  google + "&state=another", google + "#fragment", google.replace("web.test", "evil.test"),
  google.replace("state=protected", "state="), google.replace("accounts.google.com", "user@accounts.google.com"),
  "https://github.com/login/oauth/authorize?state=x"
])("rejects an unsafe provider redirect", async location => {
  upstream.mockResolvedValue(redirect(location)); const response = await call(post("auth/external/google/start"));
  expect(response.status).toBe(502); expect(response.headers.has("location")).toBe(false); expect(upstream).toHaveBeenCalledTimes(1);
});
it.each(["auth/external/google/start", "users/me/logins/github/start"])("guards OAuth start %s", async path => {
  expect((await call(post(path, { Origin: "https://evil.test" }))).status).toBe(403);
  const noCsrf = post(path); noCsrf.headers.delete("X-CSRF-TOKEN"); expect((await call(noCsrf)).status).toBe(400);
  expect(upstream).not.toHaveBeenCalled();
});
it("follows only fixed completion and carries newly issued chunked external tickets", async () => {
  upstream.mockResolvedValueOnce(redirect("/api/v1/auth/external/complete", [
    "ratefreak.external=chunks-2; Path=/; HttpOnly", "ratefreak.externalC1=first; Path=/; HttpOnly", "ratefreak.externalC2=second; Path=/; HttpOnly",
    ".AspNetCore.Correlation.google.flow=; Path=/api/v1/auth/external/google/callback; Max-Age=0"
  ])).mockResolvedValueOnce(Response.json({ requiresRegistration: true, provider: "google" }, { headers: { "Set-Cookie": "ratefreak.external=pending; Path=/; HttpOnly" } }));
  const response = await call(request("auth/external/google/callback?code=private&state=protected&scope=email", { headers: { Cookie: ".AspNetCore.Correlation.google.flow=N; unrelated=secret" } }));
  expect(response.status).toBe(303); expect(response.headers.get("location")).toBe("http://web.test/auth/external/register");
  expect(String(upstream.mock.calls[0][0])).toBe("http://localhost:5080/api/v1/auth/external/google/callback?code=private&state=protected");
  expect(String(upstream.mock.calls[1][0])).toBe("http://localhost:5080/api/v1/auth/external/complete");
  const cookies = new Headers(upstream.mock.calls[1][1]?.headers).get("cookie");
  expect(cookies).toContain("ratefreak.externalC1=first"); expect(cookies).toContain("ratefreak.externalC2=second"); expect(cookies).not.toContain("unrelated");
  expect(response.headers.getSetCookie()).toHaveLength(5); expect(response.headers.get("referrer-policy")).toBe("no-referrer");
});
it.each([[{ id: "a1111111-1111-1111-1111-111111111111", handle: "reader", displayName: "Reader", email: "r@example.test", createdAt: "2026-01-01T00:00:00Z" }, "/"], [{ linked: true, provider: "github" }, "/account"]])("redirects completed authentication to a clean fixed destination", async (body, destination) => {
  upstream.mockResolvedValueOnce(redirect("http://web.test/api/v1/auth/external/complete"))
    .mockResolvedValueOnce(Response.json(body, { headers: { "Set-Cookie": "ratefreak.session=s; Path=/; HttpOnly" } }));
  const response = await call(request("auth/external/github/callback?code=private&state=protected"));
  expect(response.headers.get("location")).toBe(`http://web.test${destination}`); expect(response.headers.getSetCookie()[0]).toContain("ratefreak.session=s");
});
it.each(["https://evil.test/api/v1/auth/external/complete", "/api/v1/users/me", "/api/v1/auth/external/complete?next=evil", "//evil.test/api/v1/auth/external/complete"])("never follows an unexpected callback redirect", async location => {
  upstream.mockResolvedValue(redirect(location)); const response = await call(request("auth/external/google/callback?state=x&code=y"));
  expect(response.headers.get("location")).toBe("http://web.test/auth/external/error?reason=failed"); expect(upstream).toHaveBeenCalledTimes(1);
});
it("sanitizes cancellation/provider errors and forwards deletion cookies", async () => {
  upstream.mockResolvedValue(Response.json({ title: "private upstream error" }, { status: 400, headers: { "Set-Cookie": "ratefreak.external=; Path=/; Max-Age=0" } }));
  const response = await call(request("auth/external/google/callback?error=access_denied&state=x"));
  expect(response.headers.get("location")).toBe("http://web.test/auth/external/error?reason=failed"); expect(await response.text()).not.toContain("private");
  expect(response.headers.getSetCookie()[0]).toContain("Max-Age=0");
});
it.each(["state=a&state=b&code=x", "state=x&code=" + "a".repeat(8192)])("rejects ambiguous or excessive callback queries locally", async query => {
  const response = await call(request(`auth/external/google/callback?${query}`)); expect(response.status).toBe(400); expect(upstream).not.toHaveBeenCalled();
});
it.each(["auth/external/complete", "auth/external/unknown/callback", "auth/external/google/callback/extra", "auth/external/google/start?returnTo=https://evil.test"])("keeps unapproved OAuth paths closed", async path => {
  expect((await call(request(path))).status).toBe(404); expect(upstream).not.toHaveBeenCalled();
});
it.each(["ratefreak.external=a; ratefreak.external=b", "ratefreak.external=" + "a".repeat(16385)])("rejects unsafe external cookies before fetching", async cookie => {
  expect((await call(request("auth/external/google/callback?state=x&code=y", { headers: { Cookie: cookie } }))).status).toBe(400); expect(upstream).not.toHaveBeenCalled();
});
it("forwards pending onboarding tickets only on approved JSON routes", async () => {
  upstream.mockResolvedValue(Response.json({ requiresRegistration: true, provider: "github" }));
  expect((await call(request("auth/external/pending", { headers: { Cookie: "ratefreak.external=pending; other=secret" } }))).status).toBe(200);
  expect(new Headers(upstream.mock.calls[0][1]?.headers).get("Cookie")).toBe("ratefreak.external=pending");
});
it("times out OAuth starts without returning authorization data", async () => {
  vi.useFakeTimers(); upstream.mockImplementation((_url, options) => new Promise((_, reject) => options?.signal?.addEventListener("abort", () => reject(options.signal?.reason))));
  const pending = call(post("auth/external/google/start")); await vi.advanceTimersByTimeAsync(10_000);
  expect((await pending).status).toBe(504); expect(vi.getTimerCount()).toBe(0);
});
it("aborts callback completion when the browser disconnects", async () => {
  const controller = new AbortController();
  upstream.mockImplementation((_url, options) => new Promise((_, reject) => options?.signal?.addEventListener("abort", () => reject(options.signal?.reason))));
  const pending = call(request("auth/external/google/callback?state=x&code=y", { signal: controller.signal }));
  const assertion = expect(pending).rejects.toMatchObject({ name: "AbortError" }); controller.abort(); await assertion;
});
it.each([{}, { requiresRegistration: true, provider: "unknown" }, { linked: true, provider: "github" }])("rejects malformed or mismatched completion data", async body => {
  upstream.mockResolvedValueOnce(redirect("/api/v1/auth/external/complete")).mockResolvedValueOnce(Response.json(body));
  const response = await call(request("auth/external/google/callback?state=x&code=y"));
  expect(response.headers.get("location")).toBe("http://web.test/auth/external/error?reason=failed");
});
it("preserves Retry-After on limited starts", async () => {
  upstream.mockResolvedValue(Response.json({ title: "limit" }, { status: 429, headers: { "Retry-After": "60" } }));
  const response = await call(post("auth/external/google/start")); expect(response.status).toBe(429); expect(response.headers.get("Retry-After")).toBe("60");
});
it("rejects external ticket chunk counts beyond the supported bound", async () => {
  const response = await call(request("auth/external/google/callback?state=x&code=y", { headers: { Cookie: "ratefreak.external=chunks-6; ratefreak.externalC6=extra" } }));
  expect(response.status).toBe(400); expect(upstream).not.toHaveBeenCalled();
});
it("rejects excessive selected correlation cookies", async () => {
  const cookies = Array.from({ length: 33 }, (_, i) => `.AspNetCore.Correlation.google.flow${i}=N`).join("; ");
  expect((await call(request("auth/external/google/callback?state=x&code=y", { headers: { Cookie: cookies } }))).status).toBe(400); expect(upstream).not.toHaveBeenCalled();
});
it("only forwards production host cookies and ignores development tickets", async () => {
  vi.stubEnv("NODE_ENV", "production"); vi.stubEnv("RATEFREAK_API_URL", "https://api.test"); vi.stubEnv("VERCEL", "1"); vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "vercel");
  upstream.mockResolvedValueOnce(redirect("/api/v1/auth/external/complete")).mockResolvedValueOnce(Response.json({ requiresRegistration: true, provider: "google" }));
  await call(request("auth/external/google/callback?state=x&code=y", { headers: { "x-vercel-forwarded-for": "192.0.2.1", Cookie: "__Host-ratefreak.external=production; ratefreak.external=development; __Host-ratefreak.session=s; unrelated=x" } }));
  expect(new Headers(upstream.mock.calls[0][1]?.headers).get("Cookie")).toBe("__Host-ratefreak.external=production; __Host-ratefreak.session=s");
});
it("bounds completion response bodies", async () => {
  upstream.mockResolvedValueOnce(redirect("/api/v1/auth/external/complete")).mockResolvedValueOnce(new Response("x".repeat(65537)));
  const response = await call(request("auth/external/google/callback?state=x&code=y")); expect(response.headers.get("location")).toContain("/auth/external/error?reason=failed");
});
