'use client';
import {useId,useState} from 'react';
import {Button} from '@/components/ui/button';
import {ApiError} from '@/shared/api/api-error';
import {fieldErrors} from '@/features/auth/components/form-error';
import {useStaff} from '../query/context';
import {useStaffWrite} from '../hooks/use-staff-write';
import {resolveReport,setVisibility} from '../api/moderation';
import type {StaffEditorProps} from '../interfaces/moderation';
import styles from './moderation.module.css';
export function ActionEditor({target,report,available,segment,onSaved,onReload}:StaffEditorProps){
 const staff=useStaff(),id=useId(),ready=available&&staff.active;
 const write=useStaffWrite(ready);const [reason,setReason]=useState(''),[consent,setConsent]=useState(''),[action,setAction]=useState(''),[error,setError]=useState<Error|null>(null),[blocked,setBlocked]=useState<'uncertain'|'conflict'|'missing'|''>(''),[reloading,setReloading]=useState(false);
 const fingerprint=JSON.stringify([target?.id,target?.updatedAt,target?.hidden,report?.id,report?.status,reason,action]);
 if(consent&&(!ready||consent!==fingerprint))setConsent('');
 if(report?.status==='resolved'||target?.author?.id.toLowerCase()===staff.accountId.toLowerCase())return null;
 const fields=fieldErrors(error,'reason');
 async function reload(){setReloading(true);try{await onReload();setBlocked('');setConsent('');setError(null);}catch{setError(Error('The latest inspection could not be loaded. Try again.'));}finally{setReloading(false);}}
 function choose(next:string){if(!ready||write.pending||blocked)return;if(!reason.trim()||reason.trim().length>1000){setError(new ApiError(400,{errors:{reason:['Enter 1–1000 characters.']}}));document.getElementById(id)?.focus();return;}setError(null);setAction(next);setConsent(JSON.stringify([target?.id,target?.updatedAt,target?.hidden,report?.id,report?.status,reason,next]));}
 return <section className={styles.editor} aria-label="Moderation action">
  {error&&<p role="alert">{error instanceof ApiError?(error.status===429?'Too many moderation actions. Wait before trying again.':error.status===400?'Please check the moderation reason.':'The action was rejected. Reload the inspection before trying again.'):error.message}</p>}
  {blocked==='uncertain'&&<p role="alert">We couldn’t confirm the action. Reload the page before another decision. Reloading discards this reason. <Button variant="secondary" onClick={()=>window.location.reload()}>Reload page</Button></p>}
  {blocked==='conflict'&&<p role="alert">The report or content changed. Reload the latest inspection before another decision. <Button variant="secondary" disabled={!ready||reloading} onClick={()=>void reload()}>Reload latest</Button></p>}
  {blocked==='missing'&&<p role="alert">This resource is no longer available.</p>}
  <fieldset disabled={!ready||write.pending||!!blocked||reloading}>
   <label htmlFor={id}>Moderation reason</label><textarea id={id} rows={4} required maxLength={1000} value={reason} onChange={event=>setReason(event.target.value)} aria-invalid={fields?.length?true:undefined} aria-describedby={`${id}-hint${fields?.length?` ${id}-error`:''}`}/><p id={`${id}-hint`}>Required. 1–1000 characters. Recorded in the audit history.</p>{fields?.length&&<p id={`${id}-error`} role="alert">{fields.join(' ')}</p>}
   <div className={styles.actions}>{report?<>{target&&<Button variant="secondary" onClick={()=>choose('hide')}>{target.targetType==='profile'?'Hide profile text and resolve':'Hide and resolve'}</Button>}<Button variant="secondary" onClick={()=>choose('dismiss')}>Dismiss report</Button></>:target&&<Button variant="secondary" onClick={()=>choose(target.hidden?'restore':'hide')}>{target.targetType==='profile'?(target.hidden?'Restore profile text':'Hide profile text'):(target.hidden?'Restore content':'Hide content')}</Button>}</div>
   {consent&&<div className={styles.confirm}><p>Confirm {action} for {report?`report ${report.id}`:`${target?.targetType} ${target?.id}`} using this inspected version? {action==='dismiss'&&'Dismissal does not restore content.'}</p><div className={styles.actions}><Button onClick={async()=>{
    if(!ready||write.pending||blocked||consent!==fingerprint)return;setError(null);
    const inputReason=reason.trim();const result=await write.run(signal=>report?resolveReport(report.id,{decision:action as 'hide'|'dismiss',reason:inputReason,expectedTargetUpdatedAt:target?.updatedAt??null},signal):setVisibility(segment!,target!.id,{hidden:action==='hide',reason:inputReason,expectedTargetUpdatedAt:target!.updatedAt},signal));
    setConsent('');if(result.kind==='saved'){setReason('');onSaved();}else if(result.kind==='rejected'){if(result.error.status===409)setBlocked('conflict');else if(result.error.status===404)setBlocked('missing');else setError(result.error);}else setBlocked('uncertain');
   }}>{write.pending?'Saving…':`Confirm ${action}`}</Button><Button variant="secondary" onClick={()=>setConsent('')}>Cancel confirmation</Button></div></div>}
  </fieldset>
 </section>;
}
