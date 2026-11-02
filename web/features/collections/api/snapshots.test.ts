import { beforeEach, expect, it, vi } from "vitest";
import { loadPageSnapshot, loadEntrySnapshot } from "./snapshots";
import { details, id } from "../components/test-support";
const mock=vi.fn<typeof fetch>();
beforeEach(()=>{mock.mockReset();vi.stubGlobal('fetch',mock);});
it('accepts one consistent revision and retries exactly once across changed reads',async()=>{
 const order:string[]=[];let read=0;mock.mockImplementation(async url=>{order.push(String(url));return String(url).includes('/items?')?Response.json({items:[],page:1,pageSize:20,totalCount:0}):Response.json({...details,collection:{...details.collection,revision:++read===1?1:2}});});
 const result=await loadPageSnapshot(id,1,new AbortController().signal);expect(result.details.collection.revision).toBe(2);expect(order).toHaveLength(6);expect(order.filter(u=>u.includes('/items?'))).toHaveLength(2);
});
it('stops a perpetually changing collection and never treats failed reads as empty',async()=>{
 let revision=0;mock.mockImplementation(async url=>String(url).includes('/items?')?Response.json({items:[],page:1,pageSize:20,totalCount:0}):Response.json({...details,collection:{...details.collection,revision:++revision}}));await expect(loadPageSnapshot(id,1,new AbortController().signal)).rejects.toThrow(/changing/);expect(mock).toHaveBeenCalledTimes(6);
 mock.mockResolvedValue(Response.json({}, {status:404}));await expect(loadEntrySnapshot(id,id,new AbortController().signal)).rejects.toMatchObject({status:404});
});
it('aborts between reads without continuing a snapshot sequence',async()=>{
 const controller=new AbortController();mock.mockImplementation(async()=>{controller.abort();return Response.json(details);});await expect(loadPageSnapshot(id,1,controller.signal)).rejects.toMatchObject({name:'AbortError'});expect(mock).toHaveBeenCalledTimes(1);
});
