"use client";
import { Select } from "@/components/ui/select";
import { useEditorReload } from "../hooks/use-editor-reload";
import { useId, useState } from "react";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/shared/api/api-error";
import { createField, updateField } from "../api/contents";
import { fieldServerIssues } from "../query/server-issues";
import { fieldInput, newFieldDraft, savedFieldDraft } from "../query/field-input";
import type { FieldEditorProps } from "../interfaces/contents-props";
import type { FormIssues } from "../interfaces/form-input";
import type { FieldType } from "../interfaces/contents";
import { FieldConfiguration } from "./field-configuration";
import { ContentsError } from "./contents-error";
import styles from "./collections.module.css";
export function FieldEditor({details,field,itemCount,active,actions,refresh,reload,onCancel,onSaved}:FieldEditorProps){
 const [snapshot,setSnapshot]=useState(details),[editing,setEditing]=useState(field);
 const [draft,setDraft]=useState(()=>field?savedFieldDraft(field):newFieldDraft());
 const [issues,setIssues]=useState<FormIssues>({fields:{},summary:[]}),[conflict,setConflict]=useState(false),[loading,setLoading]=useState(false),[reloadError,setReloadError]=useState<Error|null>(null);
 const reader=useEditorReload(active,`${details.collection.id}:${field?.id??"new"}`);
 const server=fieldServerIssues(actions.error,snapshot.fields.length,!!editing),errors={...server.fields,...issues.fields};
 const id=useId(),disabled=!active||actions.pending||loading||reader.pending;
 return <form className={styles.form} aria-label={editing?'Edit field':'Add field'} noValidate onSubmit={async event=>{
  event.preventDefault();if(disabled||conflict||actions.outcomeUnknown)return;const input=fieldInput(draft,snapshot.fields,editing?.id);if(!input.ok){setIssues(input.issues);return;}setIssues({fields:{},summary:[]});
  const result=await actions.execute(async signal=>{try{return editing?await updateField(snapshot.collection.id,editing.id,{expectedRevision:snapshot.collection.revision,label:input.value.label,required:input.value.required,configuration:input.value.configuration,position:input.value.position},signal):await createField(snapshot.collection.id,{expectedRevision:snapshot.collection.revision,field:input.value},signal);}catch(error){if(!signal.aborted&&error instanceof ApiError&&error.status===409)setConflict(true);throw error;}},refresh);
  if(result)onSaved();
 }}>
  {editing?<p className={styles.hint}>Key: {editing.key} · Type: {editing.fieldType}. Key and type cannot change.</p>:<><div className={styles.field}><label htmlFor={`${id}-key`}>Key</label><input id={`${id}-key`} value={draft.key} aria-invalid={!!errors.key} aria-describedby={errors.key?`${id}-key-error`:undefined} disabled={disabled} onChange={e=>setDraft({...draft,key:e.target.value})}/>{errors.key&&<p role="alert" id={`${id}-key-error`}>{errors.key.join(" ")}</p>}</div><div className={styles.field}><label htmlFor={`${id}-type`}>Type</label><Select id={`${id}-type`} value={draft.fieldType} aria-invalid={!!errors.fieldType} disabled={disabled} onChange={e=>setDraft({...draft,fieldType:e.target.value as FieldType})}>{['text','number','boolean','date','select'].map(type=><option key={type} value={type}>{type[0].toUpperCase()+type.slice(1)}</option>)}</Select></div></>}
  <div className={styles.field}><label htmlFor={`${id}-label`}>Label</label><input id={`${id}-label`} value={draft.label} aria-invalid={!!errors.label} aria-describedby={errors.label?`${id}-label-error`:undefined} disabled={disabled} onChange={e=>setDraft({...draft,label:e.target.value})}/>{errors.label&&<p role="alert" id={`${id}-label-error`}>{errors.label.join(" ")}</p>}</div>
  <label className={styles.check}><input type="checkbox" checked={draft.required} disabled={disabled||(!editing&&itemCount>0)} onChange={e=>setDraft({...draft,required:e.target.checked})}/>Required</label>
  {!editing&&itemCount>0&&<p className={styles.hint}>A required field cannot be added while this collection has entries.</p>}
  <div className={styles.field}><label htmlFor={`${id}-position`}>Position</label><input id={`${id}-position`} inputMode="numeric" value={draft.position} aria-invalid={!!errors.position} aria-describedby={errors.position?`${id}-position-error`:undefined} disabled={disabled} onChange={e=>setDraft({...draft,position:e.target.value})}/>{errors.position&&<p role="alert" id={`${id}-position-error`}>{errors.position.join(" ")}</p>}</div>
  <FieldConfiguration errors={errors.configuration} value={draft} onChange={setDraft} disabled={disabled}/>
  <p className={styles.hint}>Required settings and configuration changes must keep every saved entry valid.</p>
  <ContentsError omitValidation error={conflict?null:actions.error} summary={[...server.summary,...issues.summary,...(errors.fieldType??[]),...(errors.required??[])]}/>
  {(conflict||actions.outcomeUnknown)&&<div role="alert"><p>The collection changed, this key is already used, or the proposed field conflicts with saved values. Your draft is preserved. Reloading latest collection replaces this draft.</p><Button type="button" variant="secondary" disabled={disabled} onClick={async()=>{setLoading(true);setReloadError(null);try{const next=await reader.run(reload);if(!next)return;const nextField=editing?next.fields.find(f=>f.id===editing.id):undefined;if(editing&&!nextField)throw new Error('This field is no longer available.');setSnapshot(next);setEditing(nextField);setDraft(nextField?savedFieldDraft(nextField):newFieldDraft());setConflict(false);actions.clearError();}catch(error){setReloadError(error instanceof Error?error:new Error('Reload failed.'));}finally{setLoading(false);}}}>Reload latest collection</Button><ContentsError error={reloadError??reader.error}/></div>}
  {actions.outcomeUnknown&&<p role="alert">The outcome is unknown. Check the latest collection before trying again.</p>}
  <div className={styles.actions}><Button type="submit" disabled={disabled||conflict||actions.outcomeUnknown}>{actions.pending?'Saving…':editing?'Save field':'Add field'}</Button><Button type="button" variant="secondary" disabled={actions.pending} onClick={onCancel}>Cancel field</Button></div>
 </form>;
}
