import type {QueryClient} from '@tanstack/react-query';
import {ApiError} from '@/shared/api/api-error';
export type StaffScope='staff'|'admin';
class AccessStore {
 private failures=new Map<string,number>();
 private controllers=new Map<string,Set<AbortController>>();
 private listeners=new Set<()=>void>();
 constructor(private client:QueryClient){}
 subscribe=(listener:()=>void)=>{this.listeners.add(listener);return ()=>{this.listeners.delete(listener);};};
 denied=(id:string,scope:StaffScope='staff')=>this.failures.get(id)??(scope==='admin'?this.failures.get(`admin:${id}`):0)??0;
 track(id:string,controller:AbortController,scope:StaffScope='staff'){const key=scope==='admin'?`admin:${id}`:id,requests=this.controllers.get(key)??new Set();requests.add(controller);this.controllers.set(key,requests);return ()=>{requests.delete(controller);if(!requests.size)this.controllers.delete(key);};}
 deny(id:string,status:number,scope:StaffScope='staff'){
  if(status===401)scope='staff';
  const key=scope==='admin'?`admin:${id}`:id;this.failures.set(key,status);
  for(const controller of this.controllers.get(key)??[])controller.abort();
  if(scope==='staff')for(const controller of this.controllers.get(`admin:${id}`)??[])controller.abort();
  const queryKey=scope==='admin'?['staff',id,'admin']:['staff',id];void this.client.cancelQueries({queryKey});this.client.removeQueries({queryKey});for(const listener of this.listeners)listener();
 }
 allow(id:string,scope:StaffScope='staff'){this.failures.delete(scope==='admin'?`admin:${id}`:id);for(const listener of this.listeners)listener();}
}
const stores=new WeakMap<QueryClient,AccessStore>();
export function accessStore(client:QueryClient):AccessStore{let store=stores.get(client);if(!store){store=new AccessStore(client);stores.set(client,store);}return store;}
export async function staffRequest<T>(client:QueryClient,id:string,operation:(signal:AbortSignal)=>Promise<T>,signal?:AbortSignal,scope:StaffScope='staff'):Promise<T>{
 const store=accessStore(client);if(store.denied(id,scope))throw new ApiError(store.denied(id,scope));
 const controller=new AbortController(),release=store.track(id,controller,scope),abort=()=>controller.abort();signal?.addEventListener('abort',abort,{once:true});if(signal?.aborted)controller.abort();
 try{controller.signal.throwIfAborted();const result=await operation(controller.signal);controller.signal.throwIfAborted();if(store.denied(id,scope))throw new ApiError(store.denied(id,scope));return result;}
 catch(error){if(!controller.signal.aborted&&error instanceof ApiError&&[401,403].includes(error.status))store.deny(id,error.status,scope);throw error;}
 finally{release();signal?.removeEventListener('abort',abort);}
}
