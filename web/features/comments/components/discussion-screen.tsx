"use client";
import { ApiError } from "@/shared/api/api-error";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import { isMediaId } from "@/features/catalog/query/filters";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useDiscussion } from "../hooks/use-discussion";
import { commentPage, discussionHref } from "../query/paging";
import type { DiscussionProps } from "../interfaces/comments";
import { ReviewContext } from "./review-context";
import { CommentList } from "./comment-list";
import { CommentForm } from "./comment-form";
import styles from "./comments.module.css";
export function DiscussionScreen({ reviewId }: DiscussionProps) {
 const params = useSearchParams(), session = useSession(), client = useQueryClient();
 const valid = isMediaId(reviewId), page = commentPage(new URLSearchParams(params.toString())); const review = useDiscussion(reviewId, valid);
 const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
 const missing = review.error instanceof ApiError && review.error.status === 404;
 const available = !missing && !!session.account && !session.error && !session.isTransitioning && session.account.id === cached?.id;
 if (!valid) return <p>Invalid review address.</p>;
 if (review.isPending) return <LoadingState label="Loading review…" />;
 if (review.isError && !review.data) return <ErrorState title="Review unavailable" description="This review may have been removed, or requests may be limited. Wait and try again." onRetry={() => void review.refetch()} />;
 return <div className={styles.screen}><h1>Review discussion.</h1>{review.isError && <ErrorState title="Review unavailable" description={missing ? "This review is no longer available." : "We couldn’t refresh the review. Your draft is kept."} onRetry={() => void review.refetch()} />}<div hidden={missing} inert={missing}><ReviewContext key={review.data.id} review={review.data} /><h2>Comments</h2>
  {page === null ? <><p role="alert">Invalid comment page.</p><Link href={discussionHref(reviewId)}>Reset comment page</Link></> : <>
   <CommentList reviewId={reviewId} page={page} enabled={!missing} />
   {cached && <CommentForm key={`${cached.id}:${reviewId}`} reviewId={reviewId} accountId={cached.id} available={available} />}
   {session.account === null && <p><Link href={`/login?returnTo=${encodeURIComponent(discussionHref(reviewId))}`}>Log in to comment</Link></p>}
  </>}
 </div></div>;
}
