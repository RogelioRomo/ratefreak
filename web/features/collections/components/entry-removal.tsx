"use client";
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { deleteItem } from '../api/contents';
import { useEditorReload } from '../hooks/use-editor-reload';
import type { EntrySnapshot } from '../interfaces/snapshots';
import type { EntryRemovalProps } from '../interfaces/contents-props';
import { ContentsError } from './contents-error';
import styles from './collections.module.css';
export function EntryRemoval({snapshot,reload,active,actions,refresh,onCancel,onSaved}:EntryRemovalProps){
 const reader=useEditorReload(active,`${snapshot.details.collection.id}:${snapshot.item.id}`);
 const [consent,setConsent]=useState<{snapshot:EntrySnapshot;observed:number}|null>(null);
 if(consent&&(!active||consent.observed!==snapshot.details.collection.revision))setConsent(null);
 const disabled=!active||reader.pending||actions.pending;
 return <section className={styles.form} aria-label="Remove entry"><p>Removing an entry permanently removes its stored field values. The catalog title and your personal activity remain.</p>
  {!consent?<Button type="button" variant="secondary" disabled={disabled||actions.needsRefresh} onClick={async()=>{actions.clearError();const latest=await reader.run(reload);if(latest)setConsent({snapshot:latest,observed:snapshot.details.collection.revision});}}>Review entry removal</Button>:<><p>Permanently remove {consent.snapshot.item.title}?</p><Button type="button" disabled={disabled||actions.needsRefresh} onClick={async()=>{if(!active||consent.observed!==snapshot.details.collection.revision)return;const target=consent.snapshot;setConsent(null);const result=await actions.execute(signal=>deleteItem(target.details.collection.id,target.item.id,target.details.collection.revision,signal),refresh);if(result)onSaved();}}>Confirm remove entry</Button></>}
  <ContentsError error={reader.error??actions.error}/>{actions.outcomeUnknown&&<p role="alert">The removal outcome is unknown. Check current entries before another action.</p>}
  <Button type="button" variant="secondary" disabled={actions.pending} onClick={()=>{setConsent(null);onCancel();}}>Cancel removal</Button>
 </section>;
}
