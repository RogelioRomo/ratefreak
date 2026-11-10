'use client';
import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import {useMutation,useQueryClient} from '@tanstack/react-query';
import {ApiError} from '@/shared/api/api-error';
import {useStaff} from '../query/context';
import {staffRequest} from '../query/access';
import type {StaffScope} from '../query/access';
export function useStaffWrite(available:boolean,scope:StaffScope='staff'){
 const staff=useStaff(),client=useQueryClient(),active=staff.active&&available;
 const controller=useRef<AbortController|null>(null),operation=useRef<((signal:AbortSignal)=>Promise<void>)|null>(null),latest=useRef(active);
 const [pending,setPending]=useState(false);
 const mutation=useMutation({mutationFn:async()=>{if(!operation.current||!controller.current)throw Error('Action unavailable.');await staffRequest(client,staff.accountId,operation.current,controller.current.signal,scope);},retry:false,gcTime:0});
 useLayoutEffect(()=>{latest.current=active;if(!active)controller.current?.abort();},[active]);
 useEffect(()=>()=>{controller.current?.abort();operation.current=null;},[]);
 async function run(execute:(signal:AbortSignal)=>Promise<void>){
  if(!active||controller.current)return {kind:'uncertain' as const};
  const request=new AbortController();controller.current=request;operation.current=execute;setPending(true);
  try{await mutation.mutateAsync();return request.signal.aborted||!latest.current?{kind:'uncertain' as const}:{kind:'saved' as const};}
  catch(error){return !request.signal.aborted&&latest.current&&error instanceof ApiError&&error.status>=400&&error.status<500&&error.status!==408?{kind:'rejected' as const,error}:{kind:'uncertain' as const};}
  finally{if(controller.current===request){controller.current=null;operation.current=null;setPending(false);mutation.reset();}}
 }
 return {run,pending};
}
