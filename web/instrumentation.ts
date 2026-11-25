export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { startTmdbPosterScheduler } = await import("./server/artwork/tmdb-posters");
    await startTmdbPosterScheduler();
  }
}
