import type {QueryClient} from '@tanstack/react-query';
import {ApiError} from '@/shared/api/api-error';
class AccessStore {
 private failures=new Map<string,number>();
 private controllers=new Map<string,Set<AbortController>>();
 private listeners=new Set<()=>void>();
 constructor(private client:QueryClient){}
 subscribe=(listener:()=>void)=>{this.listeners.add(listener);return ()=>{this.listeners.delete(listener);};};
 denied=(id:string)=>this.failures.get(id)??0;
 track(id:string,controller:AbortController){const requests=this.controllers.get(id)??new Set();requests.add(controller);this.controllers.set(id,requests);return ()=>{requests.delete(controller);if(!requests.size)this.controllers.delete(id);};}
 deny(id:string,status:number){this.failures.set(id,status);for(const controller of this.controllers.get(id)??[])controller.abort();void this.client.cancelQueries({queryKey:['staff',id]});this.client.removeQueries({queryKey:['staff',id]});for(const listener of this.listeners)listener();}
 allow(id:string){this.failures.delete(id);for(const listener of this.listeners)listener();}
}
const stores=new WeakMap<QueryClient,AccessStore>();
export function accessStore(client:QueryClient):AccessStore{let store=stores.get(client);if(!store){store=new AccessStore(client);stores.set(client,store);}return store;}
export async function staffRequest<T>(client:QueryClient,id:string,operation:(signal:AbortSignal)=>Promise<T>,signal?:AbortSignal):Promise<T>{
 const store=accessStore(client);if(store.denied(id))throw new ApiError(store.denied(id));
 const controller=new AbortController(),release=store.track(id,controller),abort=()=>controller.abort();signal?.addEventListener('abort',abort,{once:true});if(signal?.aborted)controller.abort();
 try{controller.signal.throwIfAborted();const result=await operation(controller.signal);controller.signal.throwIfAborted();if(store.denied(id))throw new ApiError(store.denied(id));return result;}
 catch(error){if(!controller.signal.aborted&&error instanceof ApiError&&[401,403].includes(error.status))store.deny(id,error.status);throw error;}
 finally{release();signal?.removeEventListener('abort',abort);}
}
