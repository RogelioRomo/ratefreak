"use client";
import { useId, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { fieldErrors } from "@/features/auth/components/form-error";
import { updateCachedComment } from "../query/cache";
import { createComment, updateComment } from "../api/comments";
import { useCommentWrite } from "../hooks/use-comment-write";
import { commentKeys } from "../hooks/use-discussion";
import type { CommentFormProps } from "../interfaces/comments";
import { CommentError } from "./comment-error";
import styles from "./comments.module.css";
export function CommentForm({ reviewId, available, comment, onSaved, onCancel }: CommentFormProps) {
 const id = useId(), write = useCommentWrite(), client = useQueryClient();
 const [body, setBody] = useState(comment?.body ?? ""), [spoiler, setSpoiler] = useState(comment?.isSpoiler ?? false);
 const [error, setError] = useState<Error | null>(null), [uncertain, setUncertain] = useState(false), [message, setMessage] = useState("");
 const errors = fieldErrors(error, "body");
 return <form className={styles.form} aria-label={comment ? "Edit comment" : "Add comment"} hidden={!available} inert={!available} onSubmit={async event => {
  event.preventDefault(); if (!available || write.pending || uncertain) return;
  setError(null); setMessage(""); const input = { body: body.trim(), isSpoiler: spoiler };
  const result = await write.run(signal => comment ? updateComment(comment.id, input, signal) : createComment(reviewId, input, signal));
  if (result.kind === "saved") {
   if (comment) updateCachedComment(client, result.value);
   if (!comment) { setBody(""); setSpoiler(false); }
   setMessage(comment ? "Comment saved." : "Comment posted. It may appear on a later page.");
   onSaved?.(); void client.invalidateQueries({ queryKey: commentKeys.review(reviewId) });
  } else if (result.kind === "rejected") setError(result.error);
  else setUncertain(true);
 }}>
  <p>Comments follow the item’s visibility. Use plain text and mark spoilers.</p>
  <CommentError error={error} />
  {uncertain && <p role="alert">We couldn’t confirm the save. Check the discussion before posting again. Reloading discards this draft. <Button variant="secondary" onClick={() => window.location.reload()}>Reload discussion</Button></p>}
  <fieldset disabled={!available || write.pending || uncertain}>
   <label htmlFor={id}>Comment</label><textarea id={id} value={body} onChange={event => setBody(event.target.value)} required maxLength={2000} rows={5} aria-invalid={errors?.length ? true : undefined} aria-describedby={`${id}-hint${errors?.length ? ` ${id}-error` : ""}`} />
   <p id={`${id}-hint`} className={styles.hint}>1–2000 characters.</p>{errors?.length && <p id={`${id}-error`} role="alert">{errors.join(" ")}</p>}
   <label className={styles.checkbox}><input type="checkbox" checked={spoiler} onChange={event => setSpoiler(event.target.checked)} />Contains spoilers</label>
   <div className={styles.actions}><Button type="submit">{write.pending ? "Saving…" : comment ? "Save comment" : "Post comment"}</Button>{onCancel && <Button variant="secondary" onClick={onCancel}>Cancel editing</Button>}</div>
  </fieldset><p role="status">{message}</p>
 </form>;
}
