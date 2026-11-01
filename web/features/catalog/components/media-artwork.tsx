"use client";
import { useState } from "react";
import { IconDeviceGamepad2, IconDisc, IconMovie, IconDeviceTv } from "@tabler/icons-react";
import type { MediaArtworkProps } from "../interfaces/component-props";
import styles from "./media-artwork.module.css";

export function supportedArtwork(reference: string | null): string | null {
  if (!reference) return null;
  // Only the canonical HTTPS formats emitted by the inspected provider adapters.
  if (/^https:\/\/image\.tmdb\.org\/t\/p\/w500\/[A-Za-z0-9_-]+\.[A-Za-z0-9]+$/.test(reference) || /^https:\/\/images\.igdb\.com\/igdb\/image\/upload\/t_cover_big\/[A-Za-z0-9_-]+\.jpg$/.test(reference)) return reference;
  return null;
}
export function MediaArtwork({ reference, title, type }: MediaArtworkProps) {
  const [failedReference, setFailedReference] = useState<string | null>(null);
  const src = supportedArtwork(reference);
  const Icon = type === "album" ? IconDisc : type === "video_game" ? IconDeviceGamepad2 : type === "tv_show" ? IconDeviceTv : IconMovie;
  return <div className={styles.artwork}>
    {src && failedReference !== src
      // eslint-disable-next-line @next/next/no-img-element -- provider allowlist, no generic image proxy or optimization required.
      ? <img src={src} alt={`Artwork for ${title}`} loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailedReference(src)} />
      : <div className={styles.fallback}><Icon size={36} stroke={1.25} aria-hidden="true" /><span>Artwork unavailable</span></div>}
  </div>;
}
