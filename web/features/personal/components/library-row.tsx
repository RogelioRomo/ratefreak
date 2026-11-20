"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Select } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaLabels } from "@/features/catalog/query/filters";
import { ApiError } from "@/shared/api/api-error";
import { useSession } from "@/features/auth/hooks/use-session";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import { getTracking, saveLibraryStatus } from "../api/personal";
import { personalKeys } from "../hooks/use-personal";
import { usePersonalWrite } from "../hooks/use-personal-write";
import { trackingStatuses } from "../query/filters";
import type { LibraryRowProps } from "../interfaces/component-props";
import type { TrackingStatus } from "../interfaces/personal";
import { PersonalError } from "./personal-error";
import styles from "./personal.module.css";

export function LibraryRow({ item, accountId, onSaved, onSaving }: LibraryRowProps) {
  const [editor, setEditor] = useState({ status: item.status, expectedStatus: item.status, observed: item.status });
  // Keep dirty status drafts through date-only/background updates. A changed server
  // status remains the expected-status conflict's responsibility until refreshed.
  if (editor.observed !== item.status) {
    const clean = editor.status === editor.expectedStatus;
    setEditor({ status: clean ? item.status : editor.status, expectedStatus: clean ? item.status : editor.expectedStatus, observed: item.status });
  }
  const status = editor.status;
  const session = useSession();
  const mounted = useRef(true);
  const [refreshing, setRefreshing] = useState(false);
  const [recoveryError, setRecoveryError] = useState("");
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const form = useRef<HTMLFormElement>(null);
  const write = usePersonalWrite();
  const conflict = write.error instanceof ApiError && write.error.status === 409;
  async function refresh() {
    if (refreshing) return;
    setRefreshing(true); setRecoveryError("");
    try {
      await session.refresh();
      if (!mounted.current || write.client.getQueryState(sessionQueryKey)?.status !== "success" ||
          write.client.getQueryData<AccountProfile | null>(sessionQueryKey)?.id !== accountId) return;
      const [current] = await Promise.all([
        write.client.fetchQuery({ queryKey: personalKeys.tracking(accountId, item.media.id), queryFn: ({ signal }) => getTracking(item.media.id, signal), staleTime: 0 }),
        write.client.invalidateQueries({ queryKey: personalKeys.library(accountId) }),
      ]);
      if (!mounted.current) return;
      if (current) setEditor({ status: current.status, expectedStatus: current.status, observed: current.status });
      write.clearError();
    } catch {
      if (mounted.current) setRecoveryError("We couldn’t refresh your entry. Try again.");
    } finally {
      if (mounted.current) setRefreshing(false);
    }
  }
  return <li>
    <div className={styles.thumbnail}><MediaArtwork reference={item.media.imageReference} title={item.media.title} type={item.media.type} sizes="6rem" /></div>
    <div className={styles.libraryInfo}>
      <div className={styles.libraryDetails}>
        <Link href={`/catalog/${item.media.id}`}>{item.media.title}</Link>
        <p>{mediaLabels[item.media.type]} · {trackingStatuses[item.status]}</p>
        <p className={styles.ownRating}>{item.ownRating == null ? "Not rated" : `Your rating: ${item.ownRating.toFixed(1)} / 5`}</p>
        {item.startedAt && <p>Started <time dateTime={item.startedAt}>{item.startedAt}</time></p>}
        {item.completedAt && <p>Completed <time dateTime={item.completedAt}>{item.completedAt}</time></p>}
      </div>
      <div className={styles.libraryTracking}>
        <form ref={form} className={styles.statusForm} aria-label={`Update status for ${item.media.title}`} onSubmit={event => {
          event.preventDefault();
          if (conflict || status === editor.expectedStatus || refreshing) return;
          onSaving();
          void write.run(signal => saveLibraryStatus(item.media.id, status, editor.expectedStatus, accountId, signal)).then(outcome => {
            if (outcome) {
              setEditor(previous => ({ ...previous, status: outcome.value.status, expectedStatus: outcome.value.status }));
              onSaved(outcome.value, !!form.current?.contains(document.activeElement) || document.activeElement === document.body);
            }
          });
        }}>
          <fieldset disabled={write.pending}>
            <div><label htmlFor={`status-${item.media.id}`}>Tracking status</label>
              <Select id={`status-${item.media.id}`} aria-label={`Status for ${item.media.title}`} value={status} onChange={event => setEditor({ ...editor, status: event.target.value as TrackingStatus })} disabled={write.pending}>
                {Object.entries(trackingStatuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </Select>
            </div>
            <Button type="submit" disabled={write.pending || refreshing || conflict || status === editor.expectedStatus} aria-label={`Save status for ${item.media.title}`}>{write.pending ? "Saving…" : "Save status"}</Button>
          </fieldset>
          {conflict ? <div><p role="alert">This entry or account changed. Refresh the library before trying again.</p><Button variant="secondary" disabled={refreshing} onClick={() => void refresh()}>{refreshing ? "Refreshing…" : "Refresh library"}</Button>{recoveryError && <p role="alert">{recoveryError}</p>}</div> : <PersonalError error={write.error} />}
        </form>
        <p className={styles.hint}>Status changes keep your dates. Open the title to edit dates or your rating.</p>
      </div>
    </div>
  </li>;
}
