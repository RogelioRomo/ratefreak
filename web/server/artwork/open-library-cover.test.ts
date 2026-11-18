import { beforeEach, expect, it, vi } from "vitest";
import { EventEmitter } from "node:events";
import { Readable } from "node:stream";
import { fetchOpenLibraryCover, allowedCoverUrl } from "./open-library-cover";

const transport = vi.hoisted(() => ({ request: vi.fn(), lookup: vi.fn() }));
vi.mock("node:https", () => ({ request: transport.request }));
vi.mock("node:dns/promises", () => ({ lookup: transport.lookup }));
beforeEach(() => { transport.request.mockReset(); transport.lookup.mockReset(); transport.lookup.mockResolvedValue([{ address: "93.184.216.34", family: 4 }]); });
const id = "6498519";
const archive = "https://archive.org/download/olcovers649/olcovers649-L.zip/6498519-L.jpg";
const storage = "https://ia601804.us.archive.org/view_archive.php?archive=/17/items/olcovers649/olcovers649-L.zip&file=6498519-L.jpg";
const shardedArchive = "https://archive.org/download/l_covers_0012/l_covers_0012_89.zip/0012895064-L.jpg";
const shardedStorage = "https://ia800100.us.archive.org/view_archive.php?archive=/5/items/l_covers_0012/l_covers_0012_89.zip&file=0012895064-L.jpg";

it.each([
  ["12895064", shardedArchive], ["12895064", shardedStorage],
  ["10294562", "https://archive.org/download/l_covers_0010/l_covers_0010_29.zip/0010294562-L.jpg"],
  ["13815236", "https://ia601909.us.archive.org/view_archive.php?archive=/31/items/l_covers_0013/l_covers_0013_81.zip&file=0013815236-L.jpg"],
])("allows the observed sharded cover chain for %s: %s", (coverId, url) => {
  expect(allowedCoverUrl(new URL(url), coverId)).toBe(true);
});

it.each([
  shardedArchive.replace("0012895064-L.jpg", "0012895065-L.jpg"),
  shardedArchive.replaceAll("l_covers_0012", "l_covers_0013"),
  shardedArchive.replace("_89.zip", "_90.zip"),
  shardedArchive.replace("0012895064-L.jpg", "12895064-L.jpg"),
  shardedArchive.replace("https:", "http:"),
  shardedArchive.replace("archive.org", "archive.org.evil.test"),
  shardedArchive + "?download=1", shardedArchive + "#fragment",
  shardedStorage.replace("0012895064-L.jpg", "0012895065-L.jpg"),
  shardedStorage.replace("_89.zip", "_90.zip"),
  shardedStorage.replace("/5/items/", "/5/private/"),
  shardedStorage.replace("ia800100.us.archive.org", "evil.archive.org"),
  shardedStorage + "&file=0012895064-L.jpg", shardedStorage + "&redirect=https://evil.test",
])("rejects mismatched or unrelated sharded redirects %s", url => {
  expect(allowedCoverUrl(new URL(url), "12895064")).toBe(false);
});

it.each([`https://covers.openlibrary.org/b/id/${id}-L.jpg?default=false`, archive, storage])("allows the observed cover chain %s", url => {
  expect(allowedCoverUrl(new URL(url), id)).toBe(true);
});
it.each([
  "https://evil.test/6498519-L.jpg", "http://archive.org/download/olcovers649/olcovers649-L.zip/6498519-L.jpg",
  "https://u:p@archive.org/download/olcovers649/olcovers649-L.zip/6498519-L.jpg",
  "https://archive.org/download/olcovers649/olcovers649-L.zip/123-L.jpg",
  "https://ia601804.us.archive.org/view_archive.php?archive=/17/items/secret.zip&file=6498519-L.jpg",
  storage + "&redirect=https://evil.test", "https://127.0.0.1/private.jpg",
])("refuses unrelated redirects %s", url => expect(allowedCoverUrl(new URL(url), id)).toBe(false));

function respond(responses: Array<{ status: number; headers: Record<string, string>; body?: Uint8Array }>) {
  transport.request.mockImplementation((_url, options, callback) => {
    expect(options.headers.Cookie).toBeUndefined(); expect(options.headers.Authorization).toBeUndefined();
    const request = new EventEmitter() as EventEmitter & { end: () => void; destroy: () => void };
    request.destroy = vi.fn(); request.end = () => {
      const next = responses.shift()!;
      const response = Object.assign(Readable.from([Buffer.from(next.body ?? [])]), { statusCode: next.status, headers: next.headers });
      queueMicrotask(() => callback(response));
    };
    return request;
  });
}
it("follows only the validated chain and returns bounded raster bytes", async () => {
  respond([{ status: 302, headers: { location: archive } }, { status: 302, headers: { location: storage } }, { status: 200, headers: { "content-type": "image/jpeg" }, body: new Uint8Array([1, 2, 3]) }]);
  const response = await fetchOpenLibraryCover(id, new AbortController().signal);
  expect(response.status).toBe(200); expect(response.headers.get("content-type")).toBe("image/jpeg");
  expect([...new Uint8Array(await response.arrayBuffer())]).toEqual([1, 2, 3]); expect(transport.request).toHaveBeenCalledTimes(3);
});
it("retrieves raster bytes through the observed padded-ID sharded redirects", async () => {
  respond([{ status: 302, headers: { location: shardedArchive } }, { status: 302, headers: { location: shardedStorage } }, { status: 200, headers: { "content-type": "image/jpeg" }, body: new Uint8Array([1, 2, 3]) }]);
  const response = await fetchOpenLibraryCover("12895064", new AbortController().signal);
  expect(response.status).toBe(200);
  expect([...new Uint8Array(await response.arrayBuffer())]).toEqual([1, 2, 3]);
});
it("rejects private DNS results before opening a socket", async () => {
  transport.lookup.mockResolvedValue([{ address: "127.0.0.1", family: 4 }]);
  expect((await fetchOpenLibraryCover(id, new AbortController().signal)).status).toBe(502); expect(transport.request).not.toHaveBeenCalled();
});
it("never follows an unapproved redirect and refuses non-raster or oversized content", async () => {
  respond([{ status: 302, headers: { location: "http://127.0.0.1/private" } }]);
  expect((await fetchOpenLibraryCover(id, new AbortController().signal)).status).toBe(502); expect(transport.request).toHaveBeenCalledTimes(1);
  respond([{ status: 200, headers: { "content-type": "image/svg+xml" }, body: new Uint8Array([1]) }]);
  expect((await fetchOpenLibraryCover(id, new AbortController().signal)).status).toBe(502);
  respond([{ status: 200, headers: { "content-type": "image/jpeg", "content-length": "5242881" } }]);
  expect((await fetchOpenLibraryCover(id, new AbortController().signal)).status).toBe(502);
});
it("rejects invalid IDs without DNS or network access", async () => {
  expect((await fetchOpenLibraryCover("../private", new AbortController().signal)).status).toBe(404); expect(transport.lookup).not.toHaveBeenCalled();
});

it("bounds distinct cover fetches and waiting work, releases cancelled waiters and returns capacity", async () => {
  transport.request.mockImplementation((_url, options) => {
    const outgoing = new EventEmitter() as EventEmitter & { end: () => void; destroy: () => void };
    outgoing.end = () => {}; outgoing.destroy = () => {};
    options.signal.addEventListener("abort", () => outgoing.emit("error", options.signal.reason), { once: true });
    return outgoing;
  });
  const callers = Array.from({ length: 72 }, () => new AbortController());
  const pending = callers.map((caller, index) => fetchOpenLibraryCover(String(10000 + index), caller.signal));
  try {
    await vi.waitFor(() => expect(transport.request.mock.calls.length).toBeGreaterThanOrEqual(8));
    expect(transport.request).toHaveBeenCalledTimes(8);
    const excess = await fetchOpenLibraryCover("99999", new AbortController().signal);
    expect(excess.status).toBe(503); expect(excess.headers.get("retry-after")).toBe("1");
  } finally {
    // Cancel queued work first: it must not start network fetches while the
    // admitted operations relinquish their slots.
    for (const caller of [...callers.slice(8), ...callers.slice(0, 8)]) caller.abort();
    await Promise.all(pending);
  }
  expect(transport.request).toHaveBeenCalledTimes(8);
  respond([{ status: 200, headers: { "content-type": "image/jpeg" }, body: new Uint8Array([1, 2, 3]) }]);
  expect((await fetchOpenLibraryCover(id, new AbortController().signal)).status).toBe(200);
});
