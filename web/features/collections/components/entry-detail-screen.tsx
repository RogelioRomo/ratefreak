"use client";
import Link from 'next/link';
import { useEffect,useState } from 'react';
import { useRouter,useSearchParams } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { isMediaId } from '@/features/catalog/query/filters';
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/ui/loading-state';
import { ErrorState } from '@/components/ui/error-state';
import { CollectionSession } from './collection-session';
import { EntryValues } from './entry-values';
import { EntryEditor } from './entry-editor';
import { EntryRemoval } from './entry-removal';
import { ContentsError } from './contents-error';
import { useEntryContents } from '../hooks/use-collection-contents';
import { useContentsActions } from '../hooks/use-contents-actions';
import { useEditorReload } from '../hooks/use-editor-reload';
import { clearObsoleteContents } from '../query/refresh-caches';
import { loadEntrySnapshot } from '../api/snapshots';
import type { EntrySnapshot } from '../interfaces/snapshots';
import type { EntryDetailProps } from '../interfaces/contents-props';
import type { CollectionSessionState } from '../interfaces/component-props';
import styles from './collections.module.css';
export function EntryDetailScreen({collectionId,itemId}:EntryDetailProps){const params=useSearchParams();return <div className={styles.screen}><Link className={styles.link} href={`/collections/${collectionId.toLowerCase()}`}>Back to collection</Link>{!isMediaId(collectionId)||!isMediaId(itemId)||params.size?<Unavailable/>:<CollectionSession returnTo={`/collections/${collectionId.toLowerCase()}`}>{state=><EntryContent key={`${collectionId.toLowerCase()}:${itemId.toLowerCase()}`} {...state} collectionId={collectionId.toLowerCase()} itemId={itemId.toLowerCase()}/>}</CollectionSession>}</div>;}
function Unavailable(){return <section className={styles.empty}><h1>Entry unavailable.</h1><p>This entry is missing or isn’t accessible to you.</p></section>;}
function EntryContent({collectionId,itemId,viewer,accountId,active}:EntryDetailProps&CollectionSessionState){
 const query=useEntryContents(viewer,collectionId,itemId,active),actions=useContentsActions(viewer,collectionId,active),reader=useEditorReload(active,`${collectionId}:${itemId}`),client=useQueryClient(),router=useRouter();
 const [editor,setEditor]=useState<{kind:'edit'|'remove';snapshot:EntrySnapshot}|null>(null),[saved,setSaved]=useState(''),[gone,setGone]=useState(false),[removed,setRemoved]=useState(false);
 const {refresh:refreshSession}=useSession(),unauthorized=query.error instanceof ApiError&&query.error.status===401;
 useEffect(()=>{if(unauthorized)void refreshSession().catch(()=>{});},[unauthorized,refreshSession]);
 async function fresh(signal:AbortSignal){let latest:EntrySnapshot;try{latest=await loadEntrySnapshot(collectionId,itemId,signal);}catch(error){if(!signal.aborted&&error instanceof ApiError&&error.status===404)setGone(true);throw error;}signal.throwIfAborted();client.setQueryData(['collections',viewer,'entry-snapshot',collectionId,itemId],latest);return latest;}
 async function refresh(signal:AbortSignal){await client.cancelQueries({predicate:q=>q.queryKey[0]==='collections'&&q.queryKey[3]===collectionId});signal.throwIfAborted();clearObsoleteContents(client,viewer,collectionId,['collections',viewer,'entry-snapshot',collectionId,itemId]);await fresh(signal);signal.throwIfAborted();void client.invalidateQueries({predicate:q=>q.queryKey[0]==='collections'&&q.queryKey[2]==='list'});}
 async function open(kind:'edit'|'remove'){if(editor||reader.pending||actions.pending||actions.needsRefresh)return;actions.clearError();setSaved('');const latest=await reader.run(fresh);if(latest)setEditor({kind,snapshot:latest});}
 if(gone||query.error instanceof ApiError&&query.error.status===404)return <Unavailable/>;
 if(unauthorized)return <><p role="alert">Your session expired.</p><Link href={`/login?${new URLSearchParams({returnTo:`/collections/${collectionId}`})}`}>Log in to continue</Link></>;
 if(!query.data)return query.isError?<ErrorState title="Entry unavailable." onRetry={()=>void query.refetch()}/>:<LoadingState label="Loading entry…"/>;
 const snapshot=query.data,owner=accountId===snapshot.details.collection.owner.id,locked=!!editor||reader.pending||actions.pending||actions.needsRefresh||!active;
 return <><header className={styles.heading}><div><h1>{snapshot.item.title}</h1><p>{snapshot.item.mediaItemId?'Catalog title':'Custom entry'} · {snapshot.details.collection.name}</p></div></header>{snapshot.item.mediaItemId&&<Link className={styles.link} href={`/catalog/${snapshot.item.mediaItemId}`}>View catalog title</Link>}
 <EntryValues fields={snapshot.details.fields} data={snapshot.item.data}/>{saved&&<p role="status">{saved}</p>}
 <ContentsError error={reader.error}/>{query.isError&&<ContentsError error={query.error}/>}
 {actions.needsRefresh&&<section role="status"><p>{removed?'Removed':'Saved'}. Refresh needed before another change.</p><Button variant="secondary" disabled={!active||actions.pending} onClick={()=>void actions.retryRefresh()}>Refresh collection</Button></section>}
 {owner&&<div className={styles.actions}><Button variant="secondary" disabled={locked} onClick={()=>void open('edit')}>Edit entry</Button><Button variant="secondary" disabled={locked} onClick={()=>void open('remove')}>Remove entry</Button></div>}
 {owner&&editor?.kind==='edit'&&<section className={styles.section}><h2>Edit entry</h2><EntryEditor details={editor.snapshot.details} item={editor.snapshot.item} active={active} actions={actions} refresh={refresh} reload={fresh} onCancel={()=>setEditor(null)} onSaved={()=>{setEditor(null);setSaved('Entry saved.');}} onUnavailable={()=>setGone(true)}/></section>}
 {owner&&editor?.kind==='remove'&&<EntryRemoval snapshot={snapshot} active={active} actions={actions} reload={fresh} refresh={async signal=>{signal.throwIfAborted();client.removeQueries({predicate:q=>q.queryKey[0]==='collections'&&q.queryKey[3]===collectionId});router.push(`/collections/${collectionId}`);}} onCancel={()=>setEditor(null)} onSaved={()=>{setEditor(null);setRemoved(true);setSaved('Entry removed.');}}/>}
 </>;
}
