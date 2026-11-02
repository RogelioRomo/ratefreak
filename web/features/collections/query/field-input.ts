import type { CollectionField } from "../interfaces/collections";
import type { FieldDefinitionInput } from "../interfaces/contents";
import type { FieldDraft, FormIssues, InputResult } from "../interfaces/form-input";
import { decimalText, decimalValue, compareDecimals } from "./exact-decimal";
import { positionValue, validText } from "./input-common";
export function newFieldDraft():FieldDraft{return {key:'',label:'',fieldType:'text',required:false,position:'0',maxLength:'1000',min:'',max:'',options:['']};}
export function savedFieldDraft(field:CollectionField):FieldDraft{return {...newFieldDraft(),key:field.key,label:field.label,fieldType:field.fieldType,required:field.required,position:String(field.position),maxLength:String(field.configuration.maxLength??1000),min:field.configuration.min?decimalText(field.configuration.min):'',max:field.configuration.max?decimalText(field.configuration.max):'',options:field.configuration.options?[...field.configuration.options]:['']};}
export function fieldInput(draft:FieldDraft,existing:CollectionField[],editingFieldId?:string):InputResult<FieldDefinitionInput>{
 const issues:FormIssues={fields:{},summary:[]},config:FieldDefinitionInput['configuration']={};const fail=(key:string,message:string)=>{issues.fields[key]=[message];};
 const prior=existing.find(f=>f.id===editingFieldId);
 if(!/^[a-z][a-z0-9_]{0,39}$/.test(draft.key)||['id','title','media_item_id','created_at','updated_at'].includes(draft.key))fail('key','Use 1–40 lowercase letters, digits or underscores, starting with a letter; this key may be reserved.');
 if(existing.some(f=>f.id!==editingFieldId&&f.key===draft.key))fail('key','A field with this key already exists.');
 if(prior&&(prior.key!==draft.key||prior.fieldType!==draft.fieldType))fail('key','Key and type cannot change.');
 if(!editingFieldId&&existing.length>=30)issues.summary.push('A collection can have at most 30 fields.');
 if(!validText(draft.label.trim(),80))fail('label','Label must contain 1–80 valid characters.');
 const position=positionValue(draft.position);if(position===null)fail('position','Position must be an integer from 0 to 2147483647.');
 switch(draft.fieldType){
  case 'text':{const n=positionValue(draft.maxLength);if(n===null||n<1||n>4000)fail('configuration','Text maximum length must be from 1 to 4000.');else config.maxLength=n;break;}
  case 'number':{try{if(draft.min!=='')config.min=decimalValue(draft.min);if(draft.max!=='')config.max=decimalValue(draft.max);if(config.min&&config.max&&compareDecimals(decimalText(config.min),decimalText(config.max))>0)fail('configuration','Minimum must not exceed maximum.');}catch{fail('configuration','Enter exact decimal bounds within the supported range.');}break;}
  case 'select':{if(draft.options.length<1||draft.options.length>50||draft.options.some(o=>!validText(o,80))||new Set(draft.options).size!==draft.options.length)fail('configuration','Use 1–50 distinct, nonblank options, each up to 80 valid characters.');else config.options=[...draft.options];break;}
  case 'boolean':case 'date':break;
  default:fail('fieldType','Choose a supported field type.');
 }
 return Object.keys(issues.fields).length||issues.summary.length?{ok:false,issues}:{ok:true,value:{key:draft.key,label:draft.label.trim(),fieldType:draft.fieldType,required:draft.required,position:position!,configuration:config}};
}
