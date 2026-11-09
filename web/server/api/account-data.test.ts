import { beforeEach, expect, it, vi } from "vitest";
import { proxyRequest } from "./proxy";
const upstream = vi.fn<typeof fetch>();
beforeEach(() => {
    vi.stubEnv("NODE_ENV", "development");
    vi.stubEnv("RATEFREAK_API_URL", "http://localhost:5080");
    vi.stubEnv("RATEFREAK_WEB_ORIGIN", "http://web.test");
    vi.stubEnv("RATEFREAK_CLIENT_IP_MODE", "loopback");
    vi.stubEnv("RATEFREAK_PROXY_SECRET", "a".repeat(64));
    vi.stubGlobal("fetch", upstream);
    upstream.mockReset();
});
function request(action = "export", signal?: AbortSignal) { return new Request(`http://web.test/api/v1/users/me/data/${action}`, { method: "POST", signal, body: "{}", headers: { Origin: "http://web.test", "Content-Type": "application/json", "X-CSRF-TOKEN": "csrf", Cookie: "ratefreak.lifecycle=grant; ratefreak.session=s" } }); }
it("streams the ZIP attachment without buffering and forwards the HttpOnly grant", async () => {
    const archive = new Response(new Uint8Array([80, 75, 3, 4]), { headers: { "Content-Type": "application/zip", "Content-Disposition": 'attachment; filename="my-data.zip"' } });
    const buffered = vi.spyOn(archive, "arrayBuffer");
    upstream.mockResolvedValue(archive);
    const response = await proxyRequest(request(), ["users", "me", "data", "export"]);
    expect(response.status).toBe(200);
    expect(response.headers.get("Content-Disposition")).toBe('attachment; filename="my-data.zip"');
    expect(response.headers.get("Cache-Control")).toBe("no-store");
    expect(buffered).not.toHaveBeenCalled();
    expect(new Headers(upstream.mock.calls[0][1]?.headers).get("Cookie")).toContain("ratefreak.lifecycle=grant");
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(new Uint8Array([80, 75, 3, 4]));
});
it("rejects an oversized archive before streaming", async () => {
    upstream.mockResolvedValue(new Response("zip", { headers: { "Content-Type": "application/zip", "Content-Length": String(256 * 1024 * 1024 + 1) } }));
    expect((await proxyRequest(request(), ["users", "me", "data", "export"])).status).toBe(502);
});
it("cancels the upstream when the download reader cancels", async () => {
    let cancelled = false;
    upstream.mockResolvedValue(new Response(new ReadableStream({ cancel() { cancelled = true; } }), { headers: { "Content-Type": "application/zip" } }));
    const response = await proxyRequest(request(), ["users", "me", "data", "export"]);
    await response.body!.cancel();
    expect(cancelled).toBe(true);
    expect(upstream.mock.calls[0][1]?.signal?.aborted).toBe(true);
});
it("keeps export generation alive for five minutes rather than ten seconds", async () => {
    vi.useFakeTimers();
    upstream.mockImplementation((_url, options) => new Promise((_resolve, reject) => options?.signal?.addEventListener("abort", () => reject(options.signal?.reason))));
    const pending = proxyRequest(request(), ["users", "me", "data", "export"]);
    await vi.advanceTimersByTimeAsync(10000);
    expect(upstream.mock.calls[0]?.[1]?.signal?.aborted).toBe(false);
    await vi.advanceTimersByTimeAsync(290000);
    expect((await pending).status).toBe(504);
    vi.useRealTimers();
});
