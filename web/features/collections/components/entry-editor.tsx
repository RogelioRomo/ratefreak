"use client";
import { Select } from "@/components/ui/select";
import { useId,useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/shared/api/api-error';
import { getCollection } from '../api/collections';
import { createItem,updateItem } from '../api/contents';
import { entryServerIssues } from "../query/server-issues";
import { entryData,entryDrafts } from '../query/entry-input';
import { positionValue,validText } from '../query/input-common';
import { useEditorReload } from '../hooks/use-editor-reload';
import type { CatalogSelection,EntryEditorProps } from '../interfaces/contents-props';
import type { FormIssues } from '../interfaces/form-input';
import { EntryValueControls } from './entry-value-controls';
import { CatalogPicker } from './catalog-picker';
import { ContentsError } from './contents-error';
import styles from './collections.module.css';
export function EntryEditor(props:EntryEditorProps){
 // Validation is deliberately before mounting editable state: unknown saved values must not be lost.
 try{entryDrafts(props.details.fields,props.item?.data);}catch(error){return <><ContentsError error={error instanceof Error?error:new Error('Unsafe saved entry.')}/><Button type="button" variant="secondary" onClick={props.onCancel}>Cancel entry</Button></>;}
 return <SafeEditor {...props}/>;
}
function SafeEditor({details,item,active,actions,refresh,reload,onCancel,onSaved,onUnavailable}:EntryEditorProps){
 const [snapshot,setSnapshot]=useState(details),[savedItem,setSavedItem]=useState(item);
 const [title,setTitle]=useState(item?.mediaItemId?'':item?.title??''),[kind,setKind]=useState(item?.mediaItemId?'catalog':'custom');
 const [selected,setSelected]=useState<CatalogSelection|null>(item?.mediaItemId?{id:item.mediaItemId,title:item.title}:null);
 const [values,setValues]=useState(()=>entryDrafts(details.fields,item?.data));const [position,setPosition]=useState(String(item?.position??0));
 const [issues,setIssues]=useState<FormIssues>({fields:{},summary:[]}),[conflict,setConflict]=useState(false),[missing,setMissing]=useState(false);
 const server=entryServerIssues(actions.error,snapshot.fields),errors={...server.fields,...issues.fields};
 const reader=useEditorReload(active,`${details.collection.id}:${item?.id??'new'}`),id=useId(),disabled=!active||actions.pending||reader.pending;
 return <form className={styles.form} aria-label={item?'Edit entry':'Add entry'} noValidate onSubmit={async event=>{
  event.preventDefault();if(disabled||conflict||missing||actions.outcomeUnknown)return;
  const input=entryData(values,snapshot.fields),next:FormIssues={fields:{},summary:[]};if(!input.ok){next.fields=input.issues.fields;next.summary=input.issues.summary;}
  const pos=positionValue(position);if(pos===null)next.fields.position=['Position must be an integer from 0 to 2147483647.'];
  if(kind==='custom'&&!validText(title.trim(),200))next.fields.title=['Entry title must contain 1–200 valid characters.'];
  if(kind==='catalog'&&!selected)next.summary.push('Choose a catalog title.');
  setIssues(next);if(!input.ok||Object.keys(next.fields).length||next.summary.length)return;
  const body={expectedRevision:snapshot.collection.revision,title:kind==='catalog'?null:title.trim(),position:pos!,data:input.value};
  const result=await actions.execute(async signal=>{
   try{return savedItem?await updateItem(snapshot.collection.id,savedItem.id,body,signal):await createItem(snapshot.collection.id,{...body,mediaItemId:kind==='catalog'?selected!.id:null},signal);}
   catch(error){if(!signal.aborted&&error instanceof ApiError){if(error.status===409)setConflict(true);if(error.status===404){try{await getCollection(snapshot.collection.id,signal);if(!signal.aborted){if(savedItem)setMissing(true);else setConflict(true);}}catch(parentError){if(!signal.aborted&&parentError instanceof ApiError&&parentError.status===404)onUnavailable?.();}}}throw error;}
  },refresh);if(result)onSaved();
 }}>
  {!savedItem&&<div className={styles.field}><label htmlFor={`${id}-kind`}>Entry kind</label><Select id={`${id}-kind`} value={kind} disabled={disabled} onChange={e=>setKind(e.target.value)}><option value="custom">Custom entry</option><option value="catalog">Catalog title</option></Select></div>}
  {kind==='custom'?<div className={styles.field}><label htmlFor={`${id}-title`}>Entry title</label><input id={`${id}-title`} value={title} aria-invalid={!!errors.title} aria-describedby={errors.title?`${id}-title-error`:undefined} disabled={disabled||missing} onChange={e=>setTitle(e.target.value)}/>{errors.title&&<p role="alert" id={`${id}-title-error`}>{errors.title.join(" ")}</p>}</div>:savedItem?<p>Catalog title: {selected?.title}. Its catalog identity cannot change.</p>:<CatalogPicker value={selected} onChange={setSelected} disabled={disabled}/>}
  <div className={styles.field}><label htmlFor={`${id}-position`}>Entry position</label><input id={`${id}-position`} inputMode="numeric" value={position} aria-invalid={!!errors.position} aria-describedby={errors.position?`${id}-position-error`:undefined} disabled={disabled||missing} onChange={e=>setPosition(e.target.value)}/>{errors.position&&<p role="alert" id={`${id}-position-error`}>{errors.position.join(" ")}</p>}</div>
  <EntryValueControls fields={snapshot.fields} value={values} onChange={setValues} errors={errors} disabled={disabled||missing}/>
  <ContentsError omitValidation error={conflict?null:actions.error} summary={[...server.summary,...issues.summary]}/>
  {(conflict||actions.outcomeUnknown)&&<div role="alert"><p>The collection changed, this catalog title may already be included, or these values no longer match its fields. Your draft is preserved. Reloading latest collection replaces this draft.</p><Button type="button" variant="secondary" disabled={disabled} onClick={async()=>{const latest=await reader.run(reload);if(latest){const nextItem='item' in latest?latest.item:undefined;try{const nextValues=entryDrafts(latest.details.fields,nextItem?.data);setSnapshot(latest.details);setSavedItem(nextItem);setTitle(nextItem?.mediaItemId?'':nextItem?.title??'');setKind(nextItem?.mediaItemId?'catalog':'custom');setSelected(nextItem?.mediaItemId?{id:nextItem.mediaItemId,title:nextItem.title}:null);setPosition(String(nextItem?.position??0));setValues(nextValues);setIssues({fields:{},summary:[]});setConflict(false);actions.clearError();}catch(error){setIssues({fields:{},summary:[error instanceof Error?error.message:'Unable to load entry.']});}}}}>Reload latest collection</Button><ContentsError error={reader.error}/></div>}
  {missing&&<p role="alert">This entry is no longer available. Cancel editing to refresh the collection.</p>}
  {actions.outcomeUnknown&&<p role="alert">The save outcome is unknown. Check current entries before saving again.</p>}
  <div className={styles.actions}><Button type="submit" disabled={disabled||conflict||missing||actions.outcomeUnknown}>{actions.pending?'Saving…':'Save entry'}</Button><Button type="button" variant="secondary" disabled={actions.pending} onClick={onCancel}>Cancel entry</Button></div>
 </form>;
}
