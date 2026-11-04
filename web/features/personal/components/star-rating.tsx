"use client";
import { useId, useState } from "react";
import { IconStarFilled } from "@tabler/icons-react";
import type { StarRatingProps } from "../interfaces/star-rating-props";
import styles from "./star-rating.module.css";
export function StarRating({ value, onChange, disabled = false }: StarRatingProps) {
 const name = useId(), [preview, setPreview] = useState<number | null>(null);
 const displayed = disabled ? value : preview ?? value;
 const label = (score: number) => `${score} ${score === 1 ? "star" : "stars"}`;
 return <fieldset className={styles.control} disabled={disabled} onPointerLeave={() => setPreview(null)} aria-describedby={`${name}-hint`}>
  <legend>Your score</legend>
  <label className={styles.zero} onPointerEnter={event => { if (!disabled && event.pointerType !== "touch") setPreview(0); }}>
   <input type="radio" name={name} value="0" checked={value === 0} onChange={() => { setPreview(null); onChange(0); }} aria-label="0 stars" required />0 stars
  </label>
  <div className={styles.stars} onPointerLeave={() => setPreview(null)}>
   {Array.from({ length: 5 }, (_, index) => {
    const fill = Math.max(0, Math.min(1, (displayed ?? 0) - index));
    return <span className={styles.star} key={index} data-rating-star>
     <IconStarFilled className={styles.empty} size={36} aria-hidden="true" />
     <span className={`${styles.fill} ${fill === 1 ? styles.full : fill === 0.5 ? styles.halfFilled : styles.unfilled}`}><IconStarFilled size={36} aria-hidden="true" /></span>
     {[0.5, 1].map(half => { const score = index + half; return <label className={`${styles.half} ${half === 1 ? styles.right : styles.left}`} key={half} title={label(score)} onPointerEnter={event => { if (!disabled && event.pointerType !== "touch") setPreview(score); }}>
      <input type="radio" name={name} value={score} aria-label={label(score)} checked={value === score} required onFocus={() => setPreview(null)} onChange={() => { setPreview(null); onChange(score); }} />
     </label>; })}
    </span>;
   })}
  </div>
  <p className={styles.value} aria-live="polite">{displayed === null ? "Not rated" : `${displayed.toFixed(1)} / 5`}</p>
  <p id={`${name}-hint`} className={styles.hint}>Choose a half or full star. Use arrow keys to adjust.</p>
 </fieldset>;
}
