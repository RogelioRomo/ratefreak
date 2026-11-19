"use client";
import { CatalogReportControls } from "@/features/custom-catalog/components/catalog-report-controls";
import { useCatalogViewer } from "@/features/custom-catalog/hooks/use-catalog-viewer";
import { TitleActivity } from "@/features/personal/components/title-activity";
import Link from "next/link";
import { IconArrowLeft } from "@tabler/icons-react";
import { ApiError } from "@/shared/api/api-error";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import type { MediaDetailScreenProps } from "../interfaces/component-props";
import { useMedia } from "../hooks/use-media";
import { isMediaId, mediaLabels } from "../query/filters";
import { CatalogCredits } from "@/features/credits/components/catalog-credits";
import { MediaArtwork } from "./media-artwork";
import { BookMetadata } from "./book-metadata";
import styles from "./media-detail-screen.module.css";

export function MediaDetailScreen({ id }: MediaDetailScreenProps) {
  const media = useMedia(id);
  const { viewer } = useCatalogViewer();
  const missing = !isMediaId(id) || (media.error instanceof ApiError && media.error.status === 404);
  const limited = media.error instanceof ApiError && media.error.status === 429;
  return <div className={styles.screen}>
    <Link className={styles.back} href="/catalog"><IconArrowLeft size={18} aria-hidden="true" />Back to catalog</Link>
    {missing ? <section className={styles.missing}><h1>Title not found.</h1><p>This title may have been removed, or the link is incorrect. Browse the catalog to find another.</p></section>
      : media.isPending ? <LoadingState label="Loading title…" />
      : media.isError ? <ErrorState title={limited ? "Too many requests." : "Title unavailable."} description={limited ? "Please wait a moment before trying again." : "We couldn’t load this title. Please try again."} onRetry={() => void media.refetch()} />
      : <><article className={styles.detail}>
        <div className={styles.artwork}><MediaArtwork reference={media.data.imageReference} title={media.data.title} type={media.data.type} sizes="(max-width: 368px) calc(100vw - 3rem), 320px" /></div>
        <div className={styles.content}>
          <p className={styles.type}>{media.data.category?.name ?? mediaLabels[media.data.type]}</p>
          <h1>{media.data.title}</h1>
          {media.data.creatorUserId === viewer && <Link href={`/catalog/${id}/edit`}>Manage item</Link>}
          {media.data.type === "book" ? <BookMetadata metadata={media.data.metadata} externalId={media.data.externalIds?.find(identity => identity.provider === "openlibrary" && identity.namespace === "work")?.externalId} />
            : media.data.type !== "custom" && <p className={styles.date}>{media.data.releaseDate ? <time dateTime={media.data.releaseDate}>{new Date(`${media.data.releaseDate}T00:00:00Z`).toLocaleDateString("en", { year: "numeric", month: "long", day: "numeric", timeZone: "UTC" })}</time> : "Release date unavailable"}</p>}
          <section className={styles.description} aria-labelledby="about-title"><h2>About this title</h2><p>{media.data.description?.trim() || "No description available yet."}</p></section>
          <CatalogCredits type={media.data.type} providers={media.data.externalIds?.map(identity => identity.provider) ?? []} />
        </div>
      </article><CatalogReportControls item={media.data} /><TitleActivity mediaId={id.toLowerCase()} canShareRating={media.data.visibility === "public" && media.data.category?.scope === "public" && media.data.category.isHidden === false && media.data.isHidden === false && media.data.isArchived === false} /></>}
  </div>;
}
