import { beforeEach, expect, it, vi } from "vitest";
import { getItems, createItem, updateField, deleteField, deleteItem } from "./contents";
import { resetCsrfState } from "@/shared/api/csrf";
import { decimalValue } from "../query/exact-decimal";
const mock=vi.fn<typeof fetch>();const id='a1111111-1111-1111-1111-111111111111';
beforeEach(()=>{resetCsrfState();mock.mockReset();vi.stubGlobal('fetch',mock);mock.mockImplementation(async url=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):new Response(null,{status:204}));});
it('sends complete typed replacements and exact decimal JSON through existing CSRF handling',async()=>{
 mock.mockImplementation(async(url,options)=>String(url).endsWith('csrf')?Response.json({requestToken:'token'}):options?.method==='POST'?new Response('{"id":"item","mediaItemId":null,"title":"Custom","data":{"n":9007199254740993},"position":0,"createdAt":"d","updatedAt":"d"}'):Response.json({id:'field',key:'n',label:'Number',fieldType:'number',required:false,position:0,configuration:{}}));
 const signal=new AbortController().signal;await createItem(id,{expectedRevision:2,mediaItemId:null,title:'Custom',data:{n:decimalValue('9007199254740993')},position:0},signal);await updateField(id,id,{expectedRevision:3,label:'Number',required:false,position:0,configuration:{min:decimalValue('0.0000000000000000000000000001')}},signal);
 const writes=mock.mock.calls.filter(([,o])=>o?.method!=='GET');expect(writes[0][1]?.body).toBe('{"expectedRevision":2,"mediaItemId":null,"title":"Custom","data":{"n":9007199254740993},"position":0}');expect(String(writes[1][1]?.body)).toContain('"min":0.0000000000000000000000000001');expect(writes[1][1]?.signal).toBe(signal);
});
it('uses bounded paging and exact query-only deletion revisions with204',async()=>{
 mock.mockResolvedValueOnce(Response.json({items:[],page:2,pageSize:20,totalCount:0}));const signal=new AbortController().signal;await getItems(id.toUpperCase(),2,signal);await deleteField(id,id,7,signal);await deleteItem(id,id,8,signal);
 expect(mock.mock.calls[0][0]).toBe(`/api/v1/collections/${id}/items?page=2&pageSize=20`);
 expect(mock.mock.calls.filter(([,o])=>o?.method==='DELETE').map(([url])=>url)).toEqual([`/api/v1/collections/${id}/fields/${id}?expectedRevision=7&confirm=true`,`/api/v1/collections/${id}/items/${id}?expectedRevision=8`]);
});
