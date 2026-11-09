"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { deleteItem } from "../api/contents";
import type { ListRemovalProps } from "../interfaces/contents-props";
import { ContentsError } from "./contents-error";
import styles from "./collections.module.css";
export function ListRemoval({ details, item, active, actions, refresh, onCancel, onSaved }: ListRemovalProps) {
 const [confirmed, setConfirmed] = useState(false);
 return <section className={styles.form} aria-label="Confirm removal"><h2>Remove {item.media.title} from this list?</h2><p>The catalog item, ratings and reviews will remain.</p><label className={styles.check}><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} disabled={!active || actions.pending} />Remove this membership only</label><ContentsError error={actions.error} /><div className={styles.actions}><Button type="button" disabled={!active || actions.pending || actions.needsRefresh || actions.outcomeUnknown || !!actions.error || !confirmed} onClick={async () => { const result = await actions.execute(signal => deleteItem(details.collection.id, item.id, details.collection.revision, signal), refresh); if (result) onSaved(); }}>Remove from list</Button><Button type="button" variant="secondary" disabled={actions.pending} onClick={onCancel}>Cancel</Button></div></section>;
}
