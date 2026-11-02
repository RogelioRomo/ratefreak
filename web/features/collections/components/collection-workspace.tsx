"use client";
import Link from 'next/link';
import { useEffect,useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import { Button } from '@/components/ui/button';
import { ErrorState } from '@/components/ui/error-state';
import { LoadingState } from '@/components/ui/loading-state';
import { useCollectionContents } from '../hooks/use-collection-contents';
import { useContentsActions } from '../hooks/use-contents-actions';
import { useEditorReload } from '../hooks/use-editor-reload';
import { loadPageSnapshot } from '../api/snapshots';
import { clearObsoleteContents } from '../query/refresh-caches';
import { collectionKeys } from '../query/keys';
import { itemsHref,itemsPage } from '../query/items-page';
import type { CollectionWorkspaceProps,OwnerEditor } from '../interfaces/contents-props';
import type { PageSnapshot } from '../interfaces/snapshots';
import type { CollectionField } from '../interfaces/collections';
import { CollectionEditor } from './collection-editor';
import { FieldManager } from './field-manager';
import { FieldEditor } from './field-editor';
import { FieldRemoval } from './field-removal';
import { EntryEditor } from './entry-editor';
import { EntryList } from './entry-list';
import { ContentsError } from './contents-error';
import styles from './collections.module.css';
export function CollectionUnavailable(){return <section className={styles.empty}><h1>Collection unavailable.</h1><p>This collection is missing or isn’t accessible to you.</p></section>;}
export function CollectionWorkspace({id,viewer,accountId,active}:CollectionWorkspaceProps){
 const params=useSearchParams(),page=itemsPage(new URLSearchParams(params.toString())),client=useQueryClient();
 const query=useCollectionContents(viewer,id,page,active),actions=useContentsActions(viewer,id,active),reader=useEditorReload(active,id);
 const [remembered,setRemembered]=useState<PageSnapshot|undefined>(query.data);
 if(query.data&&query.data!==remembered)setRemembered(query.data);
 const snapshot=query.data??remembered;
 const [editor,setEditor]=useState<OwnerEditor|null>(null),[saved,setSaved]=useState(''),[refreshGate,setRefreshGate]=useState(false),[unavailable,setUnavailable]=useState(false);
 const {refresh:refreshSession}=useSession();const unauthorized=query.error instanceof ApiError&&query.error.status===401;
 useEffect(()=>{if(unauthorized)void refreshSession().catch(()=>{});},[unauthorized,refreshSession]);
 async function fresh(signal:AbortSignal):Promise<PageSnapshot>{
  let latest:PageSnapshot;try{latest=await loadPageSnapshot(id,page??1,signal);}catch(error){if(!signal.aborted&&error instanceof ApiError&&error.status===404)setUnavailable(true);throw error;}signal.throwIfAborted();
  client.setQueryData(['collections',viewer,'page-snapshot',id.toLowerCase(),page],latest);
  client.setQueryData(collectionKeys.detail(viewer,id),latest.details);return latest;
 }
 async function refresh(signal:AbortSignal){
  await client.cancelQueries({predicate:q=>q.queryKey[0]==='collections'&&q.queryKey[3]===id.toLowerCase()});signal.throwIfAborted();
  clearObsoleteContents(client,viewer,id,['collections',viewer,'page-snapshot',id.toLowerCase(),page]);
  await fresh(signal);signal.throwIfAborted();void client.invalidateQueries({predicate:q=>q.queryKey[0]==='collections'&&q.queryKey[2]==='list'});
 }
 async function open(kind:OwnerEditor['kind'],field?:CollectionField){
  if(editor||actions.pending||actions.needsRefresh||refreshGate||reader.pending||!active)return;
  actions.clearError();setSaved('');const latest=await reader.run(fresh);if(!latest)return;
  if(kind==='edit-field'||kind==='remove-field'){const target=latest.details.fields.find(f=>f.id===field?.id);if(!target)return;setEditor({kind,snapshot:latest,field:target});}
  else setEditor({kind,snapshot:latest});
 }
 async function retryMetadataRefresh(){const result=await reader.run(refresh);if(result!==null)setRefreshGate(false);}
 if(page===null)return <><p role="alert">Invalid entry page.</p><Link href={`/collections/${id.toLowerCase()}`}>Reset entry page</Link></>;
 if(unavailable||query.error instanceof ApiError&&query.error.status===404)return <CollectionUnavailable/>;
 if(unauthorized)return <><p role="alert">Your session expired.</p><Link href={`/login?${new URLSearchParams({returnTo:`/collections/${id.toLowerCase()}`})}`}>Log in to continue</Link></>;
 if(!snapshot)return query.isError?<ErrorState title="Collection unavailable." description="Wait if requests are limited, then try again." onRetry={()=>void query.refetch()}/>:<LoadingState label="Loading collection…"/>;
 const collection=snapshot.details.collection,owner=accountId===collection.owner.id;
 const locked=!!editor||actions.pending||actions.needsRefresh||refreshGate||reader.pending||!active;
 const onSaved=()=>{setEditor(null);setSaved('Collection saved.');};
 const common={active,actions,refresh,onCancel:()=>{setEditor(null);actions.clearError();},onSaved};
 return <>
  <header className={styles.heading}><div><h1>{collection.name}</h1><p>{collection.owner.displayName} · @{collection.owner.handle}</p></div><span className={styles.visibility}>{collection.visibility==='private'?'Private':'Public'}</span></header>
  {collection.description&&<p className={styles.description}>{collection.description}</p>}
  {saved&&<p role="status">{saved}</p>}
  {(actions.needsRefresh||refreshGate)&&<section role="status"><p>Saved or removed. Refresh needed before another change.</p><Button type="button" variant="secondary" disabled={!active||actions.pending||reader.pending} onClick={()=>void(actions.needsRefresh?actions.retryRefresh():retryMetadataRefresh())}>Refresh collection</Button></section>}
  {query.isError&&<ErrorState title="Latest collection data unavailable." description="Your draft is preserved." onRetry={()=>void query.refetch()}/>}
  <ContentsError error={reader.error}/>
  {owner&&<Button variant="secondary" disabled={locked} onClick={()=>void open('metadata')}>Edit metadata</Button>}
  {owner&&editor?.kind==='metadata'&&<section className={styles.section}><h2>Edit metadata</h2><CollectionEditor saved={editor.snapshot.details} reload={async signal=>(await fresh(signal)).details} accountId={accountId!} active={active} onCancel={()=>setEditor(null)} onSaved={()=>{setEditor(null);setSaved('Collection saved.');setRefreshGate(true);void retryMetadataRefresh();}}/></section>}
  <section className={styles.section}><h2>Entries</h2>{owner&&<Button variant="secondary" disabled={locked} onClick={()=>void open('add-entry')}>Add entry</Button>}
   {owner&&editor?.kind==='add-entry'&&<EntryEditor {...common} details={editor.snapshot.details} reload={fresh} onUnavailable={()=>setUnavailable(true)}/>}
   {query.isPending?<LoadingState label="Loading entries…"/>:query.data?.page.items.length?<><p className={styles.summary}>{query.data.page.totalCount} {query.data.page.totalCount===1?'entry':'entries'}</p><EntryList collectionId={id} items={query.data.page.items}/></>:!query.isError?<section className={styles.empty}><h3>{page>1?'This page has no entries.':'No entries yet.'}</h3>{page>1&&<Link className={styles.link} href={itemsHref(id,1)}>Go to first entry page</Link>}</section>:null}
   <nav className={styles.pagination} aria-label="Entry pages">{page>1?<Link href={itemsHref(id,page-1)}>Previous entries</Link>:<span>Previous entries</span>}<span>Page {page}</span>{query.data&&page<10000&&page*20<query.data.page.totalCount?<Link href={itemsHref(id,page+1)}>Next entries</Link>:<span>Next entries</span>}</nav>
  </section>
  <FieldManager fields={snapshot.details.fields} owner={owner} locked={locked} onAdd={()=>void open('add-field')} onEdit={field=>void open('edit-field',field)} onRemove={field=>void open('remove-field',field)}/>
  {owner&&(editor?.kind==='add-field'||editor?.kind==='edit-field')&&<section className={styles.section}><h2>{editor.kind==='add-field'?'Add field':'Edit field'}</h2><FieldEditor {...common} details={editor.snapshot.details} field={editor.kind==='edit-field'?editor.field:undefined} itemCount={snapshot.page.totalCount} reload={async signal=>(await fresh(signal)).details}/></section>}
  {owner&&editor?.kind==='remove-field'&&<FieldRemoval {...common} collectionId={id} field={editor.field} revision={collection.revision}/>}
 </>;
}
