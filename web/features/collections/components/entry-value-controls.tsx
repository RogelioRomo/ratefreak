"use client";
import { Select } from "@/components/ui/select";
import { useId } from "react";
import type { EntryValueControlsProps } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function EntryValueControls({fields,value,onChange,errors,disabled}:EntryValueControlsProps){
 const prefix=useId();return <>{fields.map(field=>{const draft=value[field.key]??{set:false,value:''},id=`${prefix}-${field.id}`,error=errors[`data.${field.key}`];const change=(next:string|boolean)=>onChange({...value,[field.key]:{set:true,value:next}});const off=disabled||!draft.set;
 return <div className={styles.valueField} key={field.id}>
  {!field.required&&<label className={styles.check}><input type="checkbox" checked={draft.set} disabled={disabled} onChange={event=>onChange({...value,[field.key]:{...draft,set:event.target.checked}})}/>{`Set a value for ${field.label}`}</label>}
  <div className={styles.field}><label htmlFor={id}>{field.label}{field.required?' (required)':''}</label>
   {field.fieldType==='text'?<textarea id={id} rows={3} value={String(draft.value)} disabled={off} onChange={event=>change(event.target.value)} aria-invalid={!!error} aria-describedby={error?`${id}-error`:undefined}/>
   :field.fieldType==='boolean'?<Select id={id} value={String(draft.value)} disabled={off} onChange={event=>change(event.target.value==='true')} aria-invalid={!!error} aria-describedby={error?`${id}-error`:undefined}><option value="false">No</option><option value="true">Yes</option></Select>
   :field.fieldType==='select'?<Select id={id} value={String(draft.value)} disabled={off} onChange={event=>change(event.target.value)} aria-invalid={!!error} aria-describedby={error?`${id}-error`:undefined}><option value="" disabled>Choose an option</option>{field.configuration.options?.map(option=><option key={option} value={option}>{option}</option>)}</Select>
   :<input id={id} type={field.fieldType==='date'?'date':'text'} inputMode={field.fieldType==='number'?'decimal':undefined} value={String(draft.value)} disabled={off} onChange={event=>change(event.target.value)} aria-invalid={!!error} aria-describedby={error?`${id}-error`:undefined}/>}
   {error&&<p role="alert" id={`${id}-error`}>{error.join(' ')}</p>}
  </div></div>;
 })}</>;
}
