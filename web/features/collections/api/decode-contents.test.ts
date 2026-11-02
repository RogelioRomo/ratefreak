import { expect, it } from "vitest";
import { decodeItem, decodeDetails, decodeImpact } from "./decode-contents";
import { decimalText } from "../query/exact-decimal";
import { details } from "../components/test-support";
it('decodes exact dynamic values and safe envelope integers',()=>{
 const item=decodeItem('{"id":"item","mediaItemId":null,"title":"Custom","data":{"score":0.1234567890123456789012345678,"seen":false,"text":"123"},"position":0,"createdAt":"date","updatedAt":"date"}');
 expect(decimalText(item.data.score as Parameters<typeof decimalText>[0])).toBe('0.1234567890123456789012345678');expect(item.data.seen).toBe(false);expect(item.data.text).toBe('123');expect(item.position).toBe(0);
 const value=decodeDetails(JSON.stringify({...details,fields:[{id:'field',key:'n',label:'Number',fieldType:'number',required:false,position:0,configuration:{}}]}));expect(value.collection.revision).toBe(1);
 expect(decodeImpact('{"fieldId":"field","key":"n","populatedItemCount":0,"revision":4}')).toEqual({fieldId:'field',key:'n',populatedItemCount:0,revision:4});
});
it('rejects unsafe revisions and malformed dynamic shapes rather than rounding or discarding',()=>{
 expect(()=>decodeDetails(JSON.stringify(details).replace('"revision":1','"revision":9007199254740993'))).toThrow();
 expect(()=>decodeItem('{"id":"i","mediaItemId":null,"title":"T","data":{"n":{"text":"1"}},"position":0,"createdAt":"d","updatedAt":"d"}')).toThrow();
});
