import "server-only";
import { TmdbPosterCache } from "./tmdb-poster-cache";

const sweepIntervalMs = 60_000;
type PosterRuntime = {
  directory: string | undefined;
  policyPath: string | undefined;
  cache: TmdbPosterCache;
  started?: Promise<void>;
};

// The app route and instrumentation can be compiled into separate server bundles.
const globalRuntime = globalThis as typeof globalThis & { ratefreakTmdbPosters?: PosterRuntime };

function posterRuntime(): PosterRuntime {
  const directory = process.env.RATEFREAK_TMDB_CACHE_PATH;
  const policyPath = process.env.RATEFREAK_TMDB_POLICY_PATH;
  const current = globalRuntime.ratefreakTmdbPosters;
  if (current && current.directory === directory && current.policyPath === policyPath) return current;
  const runtime = { directory, policyPath, cache: new TmdbPosterCache({ directory, policyPath }) };
  globalRuntime.ratefreakTmdbPosters = runtime;
  return runtime;
}

export function getTmdbPosterCache(): TmdbPosterCache {
  return posterRuntime().cache;
}

export async function startTmdbPosterScheduler(): Promise<void> {
  const runtime = posterRuntime();
  if (!runtime.directory) return;
  runtime.started ??= (async () => {
    const sweepFailed = () => console.warn("TMDB poster cache reconciliation failed. Check cache directory permissions and offline maintenance state.");
    try {
      // Complete local expiry and policy cleanup before accepting requests.
      // Network work may continue in the background.
      const due = await runtime.cache.reconcile();
      void runtime.cache.refreshDue(due).catch(sweepFailed);
    } catch {
      sweepFailed();
    }
    setInterval(() => {
      // Each tick reconciles on disk even if a previous refresh batch is active.
      void runtime.cache.sweep().catch(sweepFailed);
    }, sweepIntervalMs).unref();
  })();
  await runtime.started;
}
