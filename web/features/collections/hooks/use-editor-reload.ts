"use client";
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import { useLayoutEffect,useRef,useState } from 'react';
export function useEditorReload(active:boolean,identity:string){
 const session=useSession();
 const enabled=useRef(active),live=useRef(false),epoch=useRef(0),request=useRef<AbortController|null>(null);const [pending,setPending]=useState(false),[error,setError]=useState<Error|null>(null);
 useLayoutEffect(()=>{live.current=true;return()=>{live.current=false;request.current?.abort();};},[]);
 useLayoutEffect(()=>{epoch.current++;enabled.current=active;return()=>{request.current?.abort();};},[active,identity]);
 async function run<T>(read:(signal:AbortSignal)=>Promise<T>):Promise<T|null>{
  if(!enabled.current||request.current)return null;const controller=new AbortController(),current=epoch.current;request.current=controller;setPending(true);setError(null);
  let abort!:()=>void;const cancelled=new Promise<never>((_,reject)=>{abort=()=>reject(new DOMException('Cancelled','AbortError'));controller.signal.addEventListener('abort',abort,{once:true});});
  try{const value=await Promise.race([read(controller.signal),cancelled]);controller.signal.throwIfAborted();return live.current&&epoch.current===current?value:null;}
  catch(failure){if(live.current&&!controller.signal.aborted&&epoch.current===current){setError(failure instanceof Error?failure:new Error('Read failed.'));if(failure instanceof ApiError&&failure.status===401)void session.refresh().catch(()=>{});}return null;}
  finally{controller.signal.removeEventListener('abort',abort);if(request.current===controller){request.current=null;if(live.current)setPending(false);}}
 }
 return {run,pending,error};
}
