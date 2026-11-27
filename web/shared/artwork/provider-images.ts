import type { NextConfig } from "next";
import { tmdbPosterFilename } from "./tmdb-poster-paths";
import { igdbCoverFilename } from "./igdb-cover-paths";

// This policy is shared by the optimizer endpoint and the artwork renderer.
export const providerImageConfig: NonNullable<NextConfig["images"]> = {
  remotePatterns: [],
  localPatterns: [],
  minimumCacheTTL: 86400,
  maximumDiskCacheSize: 1073741824,
  maximumResponseBody: 5 * 1024 * 1024,
  maximumRedirects: 0,
  dangerouslyAllowLocalIP: false,
  dangerouslyAllowSVG: false,
  deviceSizes: [320, 480, 640, 960],
  imageSizes: [64, 96, 128, 192, 256],
  qualities: [75],
  formats: ["image/webp"],
};

export function isProviderArtwork(reference: string): boolean {
  return tmdbPosterFilename(reference) !== null
    || igdbCoverFilename(reference) !== null
    || /^https:\/\/covers\.openlibrary\.org\/b\/id\/[1-9][0-9]{0,9}-L\.jpg\?default=false$/.test(reference);
}
