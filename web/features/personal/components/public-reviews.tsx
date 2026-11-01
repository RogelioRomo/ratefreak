"use client";
import Link from "next/link";
import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import type { MediaActivityProps, ReviewEntryProps } from "../interfaces/component-props";
import { reviewPage } from "../query/filters";
import { useReviews } from "../hooks/use-personal";
import styles from "./personal.module.css";
function ReviewEntry({ review }: ReviewEntryProps) {
  const [revealed, setRevealed] = useState(false);
  return <article className={styles.review}><header><h3>{review.author.displayName}</h3><p>@{review.author.handle} · <time dateTime={review.createdAt}>{review.createdAt.slice(0, 10)}</time></p></header>
    {review.isSpoiler && <Button variant="secondary" aria-expanded={revealed} onClick={() => setRevealed(!revealed)}>{revealed ? "Hide spoilers" : "Show spoilers"}</Button>}
    {(!review.isSpoiler || revealed) && <p className={styles.body}>{review.body}</p>}
  </article>;
}
export function PublicReviews({ mediaId }: MediaActivityProps) {
  const search = useSearchParams(); const page = reviewPage(new URLSearchParams(search.toString()));
  const reviews = useReviews(mediaId, page);
  const href = (next: number) => `/catalog/${mediaId}${next > 1 ? `?reviewsPage=${next}` : ""}`;
  return <section className={styles.section} aria-labelledby="community-reviews"><h2 id="community-reviews">Community reviews</h2>
    {page === null ? <><div role="alert">Invalid review page.</div><Link className={styles.link} href={`/catalog/${mediaId}`}>Reset review page</Link></> : reviews.isPending ? <LoadingState label="Loading reviews…" /> : reviews.isError ? <ErrorState title="Reviews unavailable." description="Please wait if requests are limited, then try again." onRetry={() => void reviews.refetch()} /> : <>
      <p className={styles.hint}>{reviews.data.totalCount} {reviews.data.totalCount === 1 ? "review" : "reviews"}</p>
      {!reviews.data.items.length ? <div className={styles.empty}><p>{page > 1 ? "No reviews on this page." : "No reviews yet. Yours could be the first."}</p>{page > 1 && <Link className={styles.link} href={href(1)}>First review page</Link>}</div> : <ul className={styles.reviewList}>{reviews.data.items.map(review => <li key={review.id}><ReviewEntry review={review} /></li>)}</ul>}
      <nav className={styles.pagination} aria-label="Review pages">{page > 1 ? <Link href={href(page - 1)}>Previous reviews</Link> : <span>Previous reviews</span>}<span>Page {page}</span>{page < Math.ceil(reviews.data.totalCount / 20) && page < 10000 ? <Link href={href(page + 1)}>Next reviews</Link> : <span>Next reviews</span>}</nav>
    </>}
  </section>;
}
