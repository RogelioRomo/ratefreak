import { expect, it } from "vitest";
import { fieldInput, newFieldDraft } from "./field-input";
it('defaults to optional text and validates immutable key rules, labels, positions and count',()=>{
 const draft={...newFieldDraft(),key:'notes',label:' Notes '};expect(fieldInput(draft,[])).toMatchObject({ok:true,value:{key:'notes',label:'Notes',fieldType:'text',required:false,configuration:{maxLength:1000},position:0}});
 for(const key of ['id','title','media_item_id','created_at','updated_at','Bad','1bad'])expect(fieldInput({...draft,key},[]).ok).toBe(false);
 expect(fieldInput({...draft,position:'2147483648'},[]).ok).toBe(false);expect(fieldInput({...draft,label:'x'.repeat(81)},[]).ok).toBe(false);
});
it('validates exact bounds and distinct select options without trimming their identity',()=>{
 const base={...newFieldDraft(),key:'n',label:'Number',fieldType:'number' as const,min:'0.1234567890123456789012345679',max:'0.1234567890123456789012345678'};expect(fieldInput(base,[]).ok).toBe(false);
 const select={...base,fieldType:'select' as const,options:[' A ','A']};expect(fieldInput(select,[])).toMatchObject({ok:true,value:{configuration:{options:[' A ','A']}}});expect(fieldInput({...select,options:['A','A']},[]).ok).toBe(false);expect(fieldInput({...select,options:Array.from({length:51},(_,i)=>String(i))},[]).ok).toBe(false);
});
