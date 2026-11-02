import { getCollection } from "./collections";
import { getItem, getItems } from "./contents";
import type { EntrySnapshot, PageSnapshot } from "../interfaces/snapshots";
async function stable<T>(id: string, read: (signal: AbortSignal)=>Promise<T>, signal: AbortSignal) {
 for(let attempt=0;attempt<2;attempt++) {
  const before=await getCollection(id,signal);signal.throwIfAborted();
  const value=await read(signal);signal.throwIfAborted();
  const after=await getCollection(id,signal);signal.throwIfAborted();
  if(before.collection.revision===after.collection.revision)return {details:after,value};
 }
 throw new Error("The collection keeps changing. Retry to load a consistent view.");
}
export async function loadPageSnapshot(id:string,page:number,signal:AbortSignal):Promise<PageSnapshot>{const result=await stable(id,s=>getItems(id,page,s),signal);return {details:result.details,page:result.value};}
export async function loadEntrySnapshot(id:string,itemId:string,signal:AbortSignal):Promise<EntrySnapshot>{const result=await stable(id,s=>getItem(id,itemId,s),signal);return {details:result.details,item:result.value};}
