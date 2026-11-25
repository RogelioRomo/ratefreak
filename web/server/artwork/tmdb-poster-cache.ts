import "server-only";
import { isTmdbPosterFilename } from "@/shared/artwork/tmdb-poster-paths";
import { CoverFetchGate } from "./cover-fetch-gate";
import { withAbort } from "./public-artwork-request";
import { readTmdbPolicy, type TmdbPolicy } from "./tmdb-policy";
import { fetchTmdbPoster, type TmdbPosterPicture } from "./tmdb-poster-source";
import { posterAge, TmdbPosterStore, type StoredPosterSchedule } from "./tmdb-poster-store";

type Problem = 404 | 502 | 503 | 504;
type PosterResult = { status: Problem } | { status: 200; body: Blob; fetchedAt: number; expiresAt: number };
type Flight = { controller: AbortController; result: Promise<PosterResult>; callers: number };
type CacheOptions = {
  directory: string | undefined;
  policyPath: string | undefined;
  now?: () => number;
  fetchPoster?: (filename: string, signal: AbortSignal) => Promise<TmdbPosterPicture | null>;
  capacity?: { entries: number; bytes: number };
};

const requestMs = 10_000;
const refreshLimit = 100;
const noStoreHeaders = { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" };

export function tmdbPosterProblem(status: Problem): Response {
  const headers = status === 503 ? { ...noStoreHeaders, "Retry-After": "1" } : noStoreHeaders;
  return new Response(null, { status, headers });
}

function policyStatus(policy: TmdbPolicy | null, filename: string): 200 | 404 | 503 {
  if (!policy) return 503;
  return policy.terminated || policy.blockedPosters.has(filename) ? 404 : 200;
}

function pictureResult(picture: TmdbPosterPicture, fetchedAt: number, expiresAt: number): PosterResult {
  // A coalesced miss shares one immutable body across callers.
  return { status: 200, fetchedAt, expiresAt, body: new Blob([picture.bytes], { type: picture.type }) };
}

export class TmdbPosterCache {
  private readonly store: TmdbPosterStore;
  private readonly now: () => number;
  private readonly fetchPoster: NonNullable<CacheOptions["fetchPoster"]>;
  private readonly gate = new CoverFetchGate();
  private readonly flights = new Map<string, Flight>();
  private readonly attempts = new Map<string, number>();
  private refreshing?: Promise<void>;

  constructor(private readonly options: CacheOptions) {
    this.now = options.now ?? Date.now;
    this.store = new TmdbPosterStore(options.directory, this.now, options.capacity);
    this.fetchPoster = options.fetchPoster ?? fetchTmdbPoster;
  }

  async get(filename: string, caller = new AbortController().signal): Promise<Response> {
    if (!isTmdbPosterFilename(filename)) return tmdbPosterProblem(404);
    const result = await this.shared(filename, false, caller);
    if (result.status !== 200) return tmdbPosterProblem(result.status);
    if (caller.aborted) return tmdbPosterProblem(504);
    const policy = this.allowed(filename);
    if (policy !== 200) return tmdbPosterProblem(policy);
    const now = this.now();
    if (result.fetchedAt > now || result.expiresAt <= now) return tmdbPosterProblem(404);
    return new Response(result.body, { headers: { ...noStoreHeaders, "Content-Type": result.body.type } });
  }

  async reconcile(): Promise<StoredPosterSchedule[]> {
    const now = this.now();
    const policy = readTmdbPolicy(this.options.policyPath);
    const entries = await this.store.reconcile(filename => policyStatus(policy, filename) === 404, AbortSignal.timeout(requestMs));
    const retained = new Set(entries.map(entry => entry.filename));
    for (const attempted of this.attempts.keys()) {
      if (!retained.has(attempted)) this.attempts.delete(attempted);
    }
    if (!policy || policy.terminated) return [];
    return entries.filter(entry => entry.refreshAt <= now);
  }

  async sweep(): Promise<void> {
    await this.refreshDue(await this.reconcile());
  }

  refreshDue(due: StoredPosterSchedule[]): Promise<void> {
    if (!this.refreshing) this.refreshing = this.refreshBatch(due).finally(() => { this.refreshing = undefined; });
    return this.refreshing;
  }

  private async refreshBatch(due: StoredPosterSchedule[]) {
    const attemptTime = (filename: string) => this.attempts.get(filename) ?? -1;
    due.sort((a, b) => attemptTime(a.filename) - attemptTime(b.filename)
      || a.expiresAt - b.expiresAt || a.filename.localeCompare(b.filename));
    const batch = due.slice(0, refreshLimit);
    for (let start = 0; start < batch.length; start += 8) {
      await Promise.all(batch.slice(start, start + 8).map(async ({ filename }) => {
        this.attempts.set(filename, this.now());
        await this.shared(filename, true, new AbortController().signal);
      }));
    }
  }

  private allowed(filename: string): 200 | 404 | 503 {
    return policyStatus(readTmdbPolicy(this.options.policyPath), filename);
  }

  private async shared(filename: string, refresh: boolean, caller: AbortSignal): Promise<PosterResult> {
    if (caller.aborted) return { status: 504 };
    let flight = this.flights.get(filename);
    if (!flight) {
      const controller = new AbortController();
      const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(requestMs)]);
      const reservation = this.gate.acquire(signal);
      if (!reservation) return { status: 503 };
      const result = this.run(filename, refresh, reservation, signal).finally(() => {
        if (this.flights.get(filename)?.controller === controller) this.flights.delete(filename);
      });
      flight = { controller, result, callers: 0 };
      this.flights.set(filename, flight);
    }
    flight.callers++;
    try {
      return await withAbort(flight.result, caller);
    } catch {
      return { status: 504 };
    } finally {
      if (--flight.callers === 0 && this.flights.get(filename) === flight) {
        this.flights.delete(filename);
        flight.controller.abort();
      }
    }
  }

  private async run(filename: string, refresh: boolean, reservation: Promise<() => void>, signal: AbortSignal): Promise<PosterResult> {
    let release: (() => void) | undefined;
    try {
      release = await reservation;
      signal.throwIfAborted();
      const status = this.allowed(filename);
      if (status !== 200) return { status };
      const now = this.now();
      const { poster, found } = this.store.read(filename, now);
      if (poster && (!refresh || now < poster.refreshAt)) return pictureResult(poster, poster.fetchedAt, poster.expiresAt);
      if (found && !poster) await this.store.removeIf(filename, now, current => !current, signal);
      // Scheduled work only refreshes retained files. Eviction and expiry may
      // have removed a candidate since the sweep selected it.
      if (refresh && !poster) return { status: 404 };

      const beforeFetch = this.allowed(filename);
      if (beforeFetch !== 200) return { status: beforeFetch };
      let picture: TmdbPosterPicture | null;
      try {
        picture = await withAbort(this.fetchPoster(filename, signal), signal);
      } catch {
        if (poster && poster.expiresAt > this.now()) return pictureResult(poster, poster.fetchedAt, poster.expiresAt);
        return { status: signal.aborted ? 504 : 502 };
      }
      const fetchedAt = this.now();
      const afterFetch = this.allowed(filename);
      if (afterFetch !== 200) return { status: afterFetch };
      if (!picture) {
        if (poster) await this.store.removeIf(filename, fetchedAt, current => !current || current.fetchedAt <= poster.fetchedAt, signal);
        return { status: 404 };
      }
      if (!await this.store.write(filename, fetchedAt, picture, () => this.allowed(filename) === 200, signal)) {
        const status = this.allowed(filename);
        return { status: status === 200 ? 503 : status };
      }
      return pictureResult(picture, fetchedAt, posterAge(fetchedAt).expiresAt);
    } catch {
      return { status: signal.aborted ? 504 : 503 };
    } finally {
      release?.();
    }
  }
}
