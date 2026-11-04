"use client";
import { ApiError } from "@/shared/api/api-error";
import { useState } from "react";
import Link from "next/link";
import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useComments } from "../hooks/use-discussion";
import type { CommentListProps } from "../interfaces/comments";
import { discussionHref } from "../query/paging";
import { CommentCard } from "./comment-card";
import styles from "./comments.module.css";
export function CommentList({ reviewId, page, enabled = true }: CommentListProps) {
 const [message, setMessage] = useState("");
 const comments = useComments(reviewId, page);
 if (comments.isPending) return <LoadingState label="Loading comments…" />;
 if (comments.isError && !comments.data) return <ErrorState title="Comments unavailable" description="This review may be unavailable, or requests may be limited. Wait and try again." onRetry={() => void comments.refetch()} />;
 const missing = comments.error instanceof ApiError && comments.error.status === 404;
 return <>
  <p role="status">{message}</p>
  {comments.isError && <ErrorState title="Comments unavailable" description={missing ? "This review is no longer available." : "We couldn’t refresh comments. Showing the previous list; your draft is kept."} onRetry={() => void comments.refetch()} />}
  <div hidden={missing} inert={missing}>
  <p className={styles.hint}>Comments: {comments.data.totalCount} · Oldest first</p>
  {comments.data.items.length ? <ul className={styles.list}>{comments.data.items.map(comment => <li key={comment.id}><CommentCard comment={comment} enabled={enabled && !missing} onDeleted={() => setMessage("Comment deleted.")} /></li>)}</ul> : <p>{page === 1 ? "No comments yet." : "No comments on this page."}</p>}
  <nav className={styles.paging} aria-label="Comment pages">{page > 1 && <Link href={discussionHref(reviewId, page - 1)}>Previous comments</Link>}{page * 20 < comments.data.totalCount && page < 10000 && <Link href={discussionHref(reviewId, page + 1)}>Next comments</Link>}{page > 1 && !comments.data.items.length && <Link href={discussionHref(reviewId)}>First comment page</Link>}</nav>
 </div></>;
}
