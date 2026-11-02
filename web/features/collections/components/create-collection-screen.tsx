"use client";
import Link from "next/link";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { CollectionSession } from "./collection-session";
import { MetadataFields } from "./metadata-fields";
import { createCollection } from "../api/collections";
import { collectionKeys } from "../query/keys";
import { fieldErrors, metadataErrors, metadataInput, writeMessage } from "../query/metadata";
import { useCollectionWrite } from "../hooks/use-collection-write";
import type { CollectionMetadata } from "../interfaces/collections";
import type { CollectionSessionState } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function CreateCollectionScreen() {
  return <div className={`${styles.screen} ${styles.split}`}><header className={styles.heading}><div><h1>A collection of your own.</h1><p>Start with a name and choose who can see it.</p></div></header><CollectionSession requireAccount returnTo="/collections/new">{state=><CreateForm {...state}/>}</CollectionSession></div>;
}
function CreateForm({viewer,active}: CollectionSessionState) {
  const [value,setValue] = useState<CollectionMetadata>({name:"",description:"",visibility:"private"});
  const [errors,setErrors] = useState<Record<string,string[]>>({});
  const write = useCollectionWrite(), client = useQueryClient(), router = useRouter();
  return <form className={styles.form} aria-label="Create collection" noValidate onSubmit={async event=>{
    event.preventDefault(); if (!active) return;
    const invalid = metadataErrors(value); setErrors(invalid); if (Object.keys(invalid).length) return;
    const result = await write.run(async signal=>{
      const saved = await createCollection(metadataInput(value),signal);
      signal.throwIfAborted();
      // Schedule invalidation without waiting on reads: their failure cannot turn a saved create into a replay.
      void client.invalidateQueries({queryKey: collectionKeys.viewer(viewer)});
      client.setQueryData(collectionKeys.detail(viewer,saved.collection.id),saved);
      return saved;
    });
    if (result) router.push(`/collections/${result.value.collection.id}`);
  }}>
    <MetadataFields value={value} onChange={setValue} errors={{...fieldErrors(write.error),...errors}} disabled={write.pending || !active}/>
    {write.error && <p role="alert">{writeMessage(write.error)}</p>}
    {write.outcomeUnknown && <p role="alert">The outcome is unknown. Check your collections before creating again. <Link href="/collections?scope=mine">Check My collections</Link></p>}
    <div className={styles.actions}><Button type="submit" disabled={write.pending || !active}>{write.pending ? "Creating…" : "Create collection"}</Button><Link className={styles.link} href="/collections">Back to collections</Link></div>
    <p className={styles.hint}>Entry and custom-field interfaces are coming in the next slice.</p>
  </form>;
}
