export const tmdbPosterOrigin = "https://image.tmdb.org/t/p/w500/";
export const tmdbPosterRoute = "/tmdb-posters/";

export function isTmdbPosterFilename(filename: unknown): filename is string {
  return typeof filename === "string" && filename.length <= 200
    && /^[A-Za-z0-9_-]+\.(?:jpg|jpeg|png|webp|avif)(?![\s\S])/.test(filename);
}

export function tmdbPosterFilename(reference: string): string | null {
  const filename = reference.startsWith(tmdbPosterOrigin) ? reference.slice(tmdbPosterOrigin.length) : null;
  return isTmdbPosterFilename(filename) ? filename : null;
}
