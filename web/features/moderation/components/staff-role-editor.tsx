'use client';
import {useEffect,useId,useRef,useState} from 'react';
import {Button} from '@/components/ui/button';
import {useStaffWrite} from '../hooks/use-staff-write';
import {setModerator,revokeAdmin} from '../api/staff-management';
import type {StaffAccount} from '../interfaces/staff-management';
import styles from './moderation.module.css';

export function StaffRoleEditor({account,available,onSaved,onReload,onBegin}:{account:StaffAccount;available:boolean;onSaved:()=>void;onReload:()=>Promise<void>;onBegin?:()=>void}){
 const id=useId(),input=useRef<HTMLTextAreaElement>(null);
 const [open,setOpen]=useState(false),[reason,setReason]=useState(''),[blocked,setBlocked]=useState(false),[message,setMessage]=useState(''),[reloading,setReloading]=useState(false);
 const write=useStaffWrite(available,'admin'),grant=!account.isModerator,administrator=account.isAdmin;
 useEffect(()=>{if(open)input.current?.focus();},[open]);
 const disabled=!available||write.pending||blocked||reloading;
 return <>
  {administrator&&<p className={styles.meta}>Administrator · Moderation access included</p>}
  <Button id={`${id}-trigger`} variant="secondary" disabled={disabled} onClick={()=>{onBegin?.();setOpen(true);}}>{administrator?'Revoke Admin':grant?'Grant moderator':'Revoke moderator'}</Button>
  {open&&<section className={styles.editor} aria-label={`${administrator?'Admin removal':'Moderator appointment'} for @${account.handle}`}>
   <form onSubmit={async event=>{
    event.preventDefault();if(disabled||!reason.trim()||reason.trim().length>1000)return;
    const result=await write.run(signal=>administrator?revokeAdmin(account.id,reason.trim(),signal):setModerator(account.id,grant,reason.trim(),signal));
    setBlocked(true);
    if(result.kind==='saved'){onSaved();return;}
    setMessage(result.kind==='uncertain'?'The result could not be confirmed. Reload account roles before another attempt.':administrator&&result.error.status===409?'The last Admin cannot be removed. Appoint another Admin before trying again.':result.error.status===429?'Too many role changes. Wait, then reload account roles.':'The appointment was rejected. Reload account roles before another attempt.');
   }}>
    <p>{administrator?'Revoke Admin':grant?'Grant moderator':'Revoke moderator'} access for @{account.handle} ({account.displayName})?</p>
    {administrator&&<p>The account will remain registered{account.isModerator?' and keep its Moderator role':''}. At least one Admin must remain.</p>}
    <fieldset disabled={disabled}>
     <label htmlFor={id}>{administrator?'Admin removal reason':'Appointment reason'}</label>
     <textarea ref={input} id={id} required maxLength={1000} rows={3} value={reason} onChange={event=>setReason(event.target.value)} aria-describedby={`${id}-hint`}/>
     <p id={`${id}-hint`} className={styles.meta}>Required. 1–1000 characters. Recorded in the audit history.</p>
     <div className={styles.actions}><Button type="submit" disabled={!reason.trim()||reason.trim().length>1000}>{write.pending?'Saving…':administrator?'Confirm Admin removal':grant?'Confirm grant':'Confirm revoke'}</Button><Button type="button" variant="secondary" onClick={()=>{setOpen(false);setReason('');document.getElementById(`${id}-trigger`)?.focus();}}>Cancel appointment</Button></div>
    </fieldset>
   </form>
   {message&&<><p role="alert">{message}</p><Button variant="secondary" disabled={!available||reloading} onClick={async()=>{
    setReloading(true);try{await onReload();}catch{setMessage('Account roles could not be refreshed. Try again.');}finally{setReloading(false);}
   }}>{reloading?'Reloading…':'Reload account roles'}</Button></>}
  </section>}
 </>;
}
