"use client";
import { useState } from "react";
import Image from "next/image";
import { IconBook, IconDeviceGamepad2, IconDisc, IconMovie, IconDeviceTv, IconPhoto } from "@tabler/icons-react";
import { isProviderArtwork } from "@/shared/artwork/provider-images";
import { tmdbPosterFilename, tmdbPosterRoute } from "@/shared/artwork/tmdb-poster-paths";
import { igdbCoverFilename, igdbCoverRoute } from "@/shared/artwork/igdb-cover-paths";
import type { MediaArtworkProps } from "../interfaces/component-props";
import styles from "./media-artwork.module.css";

export function supportedArtwork(reference: string | null): string | null {
  if (!reference) return null;
  // Only the canonical HTTPS formats emitted by the inspected provider adapters.
  if (/^\/api\/v1\/media\/[0-9a-f-]{36}\/artwork\/[0-9a-f-]{36}$/i.test(reference)) return reference;
  if (isProviderArtwork(reference)) return reference;
  return null;
}
export function MediaArtwork({ reference, title, type, sizes = "(max-width: 448px) calc(100vw - 3rem), (max-width: 768px) calc((100vw - 4rem) / 2), (max-width: 1024px) 30vw, 280px" }: MediaArtworkProps) {
  const [failedReference, setFailedReference] = useState<string | null>(null);
  const src = supportedArtwork(reference);
  const tmdbFilename = src && tmdbPosterFilename(src);
  const igdbFilename = src && igdbCoverFilename(src);
  const Icon = type === "custom" ? IconPhoto : type === "book" ? IconBook : type === "album" ? IconDisc : type === "video_game" ? IconDeviceGamepad2 : type === "tv_show" ? IconDeviceTv : IconMovie;
  return <div className={styles.artwork}>
    {src && failedReference !== src
      ? isProviderArtwork(src)
        ? <Image src={tmdbFilename ? tmdbPosterRoute + tmdbFilename : igdbFilename ? igdbCoverRoute + igdbFilename : src} unoptimized alt={`Artwork for ${title}`} fill sizes={sizes} loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailedReference(src)} />
        // eslint-disable-next-line @next/next/no-img-element -- protected uploads must retain authorization and no-store; never enter the public optimizer.
        : <img src={src} alt={`Artwork for ${title}`} loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailedReference(src)} />
      : <div className={styles.fallback}><Icon size={36} stroke={1.25} aria-hidden="true" /><span>Artwork unavailable</span></div>}
  </div>;
}
