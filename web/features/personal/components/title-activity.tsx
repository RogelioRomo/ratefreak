"use client";
import { Suspense } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import type { MediaActivityProps } from "../interfaces/component-props";
import { useAggregate } from "../hooks/use-personal";
import { CommunityRating } from "./community-rating";
import { PersonalControls } from "./personal-controls";
import { PublicReviews } from "./public-reviews";
import styles from "./personal.module.css";
export function TitleActivity({ mediaId }: MediaActivityProps) {
  const aggregate = useAggregate(mediaId);
  return <div className={styles.activity}><div className={styles.activityGrid}><section className={styles.section}><h2>Community rating</h2>
    {aggregate.isPending ? <LoadingState label="Loading community rating…" /> : aggregate.isError ? <ErrorState title="Community rating unavailable." onRetry={() => void aggregate.refetch()} /> : <CommunityRating aggregate={aggregate.data} />}
    </section><PersonalControls mediaId={mediaId} /></div><Suspense fallback={<LoadingState label="Loading reviews…" />}><PublicReviews mediaId={mediaId} /></Suspense></div>;
}
