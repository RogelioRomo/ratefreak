"use client";
import { useState } from "react";
import { IconPencil } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/shared/api/api-error";
import type { ReviewFormProps } from "../interfaces/component-props";
import type { ReviewDetails } from "../interfaces/personal";
import { saveReview, getOwnReview, deletePersonal } from "../api/personal";
import { personalKeys } from "../hooks/use-personal";
import { usePersonalWrite } from "../hooks/use-personal-write";
import { PersonalError } from "./personal-error";
import styles from "./personal.module.css";
export function ReviewForm({ accountId, mediaId, saved }: ReviewFormProps) {
  const [record, setRecord] = useState(saved);
  const [body, setBody] = useState(saved?.body ?? ""), [spoiler, setSpoiler] = useState(saved?.isSpoiler ?? false);
  const [conflict, setConflict] = useState(false), [existing, setExisting] = useState<ReviewDetails | null>(null);
  const [bodyError, setBodyError] = useState("");
  const [confirm, setConfirm] = useState(false), [message, setMessage] = useState("");
  const write = usePersonalWrite(); const keys = [personalKeys.review(accountId, mediaId), personalKeys.reviews(mediaId), ["review-discussion"], ["comments"]];
  return <form aria-label="Review" className={styles.form} onSubmit={event => {
    event.preventDefault(); setMessage(""); setBodyError("");
    if (!body.trim()) { setBodyError("Write a review before saving."); return; }
    if (conflict) return;
    void write.run(async signal => {
      try { return await saveReview(mediaId, { body: body.trim(), isSpoiler: spoiler }, record?.id, signal); }
      catch (error) {
        if (error instanceof ApiError && error.status === 409) {
          const current = await getOwnReview(mediaId, signal); signal.throwIfAborted(); setExisting(current); setConflict(true);
        }
        throw error;
      }
    }, keys).then(outcome => { if (outcome) { setRecord(outcome.value); setMessage("Review saved."); } });
  }}><p className={styles.hint}>Reviews are public. Keep personal information out of your review.</p><PersonalError error={write.error} />{bodyError && <p role="alert">{bodyError}</p>}
    {conflict && <div role="status"><p>{existing ? "Load your saved review to edit it. Your draft is still below." : "An existing review is unavailable. Your draft is kept; it has not replaced that review."}</p>{existing && <Button variant="secondary" onClick={() => { setRecord(existing); setBody(existing.body); setSpoiler(existing.isSpoiler); setConflict(false); write.clearError(); setMessage("Saved review loaded."); }}>Load saved review</Button>}</div>}
    <fieldset disabled={write.pending}><label htmlFor="review-body">Your public review</label><textarea id="review-body" required maxLength={10000} rows={7} value={body} onChange={e => setBody(e.target.value)} />
      <label className={styles.checkbox}><input type="checkbox" checked={spoiler} onChange={e => setSpoiler(e.target.checked)} />Contains spoilers</label>
      <div className={styles.actions}><Button type="submit" disabled={conflict}><IconPencil size={17} aria-hidden="true" />{write.pending ? "Saving…" : record ? "Save review" : "Publish review"}</Button>{record && !confirm && <Button variant="secondary" onClick={() => setConfirm(true)}>Delete review</Button>}</div>
      {confirm && <div className={styles.confirm}><p>Delete your public review? Your library and rating will stay.</p><Button variant="secondary" onClick={() => {
        void write.run(signal => deletePersonal(`reviews/${record!.id}`, signal), keys).then(outcome => { if (outcome) { setRecord(null); setBody(""); setSpoiler(false); setConfirm(false); setMessage("Review deleted."); } });
      }}>Confirm deletion</Button><Button variant="secondary" onClick={() => setConfirm(false)}>Cancel</Button></div>}
    </fieldset>{message && <p role="status">{message}</p>}
  </form>;
}
