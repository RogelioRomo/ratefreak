"use client";
import { useState } from "react";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import { CatalogPicker } from "./catalog-picker";
import { ContentsError } from "./contents-error";
import { createItem, updateItem } from "../api/contents";
import { getCollection } from "../api/collections";
import type { CatalogSelection, MembershipEditorProps } from "../interfaces/contents-props";
import { positionValue } from "../query/input-common";
import styles from "./collections.module.css";
export function ListMembershipEditor({ details, item, active, actions, refresh, onCancel, onSaved }: MembershipEditorProps) {
 const [snapshot, setSnapshot] = useState(details); const [selection, setSelection] = useState<CatalogSelection | null>(item ? { id: item.mediaItemId, title: item.media.title } : null); const [position, setPosition] = useState(String(item?.position ?? 0)); const [error, setError] = useState("");
 const blocked = !active || actions.pending || actions.needsRefresh || actions.outcomeUnknown || actions.error instanceof ApiError && actions.error.status === 409;
 return <form className={styles.form} aria-label={item ? "Reorder item" : "Add item"} onSubmit={async e => {
  e.preventDefault(); if (blocked) return; const value = positionValue(position); if (!selection || value === null) { setError("Choose an item and enter a nonnegative whole-number position."); return; } setError("");
  const result = await actions.execute(signal => item ? updateItem(snapshot.collection.id, item.id, { expectedRevision: snapshot.collection.revision, position: value }, signal) : createItem(snapshot.collection.id, { expectedRevision: snapshot.collection.revision, mediaItemId: selection.id, position: value }, signal), refresh); if (result) onSaved();
 }}>
  {!item && <CatalogPicker value={selection} onChange={setSelection} disabled={blocked} />}
  <div className={styles.field}><label htmlFor="membership-position">Position</label><input id="membership-position" inputMode="numeric" value={position} onChange={e => setPosition(e.target.value)} disabled={blocked} /></div>
  <p className={styles.hint}>Lower positions appear first. This changes the list, not the catalog item or its ratings.</p>
  {error && <p role="alert">{error}</p>}<ContentsError error={actions.error} />
  {(actions.outcomeUnknown || actions.error instanceof ApiError && actions.error.status === 409) && <Button type="button" variant="secondary" disabled={!active || actions.pending} onClick={async () => { try { setSnapshot(await getCollection(details.collection.id)); actions.clearError(); } catch { setError("The latest list could not be loaded. Keep this draft and try again."); } }}>Reload latest list</Button>}
  <div className={styles.actions}><Button type="submit" disabled={blocked}>{item ? "Save position" : "Add to list"}</Button><Button type="button" variant="secondary" disabled={actions.pending} onClick={onCancel}>Cancel</Button></div>
 </form>;
}
