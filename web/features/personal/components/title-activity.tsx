"use client";
import { Suspense } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import type { MediaActivityProps } from "../interfaces/component-props";
import { useAggregate } from "../hooks/use-personal";
import { PersonalControls } from "./personal-controls";
import { PublicReviews } from "./public-reviews";
import styles from "./personal.module.css";
export function TitleActivity({ mediaId }: MediaActivityProps) {
  const aggregate = useAggregate(mediaId);
  return <div className={styles.activity}><section className={styles.section}><h2>Community rating</h2>
    {aggregate.isPending ? <LoadingState label="Loading community rating…" /> : aggregate.isError ? <ErrorState title="Community rating unavailable." onRetry={() => void aggregate.refetch()} /> : <p>{aggregate.data.count === 0 || aggregate.data.average === null ? "No ratings yet." : `${aggregate.data.average.toFixed(2)} / 5 · ${aggregate.data.count} ${aggregate.data.count === 1 ? "rating" : "ratings"}`}</p>}
    </section><PersonalControls mediaId={mediaId} /><Suspense fallback={<LoadingState label="Loading reviews…" />}><PublicReviews mediaId={mediaId} /></Suspense></div>;
}
