import type { CollectionField } from "../interfaces/collections";
import type { CollectionItem, FieldValue } from "../interfaces/contents";
import type { EntryValueDraft, FormIssues, InputResult } from "../interfaces/form-input";
import { compareDecimals, decimalText, decimalValue, isDecimalValue } from "./exact-decimal";
import { validText } from "./input-common";
export function entryDrafts(fields:CollectionField[],data:CollectionItem['data']={}):Record<string,EntryValueDraft>{
 if(Object.keys(data).some(k=>!fields.some(f=>f.key===k)))throw new Error('Saved values do not match the current fields. Reload before editing.');
 const result:Record<string,EntryValueDraft>=Object.create(null) as Record<string,EntryValueDraft>;
 for(const field of fields){const value=data[field.key];const set=value!==undefined&&value!==null;
  if(set&&(field.fieldType==='number'?!isDecimalValue(value):field.fieldType==='boolean'?typeof value!=='boolean':typeof value!=='string'))throw new Error('A saved value cannot be edited safely. Reload the collection.');
  result[field.key]={set:field.required||set,value:set?(isDecimalValue(value)?decimalText(value):value as string|boolean):field.fieldType==='boolean'?false:''};
 }
 return result;
}
export function entryData(drafts:Record<string,EntryValueDraft>,fields:CollectionField[]):InputResult<CollectionItem['data']>{
 const issues:FormIssues={fields:{},summary:[]},data:CollectionItem['data']=Object.create(null) as CollectionItem['data'];
 if(Object.keys(drafts).some(k=>!fields.some(f=>f.key===k)))issues.summary.push('Fields changed. Reload before saving.');
 for(const field of fields){const draft=drafts[field.key],key=`data.${field.key}`;try{
  if(!draft?.set){if(field.required)throw new Error('A value is required.');continue;}
  let value:FieldValue=draft.value;
  switch(field.fieldType){
   case 'number':{if(typeof value!=='string')throw new Error('Enter a decimal number.');value=decimalValue(value);const {min,max}=field.configuration;if(min&&compareDecimals(decimalText(value),decimalText(min))<0||max&&compareDecimals(decimalText(value),decimalText(max))>0)throw new Error('Number is outside the allowed bounds.');break;}
   case 'boolean':if(typeof value!=='boolean')throw new Error('Choose Yes or No.');break;
   case 'text':if(typeof value!=='string'||!validText(value,field.configuration.maxLength??1000,true,!field.required))throw new Error(`Enter valid text up to ${field.configuration.maxLength??1000} characters${field.required?', with a nonblank value':''}.`);break;
   case 'date':if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value)||+value.slice(0,4)<1||Number.isNaN(Date.parse(`${value}T00:00:00Z`))||new Date(`${value}T00:00:00Z`).toISOString().slice(0,10)!==value)throw new Error('Enter a valid calendar date.');break;
   case 'select':if(typeof value!=='string'||!field.configuration.options?.includes(value))throw new Error('Choose an available option.');break;
  }
  data[field.key]=value;
 }catch(error){issues.fields[key]=[error instanceof Error?error.message:'Invalid value.'];}}
 return Object.keys(issues.fields).length||issues.summary.length?{ok:false,issues}:{ok:true,value:data};
}
