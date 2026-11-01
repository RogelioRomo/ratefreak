"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/ui/loading-state";
import type { MediaActivityProps, RetainedActivityProps } from "../interfaces/component-props";
import { useTracking, useRating, useOwnReview } from "../hooks/use-personal";
import { TrackingForm } from "./tracking-form";
import { RatingForm } from "./rating-form";
import { ReviewForm } from "./review-form";
import { PersonalReadError } from "./personal-error";
import styles from "./personal.module.css";
export function PersonalControls({ mediaId }: MediaActivityProps) {
  const session = useSession();
  const [rememberedAccount, setRememberedAccount] = useState<string | null>(session.account?.id ?? null);
  const accountId = session.account === undefined ? rememberedAccount : session.account?.id ?? null;
  // Keep drafts concealed during a check; a confirmed identity change replaces the keyed subtree.
  if (session.account !== undefined && accountId !== rememberedAccount) setRememberedAccount(accountId);
  const active = !!session.account && !session.error && !session.isTransitioning && !session.isLoading;
  return <>
    {session.error ? <section className={styles.section}><h2>Your activity</h2><p role="alert">Your session could not be checked.</p><Button variant="secondary" onClick={() => void session.refresh()}>Retry session</Button></section>
      : !active && session.account !== null ? <LoadingState label="Checking your session…" />
      : session.account === null ? <section className={styles.section}><h2>Make room for your take.</h2><p>Track this title, give it a score, or write a public review.</p><Link className={styles.link} href={`/login?${new URLSearchParams({ returnTo: `/catalog/${mediaId}` })}`}>Log in to track, rate or review</Link></section> : null}
    {accountId && <div hidden={!active} inert={!active}><fieldset className={styles.retained} disabled={!active}>
      <PrivateForms key={`${accountId}:${mediaId}`} accountId={accountId} mediaId={mediaId} active={active} />
    </fieldset></div>}
  </>;
}
function PrivateForms({ accountId, mediaId, active }: RetainedActivityProps) {
  const tracking = useTracking(accountId, mediaId, active), rating = useRating(accountId, mediaId, active), review = useOwnReview(accountId, mediaId, active);
  const session = useSession();
  const [expired, setExpired] = useState(false);
  const unauthorized = [tracking.error, rating.error, review.error].some(error => error instanceof ApiError && error.status === 401);
  const { refresh } = session;
  useEffect(() => {
    let active = true;
    if (unauthorized) void refresh().catch(() => {}).then(() => { if (active) setExpired(true); });
    return () => { active = false; };
  }, [unauthorized, refresh]);
  if (unauthorized || expired) return <section className={styles.section}><h2>Your activity</h2><p role="alert">Your session expired.</p><Link className={styles.link} href={`/login?${new URLSearchParams({ returnTo: `/catalog/${mediaId}` })}`}>Log in to continue</Link></section>;
  return <div className={styles.personal}>
    <section className={styles.section} aria-labelledby="tracking-heading"><h2 id="tracking-heading">Your library</h2>
      {tracking.isPending ? <LoadingState label="Loading tracking…" /> : tracking.data === undefined ? <PersonalReadError error={tracking.error!} retry={() => void tracking.refetch()} /> : <><TrackingForm accountId={accountId} mediaId={mediaId} saved={tracking.data} />{tracking.error && <PersonalReadError error={tracking.error} retry={() => void tracking.refetch()} />}</>}
    </section>
    <section className={styles.section} aria-labelledby="rating-heading"><h2 id="rating-heading">Your rating</h2>
      {rating.isPending ? <LoadingState label="Loading your rating…" /> : rating.data === undefined ? <PersonalReadError error={rating.error!} retry={() => void rating.refetch()} /> : <><RatingForm accountId={accountId} mediaId={mediaId} saved={rating.data} />{rating.error && <PersonalReadError error={rating.error} retry={() => void rating.refetch()} />}</>}
    </section>
    <section className={styles.section} aria-labelledby="review-heading"><h2 id="review-heading">Your review</h2>
      {review.isPending ? <LoadingState label="Loading your review…" /> : review.data === undefined ? <PersonalReadError error={review.error!} retry={() => void review.refetch()} /> : <><ReviewForm accountId={accountId} mediaId={mediaId} saved={review.data} />{review.error && <PersonalReadError error={review.error} retry={() => void review.refetch()} />}</>}
    </section>
  </div>;
}
