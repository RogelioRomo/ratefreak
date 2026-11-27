import "server-only";
import { IgdbCoverCache } from "./igdb-cover-cache";

const sweepIntervalMs = 60_000;
type PosterRuntime = {
  directory: string | undefined;
  policyPath: string | undefined;
  cache: IgdbCoverCache;
  started?: Promise<void>;
};

// The app route and instrumentation can be compiled into separate server bundles.
const globalRuntime = globalThis as typeof globalThis & { ratefreakIgdbCovers?: PosterRuntime };

function posterRuntime(): PosterRuntime {
  const directory = process.env.RATEFREAK_IGDB_CACHE_PATH;
  const policyPath = process.env.RATEFREAK_IGDB_POLICY_PATH;
  const current = globalRuntime.ratefreakIgdbCovers;
  if (current && current.directory === directory && current.policyPath === policyPath) return current;
  const runtime = { directory, policyPath, cache: new IgdbCoverCache({ directory, policyPath }) };
  globalRuntime.ratefreakIgdbCovers = runtime;
  return runtime;
}

export function getIgdbCoverCache(): IgdbCoverCache {
  return posterRuntime().cache;
}

export async function startIgdbCoverScheduler(): Promise<void> {
  const runtime = posterRuntime();
  if (!runtime.directory) return;
  runtime.started ??= (async () => {
    const sweepFailed = () => console.warn("IGDB poster cache reconciliation failed. Check cache directory permissions and offline maintenance state.");
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
