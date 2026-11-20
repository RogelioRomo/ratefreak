"use client";
import Link from "next/link";
import { useRef, useState } from "react";
import { Select } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaLabels } from "@/features/catalog/query/filters";
import { ApiError } from "@/shared/api/api-error";
import { saveLibraryStatus } from "../api/personal";
import { personalKeys } from "../hooks/use-personal";
import { usePersonalWrite } from "../hooks/use-personal-write";
import { trackingStatuses } from "../query/filters";
import type { LibraryRowProps } from "../interfaces/component-props";
import type { TrackingStatus } from "../interfaces/personal";
import { PersonalError } from "./personal-error";
import styles from "./personal.module.css";

export function LibraryRow({ item, accountId, onSaved }: LibraryRowProps) {
  const [status, setStatus] = useState<TrackingStatus>(item.status);
  const form = useRef<HTMLFormElement>(null);
  const write = usePersonalWrite();
  const conflict = write.error instanceof ApiError && write.error.status === 409;
  async function refresh() {
    await Promise.all([
      write.client.invalidateQueries({ queryKey: personalKeys.library(accountId) }),
      write.client.invalidateQueries({ queryKey: personalKeys.tracking(accountId, item.media.id) }),
    ]);
    write.clearError();
    setStatus(item.status);
  }
  return <li>
    <div className={styles.thumbnail}><MediaArtwork reference={item.media.imageReference} title={item.media.title} type={item.media.type} sizes="6rem" /></div>
    <div className={styles.libraryInfo}>
      <Link href={`/catalog/${item.media.id}`}>{item.media.title}</Link>
      <p>{mediaLabels[item.media.type]} · {trackingStatuses[item.status]}</p>
      <p className={styles.ownRating}>{item.ownRating == null ? "Not rated" : `Your rating: ${item.ownRating.toFixed(1)} / 5`}</p>
      {item.startedAt && <p>Started <time dateTime={item.startedAt}>{item.startedAt}</time></p>}
      {item.completedAt && <p>Completed <time dateTime={item.completedAt}>{item.completedAt}</time></p>}
      <form ref={form} className={styles.statusForm} aria-label={`Update status for ${item.media.title}`} onSubmit={event => {
        event.preventDefault();
        if (conflict || status === item.status) return;
        void write.run(signal => saveLibraryStatus(item.media.id, status, item.status, accountId, signal)).then(outcome => {
          if (outcome) onSaved(outcome.value, !!form.current?.contains(document.activeElement) || document.activeElement === document.body);
        });
      }}>
        <fieldset disabled={write.pending}>
          <div><label htmlFor={`status-${item.media.id}`}>Tracking status</label>
            <Select id={`status-${item.media.id}`} aria-label={`Status for ${item.media.title}`} value={status} onChange={event => setStatus(event.target.value as TrackingStatus)} disabled={write.pending}>
              {Object.entries(trackingStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </Select>
          </div>
          <Button type="submit" disabled={write.pending || conflict || status === item.status} aria-label={`Save status for ${item.media.title}`}>{write.pending ? "Saving…" : "Save status"}</Button>
        </fieldset>
        {conflict ? <div><p role="alert">This entry or account changed. Refresh the library before trying again.</p><Button variant="secondary" onClick={() => void refresh()}>Refresh library</Button></div> : <PersonalError error={write.error} />}
      </form>
      <p className={styles.hint}>Status changes keep your dates. Open the title to edit dates or your rating.</p>
    </div>
  </li>;
}
