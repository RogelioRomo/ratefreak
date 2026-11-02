"use client";
import { useLayoutEffect, useRef, useState } from "react";
import { useCollectionWrite } from "./use-collection-write";
import type { ConfirmedWrite } from "../interfaces/snapshots";
export function useContentsActions(viewer:string,id:string,active:boolean) {
 const write=useCollectionWrite();
 const live=useRef(false),epoch=useRef(0),busy=useRef(false),readController=useRef<AbortController|null>(null);
 const refreshRef=useRef<((signal:AbortSignal)=>Promise<void>)|null>(null);
 const blocked=useRef(false),enabled=useRef(active);
 const [needsRefresh,setNeedsRefresh]=useState(false),[pending,setPending]=useState(false);
 useLayoutEffect(()=>{live.current=true;return()=>{live.current=false;readController.current?.abort();};},[]);
 useLayoutEffect(()=>{epoch.current++;enabled.current=active;return()=>{readController.current?.abort();};},[viewer,id,active]);
 async function refreshOnly() {
  if(!refreshRef.current || !enabled.current)return false;
  const controller=new AbortController();readController.current=controller;
  try{await refreshRef.current(controller.signal);controller.signal.throwIfAborted();if(!live.current)return false;blocked.current=false;setNeedsRefresh(false);return true;}
  catch{if(live.current){blocked.current=true;setNeedsRefresh(true);}return false;}
  finally{if(readController.current===controller)readController.current=null;}
 }
 async function execute<T>(mutation:(signal:AbortSignal)=>Promise<T>,refresh:(signal:AbortSignal)=>Promise<void>):Promise<ConfirmedWrite<T>|null>{
  if(busy.current||blocked.current||!enabled.current)return null;
  busy.current=true;setPending(true);const current=epoch.current;let refreshing=false;
  try{
   const saved=await write.run(mutation);if(!saved||!live.current||current!==epoch.current)return null;
   blocked.current=true;setNeedsRefresh(true);refreshRef.current=refresh;
   // The write is terminal now. Close its editor before a session check can interrupt refresh.
   refreshing=true;
   void refreshOnly().finally(()=>{busy.current=false;if(live.current)setPending(false);});
   return {value:saved.value,needsRefresh:true};
  }finally{if(!refreshing){busy.current=false;if(live.current)setPending(false);}}
 }
 async function retryRefresh(){if(busy.current||!enabled.current)return;busy.current=true;setPending(true);try{await refreshOnly();}finally{busy.current=false;if(live.current)setPending(false);}}
 return {execute,retryRefresh,needsRefresh,pending,error:write.error,outcomeUnknown:write.outcomeUnknown,clearError:write.clearError};
}
