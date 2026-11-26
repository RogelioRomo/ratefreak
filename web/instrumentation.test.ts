import sharp from "sharp";
import { createRequire } from "node:module";
import { afterEach, beforeAll, beforeEach, expect, it, vi } from "vitest";
import { register } from "./instrumentation";

const { optimizeImage } = createRequire(import.meta.url)("next/dist/server/image-optimizer/transform") as typeof import("next/dist/server/image-optimizer/transform");

vi.mock("./server/artwork/tmdb-posters", () => ({ startTmdbPosterScheduler: vi.fn() }));

const heifLoader = { operation: ["VipsForeignLoadHeif"] };
const pixel = { create: { width: 2, height: 2, channels: 3 as const, background: "red" } };
let avif: Buffer;

beforeAll(async () => {
  avif = await sharp(pixel).avif().toBuffer();
});

beforeEach(() => {
  sharp.unblock(heifLoader);
  vi.stubEnv("NEXT_RUNTIME", "nodejs");
});

afterEach(() => {
  sharp.unblock(heifLoader);
});

it("rejects AVIF decoding after server startup, including disguised image bytes", async () => {
  expect((await sharp(avif).metadata()).format).toBe("heif");

  await register();

  await expect(optimizeImage({ buffer: avif, contentType: "image/webp", width: 1, quality: 75 })).rejects.toThrow(/unsupported image format/i);
  await expect(sharp(avif).metadata()).rejects.toThrow(/unsupported image format/i);
  await expect(sharp(avif).resize(1, 1).webp().toBuffer()).rejects.toThrow(/unsupported image format/i);
});

it.each(["jpeg", "png", "webp", "gif"] as const)("continues to optimize %s images after startup", async format => {
  const input = await sharp(pixel).toFormat(format).toBuffer();

  await register();

  const output = await optimizeImage({ buffer: input, contentType: "image/webp", width: 1, quality: 75 });
  expect(await sharp(output).metadata()).toMatchObject({ format: "webp", width: 1, height: 1 });
});
