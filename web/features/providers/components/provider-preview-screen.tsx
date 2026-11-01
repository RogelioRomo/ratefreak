"use client";
import Link from "next/link";
import { IconArrowLeft } from "@tabler/icons-react";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaLabels } from "@/features/catalog/query/filters";
import type { ProviderPreviewProps } from "../interfaces/component-props";
import { providerIdentity, providerNames } from "../query/identity";
import { useProviders, useExternalMedia } from "../hooks/use-providers";
import { ProviderAttribution } from "./provider-attribution";
import { ProviderError } from "./provider-error";
import { ImportControl } from "./import-control";
import styles from "./providers.module.css";
export function ProviderPreviewScreen({ provider, type, externalId }: ProviderPreviewProps) {
  const identity = providerIdentity(provider, type, externalId);
  const capabilities = useProviders();
  const cap = capabilities.data?.find(x => x.name === identity?.provider);
  const media = useExternalMedia(identity, !!cap?.enabled && !!identity && cap.supportedTypes.includes(identity.type));
  return <div className={styles.screen}>
    <Link className={styles.back} href="/catalog/discover"><IconArrowLeft size={18} aria-hidden="true" />Back to discovery</Link>
    {identity && (capabilities.isPending || capabilities.isError || (cap?.enabled && (media.isPending || media.isError))) && <h1 className={styles.stateTitle}>Provider preview.</h1>}
    {!identity ? <><h1 className={styles.stateTitle}>Preview unavailable.</h1><ErrorState title="Invalid provider title." description="Return to discovery to find a valid title." /></> : capabilities.isPending ? <LoadingState label="Loading provider…" /> : capabilities.isError ? <ProviderError error={capabilities.error} onRetry={() => void capabilities.refetch()} /> : !cap?.enabled || !cap.supportedTypes.includes(identity.type) ? <><h1 className={styles.stateTitle}>Provider unavailable.</h1><p>This provider is not currently enabled. Browse the local catalog or try another provider.</p><Link className={styles.link} href="/catalog">Browse the catalog</Link></> : <>
      {media.isPending ? <LoadingState label="Loading preview…" /> : media.isError ? <ProviderError error={media.error} onRetry={() => void media.refetch()} /> : <article className={styles.detail}>
        <div className={styles.artwork}><MediaArtwork reference={media.data.imageReference} title={media.data.title} type={media.data.type} /></div>
        <div className={styles.content}><h1>{media.data.title}</h1><p className={styles.meta}>{mediaLabels[media.data.type]} · {providerNames[identity.provider]} preview</p><p className={styles.meta}>{media.data.releaseDate ? <time dateTime={media.data.releaseDate}>{media.data.releaseDate}</time> : "Release date unavailable"}</p>
          <section className={styles.description} aria-labelledby="preview-about"><h2>About this title</h2><p>{media.data.description?.trim() || "No description available yet."}</p></section>
          <ImportControl identity={identity} />
        </div>
      </article>}
      <ProviderAttribution capability={cap} />
    </>}
  </div>;
}
