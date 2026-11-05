"use client";
import Link from "next/link";
import { ReportControl } from "@/features/reporting/components/report-control";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import { Button } from "@/components/ui/button";
import { removeCachedComment } from "../query/cache";
import { deleteComment } from "../api/comments";
import { useCommentWrite } from "../hooks/use-comment-write";
import { commentKeys } from "../hooks/use-discussion";
import type { CommentCardProps, OwnerCommentProps } from "../interfaces/comments";
import { CommentForm } from "./comment-form";
import { CommentError } from "./comment-error";
import styles from "./comments.module.css";
export function CommentCard({ comment, enabled = true, onDeleted }: CommentCardProps) {
 const [revealed, setRevealed] = useState(false); const session = useSession(), client = useQueryClient();
 const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
 const owned = cached?.id.toLowerCase() === comment.author.id.toLowerCase();
 const available = enabled && owned && session.account?.id === cached?.id && !session.error && !session.isTransitioning;
 return <article className={styles.comment}>
  <header><h3><Link href={`/users/${comment.author.handle}`}>{comment.author.displayName}</Link></h3><p className={styles.meta}>@{comment.author.handle} · <time dateTime={comment.createdAt}>{comment.createdAt.slice(0, 10)}</time>{comment.updatedAt !== comment.createdAt && " · Edited"}</p></header>
  {comment.isSpoiler && <Button variant="secondary" aria-expanded={revealed} onClick={() => setRevealed(!revealed)}>{revealed ? "Hide comment spoilers" : "Show comment spoilers"}</Button>}
  {(!comment.isSpoiler || revealed) && <p className={styles.body}>{comment.body}</p>}
  <ReportControl target={{type:"comment",id:comment.id,authorId:comment.author.id}} available={enabled}/>
  {owned && cached && <OwnerComment key={cached.id} comment={comment} accountId={cached.id} available={available} onDeleted={onDeleted} />}
 </article>;
}
function OwnerComment({ comment, accountId, available, onDeleted }: OwnerCommentProps) {
 const write = useCommentWrite(), client = useQueryClient();
 const [message, setMessage] = useState("");
 const [editing, setEditing] = useState(false), [confirm, setConfirm] = useState(false), [deleted, setDeleted] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState<Error | null>(null);
 if (!available && confirm) setConfirm(false);
 if (deleted) return <p role="status">Comment deleted.</p>;
 if (editing) return <CommentForm reviewId={comment.reviewId} accountId={accountId} comment={comment} available={available} onCancel={() => setEditing(false)} onSaved={() => { setEditing(false); setMessage("Comment saved."); }} />;
 return <div hidden={!available} inert={!available}>
  <p role="status">{message}</p><CommentError error={error} />
  {uncertain && <p role="alert">We couldn’t confirm deletion. Reload the discussion before trying again. <Button variant="secondary" onClick={() => window.location.reload()}>Reload discussion</Button></p>}
  <div className={styles.actions}><Button variant="secondary" disabled={write.pending || uncertain || !available} onClick={() => setEditing(true)}>Edit comment</Button><Button variant="secondary" disabled={write.pending || uncertain || !available} onClick={() => setConfirm(true)}>Delete comment</Button></div>
  {confirm && <div className={styles.confirm}><p>Delete your comment permanently?</p><div className={styles.actions}><Button disabled={!available || write.pending || uncertain} onClick={async () => {
   setError(null); const result = await write.run(signal => deleteComment(comment.id, signal));
   if (result.kind === "saved") { setConfirm(false); setDeleted(true); onDeleted?.(); removeCachedComment(client, comment.reviewId, comment.id); void client.invalidateQueries({ queryKey: commentKeys.review(comment.reviewId) }); }
   else if (result.kind === "rejected") { setError(result.error); setConfirm(false); }
   else { setUncertain(true); setConfirm(false); }
  }}>Confirm delete</Button><Button variant="secondary" disabled={write.pending} onClick={() => setConfirm(false)}>Keep comment</Button></div></div>}
 </div>;
}
