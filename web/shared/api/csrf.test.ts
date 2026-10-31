import { beforeEach, expect, it, vi } from "vitest";
import { getCsrfToken, resetCsrfState } from "./csrf";
import { apiRequest } from "./request";

const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  resetCsrfState();
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

it("coalesces pending issuance, but does not cache a completed token", async () => {
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
  const first = getCsrfToken();
  const second = getCsrfToken();
  expect(fetchMock).toHaveBeenCalledTimes(1);
  resolve(Response.json({ requestToken: "shared" }));
  expect(await Promise.all([first, second])).toEqual(["shared", "shared"]);
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "fresh" }));
  expect(await getCsrfToken()).toBe("fresh");
  expect(fetchMock).toHaveBeenCalledTimes(2);
});

it("does not cache failed issuance or send the mutation", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ title: "Slow down." }, { status: 429 }));
  await expect(apiRequest("auth/logout", { method: "POST" })).rejects.toMatchObject({ status: 429 });
  expect(fetchMock).toHaveBeenCalledTimes(1);
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "recovered" }));
  expect(await getCsrfToken()).toBe("recovered");
});

it("rejects invalid token responses", async () => {
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: null }));
  await expect(getCsrfToken()).rejects.toThrow("Invalid API response.");
});

it("aborting one waiter does not cancel the other or send the aborted mutation", async () => {
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
  const controller = new AbortController();
  const aborted = apiRequest("auth/logout", { method: "POST", signal: controller.signal });
  const assertion = expect(aborted).rejects.toMatchObject({ name: "AbortError" });
  const survivor = getCsrfToken();
  controller.abort();
  await assertion;
  expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(false);
  resolve(Response.json({ requestToken: "survives" }));
  expect(await survivor).toBe("survives");
  expect(fetchMock).toHaveBeenCalledTimes(1);
});

it("reset invalidates old waiters even if an old request completes late", async () => {
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
  const stale = getCsrfToken();
  const assertion = expect(stale).rejects.toMatchObject({ name: "AbortError" });
  resetCsrfState();
  resolve(Response.json({ requestToken: "old" }));
  await assertion;
  fetchMock.mockResolvedValueOnce(Response.json({ requestToken: "new" }));
  expect(await getCsrfToken()).toBe("new");
});

it("preserves cancellation while reading a successful token response", async () => {
  fetchMock.mockResolvedValueOnce(new Response(new ReadableStream({
    start(controller) { controller.error(new DOMException("Cancelled", "AbortError")); },
  }), { headers: { "Content-Type": "application/json" } }));
  await expect(getCsrfToken()).rejects.toMatchObject({ name: "AbortError" });
});
