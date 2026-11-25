import { chmodSync, mkdirSync, mkdtempSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { GET } from "@/app/tmdb-posters/[filename]/route";

const source = vi.hoisted(() => ({ fetchPoster: vi.fn() }));
vi.mock("./tmdb-poster-source", async importOriginal => ({ ...await importOriginal<typeof import("./tmdb-poster-source")>(), fetchTmdbPoster: source.fetchPoster }));

let directory: string;
beforeEach(() => {
  directory = mkdtempSync(join(realpathSync(tmpdir()), "tmdb-route-"));
  const cachePath = join(directory, "posters");
  mkdirSync(cachePath, { mode: 0o700 });
  const policyPath = join(directory, "policy.json");
  writeFileSync(policyPath, '{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}');
  chmodSync(policyPath, 0o600);
  vi.stubEnv("RATEFREAK_TMDB_CACHE_PATH", cachePath);
  vi.stubEnv("RATEFREAK_TMDB_POLICY_PATH", policyPath);
  source.fetchPoster.mockReset().mockResolvedValue({ bytes: Buffer.from([0xff, 0xd8, 0xff, 1]), type: "image/jpeg" });
});
afterEach(() => rmSync(directory, { force: true, recursive: true }));

async function poster(path: string, filename = "poster.jpg") {
  return GET(new Request(`https://ratefreak.test${path}`, {
    headers: { Cookie: "secret", Authorization: "secret", "X-Forwarded-For": "secret" },
  }), { params: Promise.resolve({ filename }) });
}

it("serves same-origin images without storing the response downstream", async () => {
  const result = await poster("/tmdb-posters/poster.jpg");
  expect(result.status).toBe(200);
  expect(result.headers.get("Cache-Control")).toBe("no-store");
  expect(result.headers.get("X-Content-Type-Options")).toBe("nosniff");
  expect(result.headers.get("Content-Type")).toBe("image/jpeg");
  expect(source.fetchPoster).toHaveBeenCalledExactlyOnceWith("poster.jpg", expect.any(AbortSignal));
});

it.each([
  ["/tmdb-posters/%70oster.jpg", "poster.jpg"],
  ["/tmdb-posters/poster%2Ejpg", "poster.jpg"],
  ["/tmdb-posters/%2Fposter.jpg", "/poster.jpg"],
  ["/tmdb-posters/poster.jpg?url=https://evil.test", "poster.jpg"],
  ["/tmdb-posters/poster.jpg?", "poster.jpg"],
  ["/tmdb-posters/poster.jpg#", "poster.jpg"],
  ["/tmdb-posters/poster.jpg/", "poster.jpg"],
  ["/tmdb-posters/poster.svg", "poster.svg"],
] as const)("rejects noncanonical route %s", async (path, filename) => {
  const result = await poster(path, filename);
  expect(result.status).toBe(404);
  expect(result.headers.get("Cache-Control")).toBe("no-store");
  expect(result.headers.get("X-Content-Type-Options")).toBe("nosniff");
  expect(source.fetchPoster).not.toHaveBeenCalled();
});
