import { expect, it } from "vitest";
import { entryData, entryDrafts } from "./entry-input";
import { decimalValue, decimalText } from "./exact-decimal";
import type { CollectionField } from "../interfaces/collections";
const fields:CollectionField[]=[{id:'n',key:'n',label:'Number',fieldType:'number',required:false,position:0,configuration:{}},{id:'b',key:'b',label:'Boolean',fieldType:'boolean',required:false,position:0,configuration:{}},{id:'t',key:'t',label:'Text',fieldType:'text',required:false,position:0,configuration:{maxLength:10}}];
it('distinguishes exact decimals, zero, false, empty text and unset in a full replacement',()=>{
 const drafts=entryDrafts(fields,{n:decimalValue('0.1234567890123456789012345678'),b:false,t:''});const result=entryData(drafts,fields);expect(result.ok).toBe(true);if(result.ok){expect(decimalText(result.value.n as ReturnType<typeof decimalValue>)).toBe('0.1234567890123456789012345678');expect(result.value.b).toBe(false);expect(result.value.t).toBe('');}
 drafts.n={set:true,value:'0'};drafts.t.set=false;expect(entryData(drafts,fields)).toMatchObject({ok:true,value:{b:false}});const zero=entryData(drafts,fields);if(zero.ok){expect(decimalText(zero.value.n as ReturnType<typeof decimalValue>)).toBe('0');expect(Object.hasOwn(zero.value,'t')).toBe(false);}
});
it('blocks incompatible saved data, unknown keys, required blanks and invalid calendar dates',()=>{
 expect(()=>entryDrafts(fields,{unknown:'Do not discard'})).toThrow();expect(()=>entryDrafts(fields,{b:'false'})).toThrow();
 expect(entryData({t:{set:true,value:'  '}},[{...fields[2],required:true}]).ok).toBe(false);
 const date:CollectionField={...fields[2],key:'date',fieldType:'date',configuration:{}};expect(entryData({date:{set:true,value:'2024-02-29'}},[date]).ok).toBe(true);expect(entryData({date:{set:true,value:'2025-02-29'}},[date]).ok).toBe(false);
});
