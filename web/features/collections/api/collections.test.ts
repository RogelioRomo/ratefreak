import { beforeEach, expect, it, vi } from "vitest";
import { details } from '../components/test-support';
import { resetCsrfState } from "@/shared/api/csrf";
import { getCollections, getCollection, createCollection, updateCollection } from "./collections";
const mock=vi.fn<typeof fetch>();const id='a1111111-1111-1111-1111-111111111111';
beforeEach(()=>{resetCsrfState();mock.mockReset();vi.stubGlobal('fetch',mock);mock.mockImplementation(async url=>Response.json(String(url).endsWith('csrf')?{requestToken:'token'}:String(url).includes('?')?{items:[],page:1,pageSize:20,totalCount:0}:details));});
it('sends viewer list filters and cancellable detail reads',async()=>{
 const signal=new AbortController().signal;await getCollections({scope:'mine',page:2,pageSize:20},signal);await getCollection(id,signal);
 expect(mock.mock.calls[0][0]).toBe('/api/v1/collections?ownOnly=true&page=2&pageSize=20');expect(mock.mock.calls[1][0]).toBe(`/api/v1/collections/${id}`);expect(mock.mock.calls[1][1]?.signal).toBe(signal);
});
it('creates without fields and replaces all metadata using the loaded revision',async()=>{
 const signal=new AbortController().signal;await createCollection({name:'Cinema',description:null,visibility:'private'},signal);await updateCollection(id,{expectedRevision:7,name:'Films',description:'Notes',visibility:'public'},signal);
 const writes=mock.mock.calls.filter(([,o])=>o?.method!=='GET');expect(writes.map(([,o])=>o?.method)).toEqual(['POST','PATCH']);expect(JSON.parse(String(writes[0][1]?.body))).toEqual({name:'Cinema',description:null,visibility:'private'});expect(JSON.parse(String(writes[1][1]?.body))).toEqual({expectedRevision:7,name:'Films',description:'Notes',visibility:'public'});expect(writes[1][1]?.signal).toBe(signal);
});
