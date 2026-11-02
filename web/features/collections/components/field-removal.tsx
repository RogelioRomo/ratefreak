"use client";
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/shared/api/api-error';
import { deleteField,getDeletionImpact } from '../api/contents';
import { useEditorReload } from '../hooks/use-editor-reload';
import type { DeletionImpact } from '../interfaces/contents';
import type { FieldRemovalProps } from '../interfaces/contents-props';
import { ContentsError } from './contents-error';
import styles from './collections.module.css';
export function FieldRemoval({collectionId,field,revision,active,actions,refresh,onCancel,onSaved}:FieldRemovalProps){
 const reader=useEditorReload(active,`${collectionId}:${field.id}`);
 const [consent,setConsent]=useState<{impact:DeletionImpact;observed:number}|null>(null),[changed,setChanged]=useState(false);
 if(consent&&(!active||consent.observed!==revision))setConsent(null);
 const disabled=!active||reader.pending||actions.pending;
 return <section className={styles.form} aria-label="Remove field"><h3>Remove field {field.label}</h3><p>Removing this field permanently deletes its stored values. This cannot be undone.</p>
  {!consent?<><Button type="button" variant="secondary" disabled={disabled||actions.needsRefresh} onClick={async()=>{actions.clearError();const impact=await reader.run(signal=>getDeletionImpact(collectionId,field.id,signal));if(impact&&impact.fieldId===field.id&&impact.key===field.key){setConsent({impact,observed:revision});setChanged(false);}}}>Preview field removal</Button>{changed&&<p role="alert">The collection changed. Load a new preview and confirm again.</p>}</>:<><p>{field.label} ({consent.impact.key}) has populated values in {consent.impact.populatedItemCount} entries.</p><Button type="button" disabled={disabled||actions.needsRefresh} onClick={async()=>{
   if(!active||consent.observed!==revision)return;const preview=consent.impact;setConsent(null);
   const result=await actions.execute(async signal=>{try{await deleteField(collectionId,field.id,preview.revision,signal);}catch(error){if(!signal.aborted&&error instanceof ApiError&&error.status===409)setChanged(true);throw error;}},refresh);if(result)onSaved();
  }}>Confirm remove field</Button></>}
  <ContentsError error={reader.error??actions.error}/>{actions.outcomeUnknown&&<p role="alert">The removal outcome is unknown. Check current fields before another action.</p>}
  <Button type="button" variant="secondary" disabled={actions.pending} onClick={()=>{setConsent(null);onCancel();}}>Cancel removal</Button>
 </section>;
}
