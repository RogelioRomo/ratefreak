import { beforeEach, expect, it, vi } from "vitest";
import { apiRequest } from "./request";
import { resetCsrfState } from "./csrf";

const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  resetCsrfState();
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

it("sends credentialed no-store GET requests with cancellation", async () => {
  fetchMock.mockResolvedValue(Response.json({ id: "account" }));
  const controller = new AbortController();
  expect(await apiRequest("users/me", { signal: controller.signal })).toEqual({ id: "account" });
  expect(fetchMock).toHaveBeenCalledWith("/api/v1/users/me", expect.objectContaining({ method: "GET", credentials: "same-origin", cache: "no-store", signal: controller.signal }));
});

it("encodes writes and obtains a fresh token for each sequential mutation", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "first" })).mockResolvedValueOnce(new Response(null, { status: 204 }))
    .mockResolvedValueOnce(Response.json({ requestToken: "second" })).mockResolvedValueOnce(new Response(null, { status: 204 }));
  expect(await apiRequest<void>("auth/logout", { method: "POST", body: { example: true } })).toBeUndefined();
  await apiRequest<void>("auth/logout", { method: "POST" });
  expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(["/api/v1/auth/csrf", "/api/v1/auth/logout", "/api/v1/auth/csrf", "/api/v1/auth/logout"]);
  expect(fetchMock.mock.calls[1][1]).toMatchObject({ body: '{"example":true}', headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": "first" } });
  expect(fetchMock.mock.calls[3][1]?.headers).toMatchObject({ "X-CSRF-TOKEN": "second" });
});

it.each(["https://evil.test", "//evil.test", "/users/me", "../users/me", "users/../me", "users\\me", "users%2fme", "users/%2e%2e/me", "users/me#fragment", "users/%252fme", "users/me\n"])("rejects unsafe path %j before fetching", async (path) => {
  await expect(apiRequest(path)).rejects.toThrow("Invalid API path.");
  expect(fetchMock).not.toHaveBeenCalled();
});

it("permits future query parameters without changing the destination", async () => {
  fetchMock.mockResolvedValue(Response.json({ items: [] }));
  await apiRequest("media?page=2&q=hello%20world");
  expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/media?page=2&q=hello%20world");
});

it("returns a safe error for malformed successful JSON", async () => {
  fetchMock.mockResolvedValue(new Response("<html>secret</html>", { status: 200 }));
  await expect(apiRequest("users/me")).rejects.toMatchObject({ message: "Invalid API response." });
});

it("does not retry a failed mutation", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "token" })).mockResolvedValueOnce(Response.json({ title: "Unavailable." }, { status: 500 }));
  await expect(apiRequest("auth/logout", { method: "POST" })).rejects.toMatchObject({ status: 500 });
  expect(fetchMock).toHaveBeenCalledTimes(2);
});

it("preserves cancellation while reading a failed response", async () => {
  fetchMock.mockResolvedValueOnce(new Response(new ReadableStream({
    start(controller) { controller.error(new DOMException("Cancelled", "AbortError")); },
  }), { status: 503, headers: { "Content-Type": "application/problem+json" } }));
  await expect(apiRequest("users/me")).rejects.toMatchObject({ name: "AbortError" });
});

it("prefers caller cancellation when an error body finishes during the abort", async () => {
  const controller = new AbortController();
  // Start with a non-aborted request; the body reader aborts during consumption.
  fetchMock.mockImplementationOnce(async () => {
    return new Response(new ReadableStream({
      pull(stream) {
        controller.abort();
        stream.enqueue(new TextEncoder().encode('{"title":"Unavailable."}'));
        stream.close();
      },
    }), { status: 503, headers: { "Content-Type": "application/problem+json" } });
  });
  await expect(apiRequest("users/me", { signal: controller.signal })).rejects.toMatchObject({ name: "AbortError" });
});
