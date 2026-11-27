export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { disableZstdDecompression } = await import("./server/zstd-policy");
    disableZstdDecompression();
    // The bundled libheif has unresolved decoder advisories. Block the loader
    // before requests or background work can parse HEIF/AVIF, regardless of MIME.
    // Next initializes its own loader allowlist on first use. Initialize that
    // exact runtime module first, or its lazy setup would undo this block.
    const { createRequire } = await import("node:module");
    const runtimeRequire = createRequire(`${process.cwd()}/package.json`);
    const { getSharp } = runtimeRequire("next/dist/server/image-optimizer/transform") as typeof import("next/dist/server/image-optimizer/transform");
    const sharp = getSharp(undefined, undefined);
    sharp.block({ operation: ["VipsForeignLoadHeif"] });
    const { startTmdbPosterScheduler } = await import("./server/artwork/tmdb-posters");
    await startTmdbPosterScheduler();
    const { startIgdbCoverScheduler } = await import("./server/artwork/igdb-covers");
    await startIgdbCoverScheduler();
  }
}
