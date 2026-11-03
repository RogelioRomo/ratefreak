"use client";
import { Select } from "@/components/ui/select";
import { useState } from "react";
import { IconBookmark } from "@tabler/icons-react";
import { Button } from "@/components/ui/button";
import type { TrackingFormProps } from "../interfaces/component-props";
import type { TrackingStatus } from "../interfaces/personal";
import { trackingStatuses } from "../query/filters";
import { saveTracking, deletePersonal } from "../api/personal";
import { personalKeys } from "../hooks/use-personal";
import { usePersonalWrite } from "../hooks/use-personal-write";
import { PersonalError } from "./personal-error";
import styles from "./personal.module.css";
export function TrackingForm({ accountId, mediaId, saved }: TrackingFormProps) {
  const [present, setPresent] = useState(!!saved);
  const [status, setStatus] = useState<TrackingStatus>(saved?.status ?? "planned");
  const [started, setStarted] = useState(saved?.startedAt ?? ""), [completed, setCompleted] = useState(saved?.completedAt ?? "");
  const [confirm, setConfirm] = useState(false), [message, setMessage] = useState(""), [dateError, setDateError] = useState("");
  const write = usePersonalWrite();
  const keys = [personalKeys.tracking(accountId, mediaId), personalKeys.library(accountId)];
  async function remove() {
    const outcome = await write.run(signal => deletePersonal(`users/me/library/${mediaId}`, signal), keys);
    if (outcome) { setPresent(false); setConfirm(false); setStatus("planned"); setStarted(""); setCompleted(""); setMessage("Removed from your library."); }
  }
  return <form aria-label="Tracking" className={styles.form} onSubmit={event => {
    event.preventDefault(); setMessage(""); setDateError("");
    if (started && completed && completed < started) { setDateError("Completion must not precede start."); return; }
    void write.run(signal => saveTracking(mediaId, { status, startedAt: started || null, completedAt: completed || null }, signal), keys).then(outcome => { if (outcome) { setPresent(true); setMessage("Tracking saved."); } });
  }}>
    <PersonalError error={write.error} />{dateError && <p role="alert">{dateError}</p>}
    <fieldset disabled={write.pending}><div className={styles.fields}>
      <div><label htmlFor="tracking-status">Tracking status</label><Select disabled={write.pending} id="tracking-status" value={status} onChange={e => setStatus(e.target.value as TrackingStatus)}>{Object.entries(trackingStatuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</Select></div>
      <div><label htmlFor="tracking-start">Started on</label><input id="tracking-start" type="date" value={started} onChange={e => setStarted(e.target.value)} /></div>
      <div><label htmlFor="tracking-complete">Completed on</label><input id="tracking-complete" type="date" value={completed} onChange={e => setCompleted(e.target.value)} aria-invalid={!!dateError} /></div>
    </div><p className={styles.hint}>Dates are optional. Changing status keeps your dates.</p>
      <div className={styles.actions}><Button type="submit"><IconBookmark size={17} aria-hidden="true" />{write.pending ? "Saving…" : present ? "Save tracking" : "Add to library"}</Button>{present && !confirm && <Button variant="secondary" onClick={() => setConfirm(true)}>Remove from library</Button>}</div>
      {confirm && <div className={styles.confirm}><p>Your rating and review will stay.</p><Button variant="secondary" onClick={() => void remove()}>Confirm removal</Button><Button variant="secondary" onClick={() => setConfirm(false)}>Cancel</Button></div>}
    </fieldset>{message && <p role="status">{message}</p>}
  </form>;
}
