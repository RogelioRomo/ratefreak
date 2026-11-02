"use client";
import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { loadEntrySnapshot, loadPageSnapshot } from "../api/snapshots";
import { collectionKeys } from "../query/keys";
import { isMediaId } from "@/features/catalog/query/filters";
function usePause(viewer:string,active:boolean){const client=useQueryClient();useEffect(()=>{if(!active)void client.cancelQueries({queryKey:collectionKeys.viewer(viewer)});},[client,viewer,active]);}
export function useCollectionContents(viewer:string,id:string,page:number|null,active:boolean){
 const client=useQueryClient();usePause(viewer,active);
 return useQuery({queryKey:['collections',viewer,'page-snapshot',id.toLowerCase(),page],enabled:active&&page!==null&&isMediaId(id),retry:false,queryFn:async({signal})=>{
  const result=await loadPageSnapshot(id,page!,signal);signal.throwIfAborted();
  client.setQueryData(collectionKeys.detail(viewer,id),result.details);
  client.setQueryData(['collections',viewer,'items',id.toLowerCase(),result.details.collection.revision,result.details.collection.schemaVersion,page],result.page);
  return result;
 }});
}
export function useEntryContents(viewer:string,id:string,itemId:string,active:boolean){
 const client=useQueryClient();usePause(viewer,active);
 return useQuery({queryKey:['collections',viewer,'entry-snapshot',id.toLowerCase(),itemId.toLowerCase()],enabled:active&&isMediaId(id)&&isMediaId(itemId),retry:false,queryFn:async({signal})=>{
  const result=await loadEntrySnapshot(id,itemId,signal);signal.throwIfAborted();
  client.setQueryData(collectionKeys.detail(viewer,id),result.details);
  client.setQueryData(['collections',viewer,'item',id.toLowerCase(),result.details.collection.revision,result.details.collection.schemaVersion,itemId.toLowerCase()],result.item);
  return result;
 }});
}
