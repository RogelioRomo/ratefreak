"use client";
import { StarRating } from "./star-rating";
import { useState } from "react";
import { IconStar } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import type { RatingFormProps } from "../interfaces/component-props";
import { saveRating, deletePersonal } from "../api/personal";
import { personalKeys } from "../hooks/use-personal";
import { usePersonalWrite } from "../hooks/use-personal-write";
import { PersonalError } from "./personal-error";
import styles from "./personal.module.css";
export function RatingForm({ accountId, mediaId, saved, canShareRating = false }: RatingFormProps) {
  const [shared, setShared] = useState(saved?.isShared ?? false);
  const [score, setScore] = useState(saved ? String(saved.score) : "");
  const [present, setPresent] = useState(!!saved), [message, setMessage] = useState("");
  const write = usePersonalWrite(); const keys = [personalKeys.rating(accountId, mediaId), personalKeys.aggregate(mediaId)];
  return <form aria-label="Rating" className={styles.form} onSubmit={event => {
    event.preventDefault(); if (score === "") return; setMessage("");
    void write.run(signal => saveRating(mediaId, +score, signal, shared), keys).then(outcome => { if (outcome) { setPresent(true); setMessage("Rating saved."); } });
  }}><PersonalError error={write.error} /><fieldset disabled={write.pending}>
    <StarRating value={score === "" ? null : +score} onChange={value => setScore(String(value))} disabled={write.pending} />
    <label className={styles.sharing}><input type="checkbox" checked={shared} disabled={!canShareRating && !shared} onChange={event => setShared(event.target.checked)} />Share this rating</label>
    <p className={styles.hint}>{canShareRating ? "Sharing makes this score public under your name in instance feeds. Leave unchecked to keep it private." : "Only ratings for publicly available items can be shared. You can still keep a private rating or unshare an existing one."}</p>
    <div className={styles.actions}><Button type="submit"><IconStar size={17} aria-hidden="true" />{write.pending ? "Saving…" : "Save rating"}</Button>{present && <Button variant="secondary" onClick={() => {
      setMessage(""); void write.run(signal => deletePersonal(`media/${mediaId}/rating`, signal), keys).then(outcome => { if (outcome) { setScore(""); setPresent(false); setMessage("Rating removed."); } });
    }}>Remove rating</Button>}</div>
  </fieldset>{message && <p role="status">{message}</p>}</form>;
}
