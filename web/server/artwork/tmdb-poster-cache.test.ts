import { spawn } from "node:child_process";
import { once } from "node:events";
import { chmodSync, copyFileSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, realpathSync, renameSync, rmSync, statSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { TmdbPosterCache } from "./tmdb-poster-cache";
import { withTmdbPosterLock } from "./tmdb-poster-lock";
import type { TmdbPosterPicture } from "./tmdb-poster-source";

let directory: string;
let cachePath: string;
let policyPath: string;
let now: number;
const firstPicture: TmdbPosterPicture = { type: "image/jpeg", bytes: Buffer.from([0xff, 0xd8, 0xff, 1]) };
const newerPicture: TmdbPosterPicture = { type: "image/jpeg", bytes: Buffer.from([0xff, 0xd8, 0xff, 2]) };
const filename = "same.jpg";
const lockFilename = ".tmdb-lock";

beforeEach(() => {
  directory = mkdtempSync(join(realpathSync(tmpdir()), "tmdb-cache-"));
  cachePath = join(directory, "posters");
  policyPath = join(directory, "policy.json");
  mkdirSync(cachePath, { mode: 0o700 });
  setPolicy();
  setClock("2025-09-30T01:02:03.004Z");
});
afterEach(() => rmSync(directory, { force: true, recursive: true }));

function setClock(iso: string) { now = Date.parse(iso); }
function setPolicy(terminated = false, blockedPosters: string[] = []) {
  const temp = `${policyPath}.new`;
  writeFileSync(temp, JSON.stringify({ format: 1, terminated, blockedIdentities: [], blockedPosters }), { mode: 0o600 });
  renameSync(temp, policyPath);
}
function setup(capacity?: { entries: number; bytes: number }) {
  const fetchPoster = vi.fn<(filename: string, signal: AbortSignal) => Promise<TmdbPosterPicture | null>>().mockResolvedValue(firstPicture);
  const cache = new TmdbPosterCache({ directory: cachePath, policyPath, now: () => now, fetchPoster, capacity });
  return { cache, fetchPoster };
}
async function payload(response: Response) { return Buffer.from(await response.arrayBuffer()); }
function posterFiles() { return readdirSync(cachePath).filter(name => name !== lockFilename); }

it("refreshes the same filename at five clamped UTC months, across a restart", async () => {
  const { cache } = setup();
  expect(await payload(await cache.get(filename))).toEqual(firstPicture.bytes);

  const restarted = setup();
  restarted.fetchPoster.mockResolvedValue(newerPicture);
  setClock("2026-02-28T01:02:03.003Z");
  await restarted.cache.sweep();
  expect(restarted.fetchPoster).not.toHaveBeenCalled();
  expect(await payload(await restarted.cache.get(filename))).toEqual(firstPicture.bytes);

  setClock("2026-02-28T01:02:03.004Z");
  await restarted.cache.sweep();
  expect(await payload(await restarted.cache.get(filename))).toEqual(newerPicture.bytes);
  expect(restarted.fetchPoster).toHaveBeenCalledTimes(1);

  setClock("2026-03-30T01:02:03.004Z");
  expect(await payload(await restarted.cache.get(filename))).toEqual(newerPicture.bytes);
  expect(restarted.fetchPoster).toHaveBeenCalledTimes(1);
});

it("removes unvisited posters exactly at six calendar months despite a failed refresh", async () => {
  setClock("2023-08-31T03:15:00.000Z");
  const { cache, fetchPoster } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  fetchPoster.mockRejectedValue(new Error("upstream outage"));
  setClock("2024-02-29T03:14:59.999Z");
  await cache.sweep();
  expect(await payload(await cache.get(filename))).toEqual(firstPicture.bytes);
  setClock("2024-02-29T03:15:00.000Z");
  await cache.sweep();
  expect(posterFiles()).toEqual([]);
  expect((await cache.get(filename)).status).toBe(502);
});

it("enforces hard six-month expiry on requests even if the scheduler never ran", async () => {
  const { cache, fetchPoster } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  fetchPoster.mockRejectedValue(new Error("outage"));
  setClock("2026-03-30T01:02:03.004Z");
  expect((await cache.get(filename)).status).toBe(502);
  expect(posterFiles()).toEqual([]);
  fetchPoster.mockResolvedValue(newerPicture);
  expect(await payload(await cache.get(filename))).toEqual(newerPicture.bytes);
});

it("drops a retained poster when an authoritative refresh returns no poster", async () => {
  const { cache, fetchPoster } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  setClock("2026-02-28T01:02:03.004Z");
  fetchPoster.mockResolvedValue(null);
  await cache.sweep();
  expect(posterFiles()).toEqual([]);
  expect((await cache.get(filename)).status).toBe(404);
});

it("checks policy before hits and removes blocked and terminated files during reconciliation", async () => {
  const { cache, fetchPoster } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  expect((await cache.get("second.jpg")).status).toBe(200);
  setPolicy(false, [filename]);
  expect((await cache.get(filename)).status).toBe(404);
  await cache.sweep();
  expect(posterFiles()).toEqual(["second.jpg"]);
  setPolicy(true);
  expect((await cache.get("second.jpg")).status).toBe(404);
  await cache.sweep();
  expect(posterFiles()).toEqual([]);
  expect(fetchPoster).toHaveBeenCalledTimes(2);
});

it("discards an upstream response when termination takes effect in flight", async () => {
  const { cache, fetchPoster } = setup();
  let finish!: (value: TmdbPosterPicture) => void;
  fetchPoster.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const result = cache.get(filename);
  await vi.waitFor(() => expect(fetchPoster).toHaveBeenCalledOnce());
  setPolicy(true);
  finish(firstPicture);
  expect((await result).status).toBe(404);
  expect(posterFiles()).toEqual([]);
});

it("fails closed for missing policy and still deletes expired file bytes", async () => {
  const { cache, fetchPoster } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  rmSync(policyPath);
  expect((await cache.get(filename)).status).toBe(503);
  setClock("2026-03-30T01:02:03.004Z");
  await cache.sweep();
  expect(posterFiles()).toEqual([]);
  expect(fetchPoster).toHaveBeenCalledTimes(1);
});

it("rejects malformed, insecure and future-dated records after restart", async () => {
  const { cache, fetchPoster } = setup();
  for (const file of ["broken.jpg", "future.jpg", "mode.jpg"]) expect((await cache.get(file)).status).toBe(200);
  const broken = join(cachePath, "broken.jpg");
  writeFileSync(broken, readFileSync(broken).subarray(0, 5));
  chmodSync(join(cachePath, "mode.jpg"), 0o644);
  setClock("2025-09-30T01:02:03.003Z");
  await cache.sweep();
  expect(posterFiles()).toEqual([]);
  expect(fetchPoster).toHaveBeenCalledTimes(3);
});

it("evicts old files to bound persistent entry count across cache instances", async () => {
  const first = setup({ entries: 2, bytes: 1024 });
  expect((await first.cache.get("one.jpg")).status).toBe(200);
  const second = setup({ entries: 2, bytes: 1024 });
  const results = await Promise.all([first.cache.get("two.jpg"), second.cache.get("three.jpg")]);
  expect(results.map(result => result.status)).toEqual([200, 200]);
  const filenames = posterFiles();
  expect(filenames).toHaveLength(2);
  for (const file of filenames) expect(statSync(join(cachePath, file)).mode & 0o777).toBe(0o600);
});

it("enforces the byte quota even with spare entry capacity", async () => {
  const { cache, fetchPoster } = setup({ entries: 8, bytes: 130 });
  fetchPoster.mockResolvedValue({ type: "image/jpeg", bytes: Buffer.concat([firstPicture.bytes, Buffer.alloc(80)]) });
  expect((await cache.get("one.jpg")).status).toBe(200);
  expect((await cache.get("two.jpg")).status).toBe(200);
  expect(posterFiles()).toEqual(["two.jpg"]);
});

it("restores the cache quota during startup reconciliation", async () => {
  const initial = setup();
  expect((await initial.cache.get("old.jpg")).status).toBe(200);
  setClock("2025-10-01T01:02:03.004Z");
  expect((await initial.cache.get("new.jpg")).status).toBe(200);
  const restarted = setup({ entries: 3, bytes: 30 });
  await restarted.cache.sweep();
  expect(posterFiles()).toEqual(["new.jpg"]);
  expect(restarted.fetchPoster).not.toHaveBeenCalled();
});

it("does not let an active refresh batch postpone unvisited expiry cleanup", async () => {
  const { cache, fetchPoster } = setup();
  expect((await cache.get("old.jpg")).status).toBe(200);
  setClock("2025-10-03T01:02:03.004Z");
  expect((await cache.get(filename)).status).toBe(200);
  let finish!: (value: TmdbPosterPicture) => void;
  fetchPoster.mockClear().mockImplementation(file => file === "old.jpg"
    ? Promise.reject(new Error("upstream outage"))
    : new Promise(resolve => { finish = resolve; }));
  setClock("2026-03-30T01:02:03.003Z");
  const first = cache.sweep();
  await vi.waitFor(() => expect(fetchPoster).toHaveBeenCalledTimes(2));
  setClock("2026-03-30T01:02:03.004Z");
  const next = cache.sweep();
  await vi.waitFor(() => expect(posterFiles()).toEqual([filename]));
  finish(newerPicture);
  await Promise.all([first, next]);
});

it("advances past the first hundred due records when upstream remains unavailable", async () => {
  const { cache, fetchPoster } = setup();
  const sample = "poster-000.jpg";
  expect((await cache.get(sample)).status).toBe(200);
  for (let index = 1; index <= 100; index++) {
    const seeded = `poster-${String(index).padStart(3, "0")}.jpg`;
    copyFileSync(join(cachePath, sample), join(cachePath, seeded));
  }
  setClock("2026-02-28T01:02:03.004Z");
  fetchPoster.mockClear().mockRejectedValue(new Error("upstream outage"));
  await cache.sweep();
  expect(fetchPoster).toHaveBeenCalledTimes(100);
  expect(fetchPoster).not.toHaveBeenCalledWith("poster-100.jpg", expect.anything());
  fetchPoster.mockClear();
  setClock("2026-02-28T01:03:03.004Z");
  await cache.sweep();
  expect(fetchPoster).toHaveBeenCalledTimes(100);
  expect(fetchPoster).toHaveBeenNthCalledWith(1, "poster-100.jpg", expect.any(AbortSignal));
});

it("reconciles expired bytes with an abandoned lock and partial write on disk", async () => {
  const { cache } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  const lock = join(cachePath, lockFilename);
  writeFileSync(lock, "", { mode: 0o600 });
  writeFileSync(join(cachePath, ".tmdb-write"), "partial", { mode: 0o600 });
  const inode = statSync(lock).ino;
  setClock("2026-03-30T01:02:03.004Z");

  await setup().cache.reconcile();

  expect(posterFiles()).toEqual([]);
  expect(statSync(lock).ino).toBe(inode);
});

it("waits for a live competing writer and reconciles after its Node process is killed", async () => {
  const { cache } = setup();
  expect((await cache.get(filename)).status).toBe(200);
  const lock = join(cachePath, lockFilename);
  const lockModule = new URL("./tmdb-poster-lock.ts", import.meta.url).href;
  const holder = spawn(process.execPath, ["--disable-warning=MODULE_TYPELESS_PACKAGE_JSON", "--input-type=module", "--eval", `
    import { writeSync } from "node:fs";
    import { registerHooks } from "node:module";
    registerHooks({
      resolve(name, context, next) {
        return name === "server-only"
          ? { url: "data:text/javascript,", shortCircuit: true }
          : next(name, context);
      },
    });
    const { withTmdbPosterLock } = await import(process.argv[1]);
    await withTmdbPosterLock(process.argv[2], AbortSignal.timeout(5000), () => {
      writeSync(1, "locked");
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0);
    });
  `, lockModule, lock], { stdio: ["ignore", "pipe", "inherit"] });
  const exited = once(holder, "close");
  let inode: number;
  try {
    const ready = await Promise.race([once(holder.stdout, "data"), exited]);
    expect(String(ready[0])).toBe("locked");
    inode = statSync(lock).ino;
    setClock("2026-03-30T01:02:03.004Z");

    const waiter = new AbortController();
    const mutation = vi.fn();
    const cancelled = withTmdbPosterLock(lock, waiter.signal, mutation);
    waiter.abort();
    await expect(cancelled).rejects.toMatchObject({ name: "AbortError" });
    expect(mutation).not.toHaveBeenCalled();
    await expect(cache.reconcile()).rejects.toThrow("Could not acquire the TMDB poster cache lock.");
    expect(posterFiles()).toEqual([filename]);
    expect(statSync(lock).ino).toBe(inode);
  } finally {
    holder.kill("SIGKILL");
    expect(await exited).toEqual([null, "SIGKILL"]);
  }

  await setup().cache.reconcile();
  expect(posterFiles()).toEqual([]);
  expect(statSync(lock).ino).toBe(inode);
});

it("fails safely when the private cache directory is missing or symlinked", async () => {
  const { cache, fetchPoster } = setup();
  const target = join(directory, "redirected");
  renameSync(cachePath, target);
  expect((await cache.get(filename)).status).toBe(503);
  symlinkSync(target, cachePath, "dir");
  expect((await cache.get(filename)).status).toBe(503);
  expect(readdirSync(target)).toEqual([]);
  expect(fetchPoster).not.toHaveBeenCalled();
});

it("never reads poster symlinks and leaves their targets intact", async () => {
  const { cache, fetchPoster } = setup();
  const target = join(directory, "unrelated.jpg");
  writeFileSync(target, "private");
  symlinkSync(target, join(cachePath, filename));
  expect(await payload(await cache.get(filename))).toEqual(firstPicture.bytes);
  expect(readFileSync(target, "utf8")).toBe("private");
  expect(fetchPoster).toHaveBeenCalledTimes(1);
});

it("coalesces same-source requests while allowing one caller to cancel", async () => {
  const { cache, fetchPoster } = setup();
  let finish!: (value: TmdbPosterPicture) => void;
  fetchPoster.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const cancelled = new AbortController();
  const one = cache.get(filename, cancelled.signal);
  const two = cache.get(filename);
  await vi.waitFor(() => expect(fetchPoster).toHaveBeenCalledOnce());
  cancelled.abort();
  expect((await one).status).toBe(504);
  finish(firstPicture);
  expect(await payload(await two)).toEqual(firstPicture.bytes);
});

it("limits distinct active and queued operations and releases them on cancellation", async () => {
  const { cache, fetchPoster } = setup();
  fetchPoster.mockImplementation((_filename, signal) => new Promise((_resolve, reject) => {
    signal.addEventListener("abort", () => reject(signal.reason), { once: true });
  }));
  const callers = Array.from({ length: 72 }, () => new AbortController());
  const requests = callers.map((caller, index) => cache.get(`poster-${index}.jpg`, caller.signal));
  try {
    await vi.waitFor(() => expect(fetchPoster).toHaveBeenCalledTimes(8));
    expect((await cache.get("excess.jpg")).status).toBe(503);
  } finally {
    for (const caller of [...callers.slice(8), ...callers.slice(0, 8)]) caller.abort();
    await Promise.all(requests);
  }
  expect(fetchPoster).toHaveBeenCalledTimes(8);
  fetchPoster.mockResolvedValue(firstPicture);
  expect((await cache.get("next.jpg")).status).toBe(200);
});
