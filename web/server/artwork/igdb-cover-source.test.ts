import { EventEmitter } from "node:events";
import { Readable } from "node:stream";
import { beforeEach, expect, it, vi } from "vitest";
import { fetchIgdbCover } from "./igdb-cover-source";

const transport = vi.hoisted(() => ({ request: vi.fn(), lookup: vi.fn() }));
vi.mock("node:https", () => ({ request: transport.request }));
vi.mock("node:dns/promises", () => ({ lookup: transport.lookup }));

const picture = Buffer.from([0xff, 0xd8, 0xff, 0xe0]);
const publicAddresses = [{ address: "93.184.216.34", family: 4 }, { address: "2606:4700:4700::1111", family: 6 }];
const filename = "poster.jpg";
const signal = () => new AbortController().signal;

beforeEach(() => {
  transport.request.mockReset();
  transport.lookup.mockReset().mockResolvedValue(publicAddresses);
});

function respond(statusCode: number, headers: Record<string, string>, body: Buffer[] = [picture]) {
  transport.request.mockImplementation((_url, _options, callback) => {
    const request = new EventEmitter() as EventEmitter & { end: () => void };
    request.end = () => queueMicrotask(() => callback(Object.assign(Readable.from(body), { statusCode, headers })));
    return request;
  });
}

it("fetches only t_cover_big bytes on the fixed host and pins already-checked DNS results", async () => {
  respond(200, { "content-type": "image/jpeg", "cache-control": "max-age=31536000" });
  expect(await fetchIgdbCover(filename, signal())).toEqual({ bytes: picture, type: "image/jpeg" });
  expect(transport.lookup).toHaveBeenCalledExactlyOnceWith("images.igdb.com", { all: true });
  const [url, options] = transport.request.mock.calls[0];
  expect(url.href).toBe("https://images.igdb.com/igdb/image/upload/t_cover_big/poster.jpg");
  expect(Object.keys(options.headers).map((key: string) => key.toLowerCase())).toEqual(["accept", "user-agent", "cache-control"]);
  const connect = vi.fn();
  options.lookup("images.igdb.com", { all: true, family: 6 }, connect);
  expect(connect).toHaveBeenCalledExactlyOnceWith(null, [publicAddresses[1]]);
});

it.each(["..%2fsecret.jpg", "../secret.jpg", "https://images.igdb.com/igdb/image/upload/t_cover_big/poster.jpg", "evil.jpg?url=bad", "photo.jpg\n", "photo.svg"])("does no network I/O for invalid source %s", async source => {
  await expect(fetchIgdbCover(source, signal())).rejects.toThrow();
  expect(transport.lookup).not.toHaveBeenCalled();
});

it.each([["127.0.0.1", 4], ["169.254.169.254", 4], ["::ffff:127.0.0.1", 6], ["fc00::1", 6], ["192.0.2.1", 4], ["not-an-ip", 4]])("refuses mixed DNS containing nonpublic or malformed %s", async (address, family) => {
  transport.lookup.mockResolvedValue([...publicAddresses, { address, family }]);
  await expect(fetchIgdbCover(filename, signal())).rejects.toThrow();
  expect(transport.request).not.toHaveBeenCalled();
});

it("does not follow a redirect even to another IGDB path", async () => {
  respond(302, { location: "https://images.igdb.com/igdb/image/upload/t_cover_big/other.jpg" });
  await expect(fetchIgdbCover(filename, signal())).rejects.toThrow();
  expect(transport.request).toHaveBeenCalledTimes(1);
});

it("distinguishes a missing poster from an upstream failure", async () => {
  respond(404, {});
  expect(await fetchIgdbCover(filename, signal())).toBeNull();
  respond(503, {});
  await expect(fetchIgdbCover(filename, signal())).rejects.toThrow();
});

it.each([
  [{ "content-type": "image/jpeg", "content-length": "5242881" }, [picture]],
  [{ "content-type": "image/jpeg" }, [picture, Buffer.alloc(5 * 1024 * 1024)]],
  [{ "content-type": "image/jpeg", "content-encoding": "gzip" }, [picture]],
  [{ "content-type": "image/jpeg" }, [Buffer.from("<html>not an image</html>")]],
  [{ "content-type": "image/svg+xml" }, [picture]],
] as const)("refuses unsupported or oversized upstream content %#", async (headers, body) => {
  respond(200, headers, [...body]);
  await expect(fetchIgdbCover(filename, signal())).rejects.toThrow();
});

it("cancels pending DNS without opening a socket", async () => {
  transport.lookup.mockImplementation(() => new Promise(() => {}));
  const caller = new AbortController();
  const pending = fetchIgdbCover(filename, caller.signal);
  caller.abort();
  await expect(pending).rejects.toThrow();
  expect(transport.request).not.toHaveBeenCalled();
});
