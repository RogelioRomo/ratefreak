import { createServer, type Server } from "node:http";
import { createRequire } from "node:module";
import { syncBuiltinESMExports } from "node:module";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { register } from "../instrumentation";

vi.mock("./artwork/tmdb-posters", () => ({ startTmdbPosterScheduler: vi.fn() }));

const zlib = createRequire(import.meta.url)("node:zlib") as typeof import("node:zlib");
const methods = ["createZstdDecompress", "ZstdDecompress", "zstdDecompress", "zstdDecompressSync"] as const;
const original = Object.fromEntries(methods.map(name => [name, Object.getOwnPropertyDescriptor(zlib, name)!]));
const plaintext = "provider response";
const frames = {
  zstd: zlib.zstdCompressSync(plaintext),
  gzip: zlib.gzipSync(plaintext),
  deflate: zlib.deflateSync(plaintext),
  br: zlib.brotliCompressSync(plaintext),
};
let server: Server;
let origin: string;

beforeEach(async () => {
  vi.stubEnv("NEXT_RUNTIME", "nodejs");
  server = createServer((request, response) => {
    const encoding = request.url!.slice(1) as keyof typeof frames;
    response.writeHead(200, { "Content-Type": "text/plain", "Content-Encoding": encoding });
    response.end(frames[encoding]);
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Missing test listener.");
  origin = `http://127.0.0.1:${address.port}`;
});

afterEach(async () => {
  Object.defineProperties(zlib, original);
  syncBuiltinESMExports();
  server.closeAllConnections();
  await new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  // Restore the native image policy for other tests in this worker too.
  const sharp = createRequire(import.meta.url)("sharp") as typeof import("sharp");
  sharp.unblock({ operation: ["VipsForeignLoadHeif"] });
});

it("refuses zstd response decoding even after fetch has already initialized", async () => {
  expect(await (await fetch(`${origin}/zstd`)).text()).toBe(plaintext);

  await register();

  await expect(fetch(`${origin}/zstd`).then(response => response.text())).rejects.toThrow();
});

it.each(methods)("blocks the %s native decompression entrypoint", async name => {
  await register();

  expect(() => Reflect.apply(zlib[name], undefined, [frames.zstd])).toThrow(/decompression is disabled/i);
});

it.each(["gzip", "deflate", "br"] as const)("continues to decode %s provider responses", async encoding => {
  await register();

  expect(await (await fetch(`${origin}/${encoding}`)).text()).toBe(plaintext);
});
