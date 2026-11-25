import type { NextConfig } from "next";
import { tmdbPosterFilename } from "./tmdb-poster-paths";

// This policy is shared by the optimizer endpoint and the artwork renderer.
export const providerImageConfig: NonNullable<NextConfig["images"]> = {
  remotePatterns: [
    { protocol: "https", hostname: "images.igdb.com", port: "", pathname: "/igdb/image/upload/t_cover_big/*.jpg", search: "" },
  ],
  localPatterns: [{ pathname: "/provider-artwork/openlibrary/*", search: "" }],
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
    || /^https:\/\/images\.igdb\.com\/igdb\/image\/upload\/t_cover_big\/[A-Za-z0-9_-]+\.jpg$/.test(reference)
    || /^https:\/\/covers\.openlibrary\.org\/b\/id\/[1-9][0-9]{0,9}-L\.jpg\?default=false$/.test(reference);
}

export function optimizerArtworkSource(reference: string): string {
  const cover = /^https:\/\/covers\.openlibrary\.org\/b\/id\/([1-9][0-9]{0,9})-L\.jpg\?default=false$/.exec(reference);
  return cover ? `/provider-artwork/openlibrary/${cover[1]}` : reference;
}
