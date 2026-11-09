"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import { getCollection, updateCollection } from "../api/collections";
import { useCollectionWrite } from "../hooks/use-collection-write";
import { collectionKeys } from "../query/keys";
import { fieldErrors, metadataErrors, metadataInput, writeMessage } from "../query/metadata";
import type { CollectionEditorProps } from "../interfaces/component-props";
import type { CollectionDetails, CollectionMetadata } from "../interfaces/collections";
import { MetadataFields } from "./metadata-fields";
import styles from "./collections.module.css";
function initialValue(saved: CollectionDetails): CollectionMetadata {return {name:saved.collection.name,description:saved.collection.description ?? "",visibility:saved.collection.visibility};}
export function CollectionEditor({saved,accountId,active,onCancel,onSaved,reload,onUnavailable}: CollectionEditorProps) {
  const [snapshot,setSnapshot] = useState(saved);
  const [value,setValue] = useState(()=>initialValue(saved));
  const [errors,setErrors] = useState<Record<string,string[]>>({});
  const [conflict,setConflict] = useState(false), [reloadFailed,setReloadFailed] = useState(false);
  const client = useQueryClient(), write = useCollectionWrite();
  const id = snapshot.collection.id;
  useEffect(() => { if (write.error instanceof ApiError && write.error.status === 404) onUnavailable?.(); }, [write.error, onUnavailable]);
  if (write.error instanceof ApiError && write.error.status === 404) return <section><h2>Collection unavailable.</h2><p>This collection is missing or isn’t accessible to you.</p></section>;
  return <form className={styles.form} aria-label="Edit collection" noValidate onSubmit={async event=>{
    event.preventDefault(); if (!active || conflict) return;
    const invalid = metadataErrors(value); setErrors(invalid); if(Object.keys(invalid).length) return;
    const result = await write.run(async signal=>{
      let updated: CollectionDetails;
      try {updated = await updateCollection(id,{...metadataInput(value),expectedRevision:snapshot.collection.revision},signal);}
      catch(error) {if(!signal.aborted && error instanceof ApiError && error.status === 409) setConflict(true);throw error;}
      signal.throwIfAborted();
      await client.cancelQueries({predicate:q=>q.queryKey[0] === "collections" && q.queryKey[2] === "detail" && q.queryKey[3] === id});
      signal.throwIfAborted();
      client.removeQueries({predicate:q=>q.queryKey[0] === "collections" && q.queryKey[1] !== accountId && (q.queryKey[2] === "list" || (q.queryKey[2] === "detail" && q.queryKey[3] === id))});
      client.setQueryData(collectionKeys.detail(accountId,id),updated);
      void client.invalidateQueries({predicate:q=>q.queryKey[0] === "collections" && q.queryKey[2] === "list"});
      return updated;
    });
    if(result) onSaved(result.value);
  }}>
    <MetadataFields value={value} onChange={setValue} errors={{...fieldErrors(write.error),...errors}} disabled={write.pending || !active}/>
    {conflict ? <div role="alert"><p>The collection changed. Your draft is preserved. Reloading latest metadata replaces your draft.</p><Button type="button" variant="secondary" disabled={write.pending || !active} onClick={async()=>{
      setReloadFailed(false);
      const result = await write.run(signal=>reload?reload(signal):getCollection(id,signal));
      if(result){client.setQueryData(collectionKeys.detail(accountId,id),result.value);setSnapshot(result.value);setValue(initialValue(result.value));setConflict(false);setErrors({});write.clearError();}
      else setReloadFailed(true);
    }}>Reload latest metadata</Button>{reloadFailed && <p>Latest metadata could not be loaded. Your draft is preserved; try reloading again.</p>}</div> : write.error && <p role="alert">{writeMessage(write.error)}</p>}
    {write.error instanceof ApiError && write.error.status === 401 && <Link href={`/login?${new URLSearchParams({returnTo:`/collections/${id.toLowerCase()}`})}`}>Log in to continue</Link>}
    {write.outcomeUnknown && !conflict && <p role="alert">The save outcome is unknown. Your draft is preserved. Check the latest collection before saving again.</p>}
    <div className={styles.actions}><Button type="submit" disabled={!active || write.pending || conflict}>{write.pending ? "Saving…" : "Save metadata"}</Button><Button type="button" variant="secondary" disabled={write.pending} onClick={onCancel}>Cancel editing</Button></div>
  </form>;
}
